# AccessControll_poc

POC: integrate an HID Aero **X1100** controller with a **VL70LF** reader through the HID Aero SDK (driver `HIDAeroNet64.dll` + .NET wrapper `HIDAeroWrap64.dll`).

## Always reference the project specification

**Read [docs/PROJECT_SPEC.md](docs/PROJECT_SPEC.md) before planning, implementing or reviewing any work in this repository.** It is the binding reference for:

- connection mode and startup command sequence (IP Client + TLS target)
- configuration order and POC values (1107, 1105, 108–115, 1102, 3103, 2116, 8304)
- VL70LF reader setup (command 112, OSDP) and door/ACR setup
- message loop, transaction handling, status and control commands
- solution structure and build order (§3): Web + Gateway hosts; only `AccessControl.Infrastructure.Aero` references the wrapper and only the Gateway (Windows x64 service) loads it; Web ↔ Gateway via SQL outbox
- the application solution lives in `AccessControll_App_poc/` (build from there); SDK DLLs in `AccessControll_App_poc/lib/HIDAero/<version>/`; .NET 10 compatibility probe in `tools/AeroSdkCompat`
- dev database: local SQL Server `localhost` (default instance), Windows Authentication, database `AccessControl_poc` — no Docker container
- engineering standards: `dotnet10-engineering-standards/` (CORE, FEATURE-PATTERN). Web shared views live in `Shared/Views/`, feature views in `Features/<Feature>/Views/`. No test project.
- open items that must not be guessed

If a change contradicts the spec, either update the spec in the same change (and say so) or stop and ask.

## Sources

- HID documentation, samples, tools and driver: `HID Doc/Extracted/` (SDK specification PDF is HID confidential — never publish it or its text).
- Code style: `.claude/skills/code-style` (.NET 10 Clean Architecture, no test project).
