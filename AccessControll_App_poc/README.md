# AccessControl (POC)

HID Aero X1100 + VL70LF integration. The binding reference is [`../docs/PROJECT_SPEC.md`](../docs/PROJECT_SPEC.md); standards in [`../dotnet10-engineering-standards`](../dotnet10-engineering-standards).

## Projects

| Project | Role | Platform |
|---|---|---|
| `AccessControl.Domain` | Entities, `Result` | AnyCPU |
| `AccessControl.Application` | Use cases, DTOs, ports | AnyCPU |
| `AccessControl.Infrastructure` | EF Core + SQL Server, auditing | AnyCPU |
| `AccessControl.Infrastructure.Aero` | HID Aero SDK integration — the only project referencing `HIDAeroWrap64` | x64 |
| `AccessControl.Web` | MVC admin + monitoring UI | AnyCPU |
| `AccessControl.Gateway` | Worker / Windows Service that owns the driver | x64 |

## First-time setup

Database: the locally installed SQL Server (default instance `MSSQLSERVER`, `localhost`), Windows Authentication, database **`AccessControl_poc`**.

```powershell
dotnet tool restore               # dotnet-ef

$cs = "Server=localhost;Database=AccessControl_poc;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True"
dotnet user-secrets set "ConnectionStrings:AccessControl" $cs --project src/AccessControl.Web
dotnet user-secrets set "ConnectionStrings:AccessControl" $cs --project src/AccessControl.Gateway
```

## Build and run

```powershell
dotnet build AccessControl.slnx
dotnet run --project src/AccessControl.Web        # UI
dotnet run --project src/AccessControl.Gateway    # driver host (Ctrl+C to stop)
```

Gateway run / port 3001 / firewall checks: [`GATEWAY-COMMANDS.md`](GATEWAY-COMMANDS.md).

Switch HID SDK build: `dotnet build -p:AeroSdkVersion=4.8.1.3` (DLLs in `lib/HIDAero/4.8.1.3/`).

## Features (Web)

| Menu | Route | What it does |
|---|---|---|
| Sites | `/Sites` | Locations that group controllers. Code unique (upper-case); a site with controllers cannot be deleted. |
| Controllers | `/Scps` | HID Aero X1100 controllers (`Scp` in code). IP Client: serial number required; IP Server: IP + port. SIO 0 (internal I/O) is created automatically. Saved as desired configuration only — nothing is sent to hardware until publishing (step 4). |

On a controller's page (step 3): **I/O panels (SIO)** — built-in SIO 0 plus external X100 / X200 / X300, each with its
inputs (general + enclosure & power supply) and relays · **Readers** — VL70LF on OSDP or Wiegand · **Doors** — reader(s),
lock relay, door contact, exit button, times and access mode.

Migrations applied: `AddSitesAndScps`, `AddSioPoints`, `DedicatedInputsNotMonitored`, `AddReadersAndDoors`, `AddPublishAndProvisioning`.

## Migrations

```powershell
dotnet ef migrations add <Name> --project src/AccessControl.Infrastructure --startup-project src/AccessControl.Web --output-dir Persistence/Migrations
dotnet ef migrations script --idempotent --project src/AccessControl.Infrastructure --startup-project src/AccessControl.Web
```

Migrations are applied as an explicit, reviewed step — never `Database.Migrate()` at startup.
