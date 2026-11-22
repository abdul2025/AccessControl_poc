# Gateway — run and verify commands

Commands for starting the Gateway, checking that it listens for HID Aero X1100 controllers on **TCP 3001**,
stopping it cleanly, and opening the Windows Firewall port.
Run them in **Windows PowerShell** (not the MSYS2/UCRT64 bash terminal — see the bash section at the end).

## 1. Start the Gateway (PowerShell window 1)

```powershell
cd C:\Users\Abdul\projects\AccessControll_poc\AccessControll_App_poc
dotnet run --project src/AccessControl.Gateway
```

Wait for:

```
HID Aero driver loaded: file 4.7.1.41, API 4.7.1.41.
Commands 11 and 12 accepted. Listening for IP Client controllers on TCP 3001, channel 44, cType 7.
Aero message pump started.
```

## 2. Check that port 3001 is listening (PowerShell window 2)

```powershell
Get-NetTCPConnection -LocalPort 3001 -State Listen | Select-Object LocalAddress, LocalPort, State, OwningProcess, @{n='Process';e={(Get-Process -Id $_.OwningProcess).ProcessName}}
```

Expected:

```
LocalAddress LocalPort  State OwningProcess Process
------------ ---------  ----- ------------- -------
0.0.0.0           3001 Listen          5608 AccessControl.Gateway
```

| What you see | Meaning |
|---|---|
| `State Listen` | The port is open and waiting for controllers |
| `Process AccessControl.Gateway` | Our Gateway owns the port |
| `0.0.0.0` | Listens on all network cards — the X1100 can reach it via this PC's IP |
| No output | Nothing is listening — the Gateway is not running or failed at startup (check window 1) |

## 3. Stop the Gateway cleanly

Press **Ctrl+C** in window 1. Expected:

```
Aero message pump stopped.
Command 203 Shutdown sent (accepted: True). Driver channels closed.
```

Run the command from step 2 again — it should return nothing (port released).

## 4. Windows Firewall rule for TCP 3001 (once per machine)

In IP Client mode the X1100 connects **to** this PC, so inbound TCP 3001 must be allowed.
If Windows showed an "allow access" popup the first time, choose **Allow** (Private networks).
Otherwise, in **PowerShell as Administrator**:

```powershell
New-NetFirewallRule -DisplayName "AccessControl Gateway (HID Aero 3001)" -Direction Inbound -Protocol TCP -LocalPort 3001 -Action Allow -Profile Private,Domain
```

Check the rule:

```powershell
Get-NetFirewallRule -DisplayName "AccessControl Gateway*" | Select-Object DisplayName, Enabled, Direction, Action
```

## 5. This PC's IP address (Host IP for the X1100 Configuration Manager)

```powershell
Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } | Select-Object InterfaceAlias, IPAddress
```

In the X1100 Configuration Manager → Host Comm: Connection Type **IP Client**, Host IP = this address, Port **3001**,
Data Security **TLS if Available** → Accept → Apply Settings, Reboot.

---

## From the MSYS2 / UCRT64 bash terminal

PowerShell cmdlets (`Get-NetTCPConnection`, `Select-Object`) do not exist in bash. Equivalents:

```bash
# Start the Gateway
cd /c/Users/Abdul/projects/AccessControll_poc/AccessControll_App_poc
dotnet run --project src/AccessControl.Gateway

# Check port 3001 (last column = process ID)
netstat -ano | grep ":3001"

# Which program owns that process ID (replace 5608); // is required in MSYS2 bash
tasklist //FI "PID eq 5608"

# Or run the PowerShell check from bash (\$_ stops bash from expanding $_)
powershell -NoProfile -Command "Get-NetTCPConnection -LocalPort 3001 -State Listen | Select-Object LocalAddress,LocalPort,State,OwningProcess,@{n='Process';e={(Get-Process -Id \$_.OwningProcess).ProcessName}}"
```
