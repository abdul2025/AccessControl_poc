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

## 6. Bench test: connect the X1100 kit to this laptop

The HID SDK has **no network scan for controllers**. In IP Client mode the controller **calls the Gateway**:
every X1100 commissioned with Host IP = this laptop connects by itself and appears in the Gateway log
(step 4: in the MVC as a *Pending controller*). Full commissioning rules: `docs/PROJECT_SPEC.md` §5.2.1.

1. **Network** — connect the X1100 to the laptop directly (Ethernet cable) or via the same switch.
   Give the laptop's Ethernet adapter a static IP on the controller's factory subnet:
   IP **192.168.0.10**, mask **255.255.0.0** (Settings → Network → Ethernet → IP assignment → Manual).
   Check it:

   ```powershell
   Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -like '192.168.0.*' } | Select-Object InterfaceAlias, IPAddress, PrefixLength
   Test-Connection 192.168.0.251 -Count 2      # controller reachable (factory default IP)
   ```

2. **Commission the controller** (Configuration Manager in the browser):
   - DIP switch **2 ON** (others OFF) → factory communication defaults; power-cycle or press RESET.
   - Open `https://192.168.0.251` (accept the certificate warning). DIP switch **1 ON** enables the default login
     `admin` / `password` for 5 minutes. Change the password.
   - **Host Comm**: Connection Type **IP Client**, Host IP **192.168.0.10** (this laptop), Port **3001**,
     Data Security **TLS if Available**, Enable Peer Certificate unchecked → Accept.
   - **Network**: keep 192.168.0.251 (or another free address on 192.168.0.x).
   - **Auto-Save ON**.
   - **Apply Settings, Reboot**, then set **all DIP switches OFF** (otherwise configuration is not saved to flash).

3. **Firewall** — allow inbound TCP 3001 (section 4).

4. **Run the Gateway** in Development (auto-accept is on only there):

   ```powershell
   cd C:\Users\Abdul\projects\AccessControll_poc\AccessControll_App_poc
   dotnet run --project src/AccessControl.Gateway
   ```

5. **Expected log** within a few seconds after the controller reboots:

   ```
   SCP 1 comm status: Communication OK (...), channel 44, ..., new session True.
   SCP 1 ID report: serial 2001843, device 4 v27, firmware 2.5, MAC 00:18:9E:..., programmed SCP ID 1, ...
   SCP 1 (serial 2001843): accepting connection — posting 1107, 1105, 1116, 302, 303.
   SCP 1: 1107 ScpDeviceSpecification (tag ...) OK.
   ... 1105, 1116, 302, 303 ... OK.
   ```

   Then check that port 3001 shows an **Established** connection from 192.168.0.251:

   ```powershell
   Get-NetTCPConnection -LocalPort 3001 | Select-Object LocalAddress, LocalPort, RemoteAddress, State
   ```

   **Record for the spec (open items 2, 12, 13):** firmware version, whether **1116** returned OK, and the
   controller's serial number. Every additional controller commissioned to the same Host IP shows up the same way
   with its own serial and SCP ID.

**Troubleshooting**

| Symptom | Check |
|---|---|
| `Test-Connection 192.168.0.251` fails | Laptop IP/mask, cable, DIP 2 ON at power-up |
| Gateway listening but no `comm status` | Firewall rule (section 4); Host IP on the controller = laptop IP; controller rebooted after Apply |
| Connects, then drops after ~15 s | 1107 not OK — check the `posted 1107` / `OK` lines and any NAK |
| `PENDING (not accepted)` | Not running in Development — `AutoAcceptControllers` is false |

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
