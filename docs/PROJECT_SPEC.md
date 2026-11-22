# AccessControll_poc — Project Specification

> **Status:** Draft v0.3 · 2026-11-22 — v0.2 adds SDK terminology, .NET 10 compatibility results (§2.1–2.2) and the solution structure (§3); v0.3 adds hardware configuration via MVC with desired state + publish (§3.7) and the revised build order (§3.6); v0.4 = review against HID SDK spec §3.3, 3.6, 5.3, 5.9–5.15, 11.8, 14.4, 14.7–14.11 (verify-then-reload via RMS, reset rule, IP Client ID assignment, 1116-before-302, IP Client cType values, nTransactions ≥ 60000)
> **Scope:** Proof of concept — use the HID Aero SDK to **establish** a connection with, **configure**, and **communicate** with an HID Aero **X1100** controller driving a **VL70LF** reader.
> This document is the binding reference for all work in this repository. When code and this spec disagree, fix one of them explicitly — never silently.

---

## 1. Context and goals

### 1.1 Business context

This POC is the hardware-integration foundation for the **VMS & ACS** programme (Attendance, Access Control and Visitor Management) — SANS (client) / NERA (integrator). Agreed in the 12-Nov-2026 workshop (`HID Doc/Extracted/MoM_Access_Visitor_Workshop_2026-11-12.docx`):

- HID hardware is **retained**; NERA builds the integration **via the SDK** (replacing manual work in Access360 / IDCube).
- Rollout starts at one SANS site, then remaining SANS sites, then SACA and NERA → many controllers at remote sites talking to one central host.
- SANS supplies in/out readers, one controller and sample cards for the initial integration test.

Requirements from the workshops that this POC must prove are technically achievable:

| Requirement (MoM) | SDK mechanism | Section |
|---|---|---|
| Automatic card provisioning / revocation (no manual enrolment) | Command 8304 add/update, 3305 delete | §7.6 |
| Card suspension during leave, activation on handover | 8304 `flags` (active bit), activation/deactivation dates | §7.6 |
| Access levels by job family / location; overcome IDCube's 8-groups-per-card limit | 1105 `nAlvl` (up to 32 per card configured here), 2116 | §6.3, §7.5 |
| Door-held-open alert (5–10 min) | 115 `dc_held`, transaction 9:x | §7.3, §8 |
| Detect readers "online but not reading" | 407 ACR status, OSDP reader online/offline CoS, 404 SIO status | §8.3 |
| ~50,000 offline events buffered on controller | 1107 `nTransactions`, 303 transaction index | §6.3, §8.1 |
| Logs not lost when a reader is replaced | Host persists every transaction; logs are keyed by host IDs, not hardware | §9 |
| Attendance feed from in/out readers | Transactions 5:x / 6:x from IN and OUT ACRs | §8.2 |
| Central, hierarchical multi-site view | IP Client mode, one listening channel for all controllers | §5 |

### 1.2 POC goals (definition of done)

1. Host application connects to the X1100 and receives Comm Status + ID Report.
2. Host fully configures the controller from scratch (system, SCP, cardholder DB, time, SIO, I/O, VL70LF reader, ACR, formats, schedules, access levels).
3. A card presented to the VL70LF produces a **granted** transaction for an enrolled card and a **denied** transaction for an unknown card, both received and persisted by the host.
4. Host can remotely unlock the door (311), drive the reader LED/buzzer, and read live status (401/404/407).
5. Host survives controller reboot and network loss: reconnects, re-sends required allocation commands, and collects buffered transactions without loss or duplication.

### 1.3 Out of scope (for this POC)

Visitor management, attendance rules, ERP/Fusion/TeamSmart/Yaqeen/Elm integrations, IDCube migration, mobile credentials, SIO expansion boards (X100/X200/X300), anti-passback, elevator control, OSDP file transfer (reader firmware update).

---

## 2. Hardware and software baseline

| Item | Value | Notes |
|---|---|---|
| Controller (SCP) | HID Aero **X1100** (X1100A / X1100C) | Internal SIO: 2 reader ports, 7 inputs, 4 outputs (SIO model **196**). Max per SCP: 64 readers/doors, 615 inputs, 388 outputs. |
| Reader | **VL70LF** | ⚠ Not described in the HID document set. Interface (OSDP vs Wiegand), OSDP address/baud, keypad, and card technology are **open items** (§12). Spec assumes **OSDP over RS-485** as the target, Wiegand as fallback. |
| Driver (native) | `HIDAeroNet64.dll` | **In use: 4.7.1.41** (`lib/HIDAero/4.7.1.41/`). Target: 4.8.1.3 from `HID Doc/Extracted/Driver/Driver/HID_aero_4_8_1_3.zip` — every file in it is password-protected (§12). |
| .NET wrapper | `HIDAeroWrap64.dll` | **In use: 4.7.1.32**, targets **netstandard 2.0**. Namespace `HID.Aero.ScpdNet.Wrapper`. |
| Host OS / arch | **Windows x64 only** | Driver is a native Windows DLL. Every project that loads it must target `x64`, and the DLLs must sit next to the executable (Copy to Output Directory). |
| Host framework | .NET 10, ASP.NET Core | Per `.claude/skills/code-style`. |
| Controller config UI | X1100 Configuration Manager (web) | Default `admin` / `password` after toggling DIP 1. **Must be changed** on first use. |

### 2.1 SDK terminology

"HID Aero SDK" = the whole kit: native driver, .NET wrapper, C/C++ headers (`.h/.lib/.def`), SDK Specification, Getting Started guides, samples and tools. Only two files ship with our application:

```
our C# code → HIDAeroWrap64.dll (.NET API we program against) → HIDAeroNet64.dll (native driver: TCP/TLS, protocol, queues) → network → X1100
```

- Reference **only the wrapper** from .NET; copy the native driver to the output folder.
- Always deploy **both DLLs from the same SDK package** — never mix versions.
- "SDK 4.7 / 4.8" is the version of the kit/driver on the host. The X1100 has its **own firmware version** (reported in the ID report) — versioned independently.
- DLL layout: `lib/HIDAero/<sdk-version>/HIDAeroNet64.dll` + `HIDAeroWrap64.dll`. Switching versions = dropping in a new folder and changing one MSBuild property (`AeroSdkVersion`).

### 2.2 .NET 10 compatibility — results (2026-11-22)

Probe: `tools/AeroSdkCompat` (console, net10.0, x64). Run: `dotnet run [-- --listen [--accept] [--seconds N]] [-p:AeroSdkVersion=x]`; the message pump runs until Q / Esc / Ctrl+C.

| Check | 4.7.1.41 / wrap 4.7.1.32 | 4.8.1.3 |
|---|---|---|
| Wrapper loads in .NET 10 (netstandard 2.0) | ✅ | ⏳ password |
| Native driver loads in x64 process | ✅ | ⏳ |
| P/Invoke `scpGetDllVersion` | ✅ 4.7.1.41 | ⏳ |
| Structure command `scpCfgCmndEx` (cmd 11, `CC_SYS`) | ✅ | ⏳ |
| String command `scpConfigCommand` (cmd 12, IP Client listener 3001) | ✅ | ⏳ |
| Message pump `SCPReplyMessage.GetMessage` (no traffic) | ✅ | ⏳ |
| Shutdown (cmd 203) | ✅ | ⏳ |
| Real controller: CommStatus / ID report / transactions decode | ⏳ hardware | ⏳ |

**Decision:** build on **.NET 10 + SDK 4.x**. Start with 4.7.1.41; move to 4.8.1.3 once the probe passes with it. Real-controller decoding is verified at bench step 1–3 (§11).

Physical prerequisites before any SDK work (from X1100 install guide PLT-04233 / PLT-08293 and Getting Started ch. 04):

- Battery jumper **ON**.
- All DIP switches **OFF** (normal operating mode) — in factory-default mode configuration is **not** saved to flash.
- **Auto-Save ON** in Configuration Manager.
- OSDP reader wiring: two twisted pairs (power + RS-485 A/B), shielded; terminate RS-485 for runs > 200 ft. Wiegand cable may not meet OSDP requirements.

---

## 3. Solution structure

Follows `.claude/skills/code-style` (Clean Architecture, feature folders, `Result` flow, EF Core + SQL Server, **no test project**), with **one deliberate extension: a Windows worker host that owns the HID driver** (§3.1).

### 3.1 Why a separate gateway host

| Constraint | Consequence |
|---|---|
| Driver is a native **Windows x64** DLL | Only one process needs to be Windows-bound; the Web host stays portable. |
| **One driver instance per process**, one listener on TCP 3001 | Hosting it inside the Web app breaks when the web app scales out, recycles (IIS) or restarts during deploys — controllers would drop. |
| Message loop must run **continuously** | A long-running Windows Service is the natural home. |
| Hardware commands must survive restarts and be auditable | Web ↔ gateway talk through **SQL tables** (command outbox + events), not in-memory calls. No extra broker for the POC. |

### 3.2 Projects

Solution name `AccessControl` (placeholder). The solution lives in **`AccessControll_App_poc/`**; repository-level docs, standards, HID material and the probe stay at the root.

```
AccessControll_poc/                  repository root
  docs/PROJECT_SPEC.md   CLAUDE.md   dotnet10-engineering-standards/   HID Doc/
  tools/AeroSdkCompat/               compatibility probe (§2.2) — uses AccessControll_App_poc/lib/HIDAero
  AccessControll_App_poc/            ← the application
    AccessControl.slnx
    global.json                      sdk 10.0.404, rollForward latestPatch
    Directory.Build.props            net10.0, Nullable, ImplicitUsings, analyzers, AeroSdkVersion/AeroSdkDir
    Directory.Packages.props         central package management (all versions pinned here)
    dotnet-tools.json                local dotnet-ef
    lib/HIDAero/<sdk-version>/       HIDAeroNet64.dll + HIDAeroWrap64.dll (§2.1)
    src/
  AccessControl.Domain/              no dependencies
  AccessControl.Application/         -> Domain
  AccessControl.Infrastructure/      -> Application, Domain      EF Core, SQL Server, auditing (portable, AnyCPU)
  AccessControl.Infrastructure.Aero/ -> Application, Domain      ONLY project that references HIDAeroWrap64 (x64)
  AccessControl.Web/                 -> Application, Infrastructure                 MVC admin + monitoring UI
  AccessControl.Gateway/             -> Application, Infrastructure, Infrastructure.Aero
                                        Worker Service, x64, runs as Windows Service
```

Dependency rules (in addition to code-style §1):
- `Web` **must not** reference `Infrastructure.Aero` → it never loads the driver and can run anywhere.
- `Application` defines hardware ports; it never sees Aero types, command numbers or `CC_*` structures.
- `Api` host is **not** built for the POC; add it later for ERP / IDM / visitor integrations (code-style Api rules apply).

### 3.3 Runtime flow

```
 Web (MVC)                         SQL Server                         Gateway (Windows Service, x64)
 ─────────                         ──────────                         ──────────────────────────────
 admin edits card / door  ──►  domain tables (Credentials, …)
                          ──►  ControllerCommands (outbox) ──poll──►  CommandDispatcher
                                  Pending→Sent→Succeeded/Failed ◄───  translates to Aero cmds → driver
                                                                      AeroMessagePump (GetMessage loop)
 live monitoring  ◄──poll──    AccessEvents (append-only)    ◄──────  TransactionMapper
                               Controllers.Status / LastSeen ◄──────  CommStatus / ID report handler
                               ControllerSyncState           ◄──────  ProvisioningService (reconnect → 1107/1105 → full or delta download)
```

- Web writes **intent** ("upsert credential X", "unlock door Y") as `ControllerCommand` rows; the gateway translates intent into SDK commands. The outbox speaks domain language, not Aero.
- Every reply is persisted before processing (§3.5 rule 4). Duplicate transactions are ignored via a unique key `(ControllerId, TransactionSerial)`.
- Desired state lives in the database; on (re)connect the gateway re-provisions the controller from it.

### 3.4 Folders per project

```
AccessControl.Domain/
  Common/Entities/                 BaseEntity, AuditableEntity, IHasDisplayName        (shared kernel)
  Common/Results/                  Result, Result<T>
  Entities/                        Site, Controller, Door (+ Reader child), CardFormat, Schedule (+ ScheduleInterval),
                                   Holiday, AccessLevel (+ AccessLevelDoor), Cardholder, Credential,
                                   AccessEvent, ControllerCommand
  Enums/{Feature}/                 ControllerStatus, ConnectionMode, ReaderInterface (Osdp/Wiegand), DoorMode,
                                   CredentialStatus, CommandStatus, CommandKind, AccessEventKind …

AccessControl.Application/
  Common/Interfaces/               ICurrentUser
  Common/Paging/                   PagedRequest, PagedResponse<T>
  Features/{Feature}/              Service, Errors, DTOs/, Interfaces/, Ports/   (code-style slice)
  Features/ControllerCommands/Ports/IControllerCommandQueue.cs   enqueue intent (used by all features)
  Hardware/Ports/                  IControllerDriver   — implemented by Infrastructure.Aero; added in step 4, when Application's
                                   provisioning first needs the driver (step 1 keeps all driver logic inside Infrastructure.Aero)
  DependencyInjection.cs           AddApplication()

AccessControl.Infrastructure/
  Auditing/                        AuditableEntityInterceptor, CurrentUser
  Persistence/AccessControlDbContext.cs
  Persistence/Features/{Feature}/  Configuration, Repository, Queries
  Persistence/Migrations/
  DependencyInjection.cs           AddInfrastructure(configuration)

AccessControl.Infrastructure.Aero/         <PlatformTarget>x64</PlatformTarget>, references lib/HIDAero/$(AeroSdkVersion)
  AeroOptions.cs                   section "Aero": ChannelId, Port, ChannelType, TimeoutMs, DebugFile, GmtOffset …
  Driver/AeroDriverHost.cs         singleton: cmd 11 + cmd 12 on start, cmd 203 on stop
  Driver/AeroCommandWriter.cs      scpCfgCmndEx (structure-based); command tags; maps CmndStatus/NAK back to callers
  Driver/AeroControllerDriver.cs   implements IControllerDriver
  Messages/AeroMessagePump.cs      dedicated thread: GetMessage loop → Channel<AeroReply>
  Messages/Mappers/                CommStatus, IdReport, Transaction, CmndStatus, Nak, Sr* → Application DTOs
  Commands/                        one builder per command family: Scp (1107/1105/1116/302/303), Sio (108/109/110/111),
                                   Reader (112), Acr (115), Points (113/114), Formats (1102), Schedules (1104/3103),
                                   AccessLevels (2116), Cards (8304/3305), Control (307/308/311/315/325)
  Descriptions/                    code → text tables (comm status, error codes, transaction types)
  DependencyInjection.cs           AddAeroDriver(configuration)

AccessControl.Gateway/                     Worker Service, <PlatformTarget>x64</PlatformTarget>, UseWindowsService()
  Program.cs                       AddApplication + AddInfrastructure + AddAeroDriver + hosted services
  Services/DriverLifetimeService.cs        start/stop driver
  Services/ReplyProcessingService.cs       consumes Channel<AeroReply> → persist events/status
  Services/CommandDispatchService.cs       polls ControllerCommands outbox → IControllerDriver
  Services/ProvisioningService.cs          on ID report: accept (1107 within 15 s), 1105, time, sync config, 303
  appsettings.json                  Aero section; connection string via user-secrets / env

AccessControl.Web/
  Features/{Feature}/              Controller, Models/, Views/   (code-style MVC slice, modal forms, live refresh)
```

### 3.5 Rules

1. **Single owner of the driver.** Exactly one `AeroDriverHost` per process. The driver is thread-safe, but initialization (cmd 11), channel creation and shutdown happen once.
2. **Structure-based commands in production code** (`SCPConfig.scpCfgCmndEx(enCfgCmnd, CC_xxx)`). String commands (`SCPDLL.scpConfigCommand`) are allowed for diagnostics/console only. Structure-based is faster and type-checked.
3. **Continuous message loop.** The host must call `SCPReplyMessage.GetMessage()` continuously (≈50 ms idle delay) or the connection is not maintained. The pump never blocks on business logic — it hands replies to an in-process channel; consumers persist/dispatch.
4. **Every reply is persisted before it is processed** (transactions especially) — this is what makes logs survive reader replacement and host restarts.
5. **Command result tracking.** Each posted command gets a tag; correlate `SCPReplyCmndStatus` (OK / NAK) back to the caller. A configuration step is only "done" when its command status is OK.
6. **Hardware IDs are configuration, not identity.** SCP number, ACR number, card-format number, access-level number etc. are allocated by the host and stored against domain entities; domain logic never hard-codes them.
7. **Time.** Controller timestamps are **UTC epoch seconds**. Store as `DateTimeOffset …AtUtc`; convert for display only. Use `TimeProvider` in host code.
8. Native-DLL failures (DllNotFound, BadImageFormat) must fail fast at startup with a clear message (x86/x64 mismatch is the usual cause).
9. Only `AccessControl.Infrastructure.Aero` references the wrapper; only `AccessControl.Gateway` loads it. Exactly one Gateway instance per controller estate (it owns TCP 3001).

### 3.6 Build order

Each step is finished across all layers and builds cleanly before the next (code-style new-system §7). Verification = `dotnet build` + manual checks; no test project.

| # | Step | Delivers | Spec |
|---|---|---|---|
| 0 | Scaffold solution + shared kernel ✅ (2026-11-22). First migration deferred to step 2 — with no entities it would be empty | empty Web + Gateway running | code-style |
| 1 | **Gateway skeleton** ✅ code (2026-11-22), bench pending: driver lifetime (11/12/203), message pump, command writer (tag → CmndStatus/NAK), reply mappers, IP Client listener, acceptance 1107 → 1105 → 1116 → 302 → 303; replies **logged** (tables arrive in step 2). Acceptance is governed by the temporary `Aero:AutoAcceptControllers` flag (true only in Development) until step 4 matches controllers by serial. 203 is the only string command (no wrapper enum entry). | X1100 connects; ID report + comm status in the log | §5 |
| 2 | **Sites & Controllers** in MVC: CRUD, model catalog, internal SIO 0 auto-created, connection mode, serial number; first migration | controller registry (desired state, not yet applied) | §3.7, §4 |
| 3 | **SIO panels, inputs/outputs, readers, doors** in MVC — saved only | full hardware configuration per controller in the DB | §3.7, §6.4, §7.1–7.3 |
| 4 | **Publish + provisioning engine**: publish versions, match/adopt controllers, build & apply the command list, record OK/NAK, `AppliedVersion`, re-apply on reconnect; live status page | the X1100 is configured from the DB | §3.7, §5, §6 |
| 5 | **Card formats** (+ raw 3:1 capture view to discover VL70LF format) | 1102 | §7.4 |
| 6 | **Schedules & Holidays** | 1104 / 3103 | §7.5 |
| 7 | **Access levels** | 2116 | §7.5 |
| 8 | **Cardholders & Credentials** | 8304 / 3305, suspend / activate / replace | §7.6 |
| 9 | **Access events** (live list, filters) | granted/denied, door alarms | §8.2 |
| 10 | **Door commands** (unlock, mode, LED) | 311 / 308 / 315 via outbox | §8.4 |
| 11 | Bench checklist | POC sign-off | §11 |

Steps 2–10 each touch Web + Application + Infrastructure; steps 1, 4–8, 10 also add command builders in Infrastructure.Aero. Steps 5–7 use the same publish pattern as step 4.

### 3.7 Hardware configuration: desired state and publish

SCPs, SIOs, I/O points, readers and doors are **entered by users in the MVC**, saved in the database as the **desired configuration**, and applied to the hardware **by the Gateway** through the SDK. The Web never sends SDK commands.

**Why desired state (not one SDK command per click):** the controller may be offline when the user saves; the SDK requires 1107/1105 to be re-sent after every reconnect (§5.4); half-finished edits must not reach the hardware; the DB must answer "what is on the controller"; a replaced controller must be re-configurable without re-entry.

**Flow (example: first X1100 "Main gate", SCP 1):**

```
MVC (user)                                  DB                                    Gateway (SDK)
1. Site "HQ"                            ─►  Site
2. Controller "Main gate", SCP 1,       ─►  Controller (ScpNumber 1, X1100, IpClient,
   X1100, IP Client, serial 2001843          Serial 2001843, Status NotPublished)
                                             + SIO 0 (internal, model 196) auto-created
3. Name I/O, add VL70LF reader, door 1  ─►  SIO / Inputs / Outputs / Readers / Doors
4. "Publish configuration"              ─►  ConfigVersion 1 (who, when) → Pending
                                                                          ◄─ X1100 connects (ID report, serial)
                                                                             match → "Main gate"
                                                                             1107 → 1105 → 1116 → 302 → 108 → 109(off)
                                                                             → 110… → 111… → 112 → 113/114 → 115
                                                                             → 109(on) → 303
                                        ◄─  per-command OK/NAK, AppliedVersion 1 → InSync
5. Controller page: Online · In sync (v1) · command log
```

**Rules:**

| # | Rule |
|---|---|
| 1 | **Explicit publish.** Editing changes the draft only; "Publish configuration" creates an immutable `ConfigVersion` (number, published by, at). The Gateway applies only published versions. |
| 2 | **Sync status per controller:** `NotPublished` → `Pending` → `Applying` → `InSync` / `Failed` (with the NAK'd command and reason). `PublishedVersion` vs `AppliedVersion` shown in the UI. |
| 3 | **Verify, then reload only what is out of sync** (HID SDK §5.13–5.14). On every connect the Gateway sends 1107/1105 (required to accept the connection and to interpret transactions), then reads the **config marker** it stored in the controller's Remote Memory Storage (**1851**) and the structure allocations (**1853**). Marker = applied `ConfigVersion` + hash. Equal to the published version → nothing to download. Different or missing → download the configuration (§6 order), write the new marker (**1852**), save to flash (**211**). Card data later uses its own marker so only changed cardholders are reloaded. |
| 4 | **Controller matching by serial number** entered by the user (IP Client). An unregistered controller that connects appears as **Pending controller** (serial, model, firmware, MAC from the ID report). It is **not** accepted (no 1107), so the driver detaches it after `provisioning_time`; it retries and stays listed until a user adopts it (links to a record) or rejects it. On adoption the Gateway programs the controller's ID with **318** = the record's SCP number, so the driver assigns that ID on later connections (SDK §14.11 default behaviour). New controllers ship with ID **1**, so the first unconfigured controller may arrive as SCP 1. |
| 4a | **Allocation changes need a reset.** Changing 1107 (capacities, GMT offset, number of SIOs/points/readers…) or 1105 (card record layout) requires **301 Reset SCP** first (SDK §5.15), which **erases the controller's database and its unreported transactions**. These fields are therefore set once at creation; changing them is a separate, explicitly confirmed "Re-initialize controller" action that first collects pending transactions, then 301, then a full download. |
| 4b | **Firmware tracking.** The ID report firmware version is stored; a mismatch with the supported version is flagged in the UI (SDK §5.13). |
| 5 | **Connection mode per controller** (IP Client default / IP Server). IP Server needs the controller IP; on publish the Gateway sends 1013 + 207 itself. |
| 6 | **Model catalog** (Domain): per model the SIO model code and I/O/reader counts — X1100 internal 196: 7 in / 4 out / 2 reader ports (4 reader slots); X100 193: 7/4/2; X200 194: 19/2/0; X300 195: 5/12/0. Creating an X1100 auto-creates SIO 0 with its points. |
| 7 | **Domain validation:** unique SCP number; max 16 SIOs per RS-485 port, unique address per port; point/reader numbers within the model's counts; a door references a reader, lock output, contact and exit inputs on the same controller. |
| 8 | **User-facing vs derived:** users enter names, types (door contact, exit button, lock, aux…), NO/NC, ports, addresses, reader interface (OSDP/Wiegand, address, secure channel), door timings. SDK fields (`osdp_flags`, `icvt_num`, 115 field list, capacities) are **derived** in Infrastructure.Aero; capacities default to §6.3 and sit in an Advanced section. |
| 9 | **Operational commands** (unlock, door mode, LED — step 10) are not configuration: they go through the `ControllerCommands` outbox immediately and are not versioned. |

**Web access (later):** the MVC will be used only by administrators and authorized technicians, with configuration and publish pages restricted to specific people. The design is deferred; pages are built so a server-side authorization check can be added per controller/action without restructuring.

---

## 4. Identifier plan

| Object | Value for POC | Rule |
|---|---|---|
| SCP ID (`nSCPId`) | **1** (or ID reported by controller in IP Client mode) | Host-allocated, unique across the whole estate. Stored on `Controller`. |
| Host channel (IP Client) | **44**, port **3001** | One listening channel for all controllers. |
| Host channel (IP Server) | **257** | One channel per controller; `nPorts` must equal `nScps` in cmd 11. |
| MSP1 driver 0 | port **3** (internal) | X1100 internal SIO. |
| MSP1 driver 1 / 2 | port **1** / **2** (RS-485) | Reserved for X100/X200/X300 later (16 SIOs each). |
| SIO 0 | X1100 internal, model **196** | 7 inputs, 4 outputs, 2 reader ports (configure 4 reader slots as in guide). |
| Reader 0 / 1 on SIO 0 | VL70LF **IN** / VL70LF **OUT** | Reader ports 1/2 on the board. |
| ACR 0 / 1 | Door 1 entry / exit | Paired if in/out on the same door. |
| Card format 0..7 | Defined per actual card bit-layout | Max **8 formats per ACR** (`cd_format` bitmap). |
| Timezone 1 | 24×7 | Timezone 0 is reserved ("none"). |
| Access level 1.. | Host-allocated | Access level 0 = none. |

---

## 5. Establishing communication

### 5.1 Connection mode decision

**Target: IP Client mode with TLS** (controller dials the host). Rationale: multi-site rollout to a central host, one listening channel, NAT/firewall friendly. **IP Server mode** is supported for lab/bench use.

### 5.2 Controller preparation (Configuration Manager)

| Setting | IP Client (target) | IP Server (lab) |
|---|---|---|
| Connection Type | IP Client | IP Server |
| Data Security | TLS if Available (POC) → TLS Required (prod) | TLS if Available |
| Host IP | host application IP | — |
| Port | 3001 | 3001 |
| Enable Peer Certificate | unchecked (POC) | — |

Accept → **Apply Settings, Reboot**.

### 5.3 Startup sequence — IP Client

| # | Command | Purpose | POC values |
|---|---|---|---|
**Driver start (once per Gateway start):**

| # | Command | Purpose | POC values |
|---|---|---|---|
| 1 | **11** System Level Specification | Allocate driver structures | `11 512 512 0 0 1 0 0 0 0 0` |
| 2 | **018** Set IP Client Defaults *(optional)* | `e_max`, `poll_delay`, `offline_time`, **`provisioning_time`** = how long the driver waits for 1107 before detaching (default 15 s). Send right after 11. | defaults: `3 5000 15000 15000` |
| 3 | **019** Set IP Client ID Assignment *(optional, once per driver start)* | How SCP IDs are assigned to incoming controllers. Default: use the ID programmed in the controller (cmd 318); if taken, first free ID from 1. Range must exceed `nScps` of cmd 11. | not sent for POC (default behaviour) |
| 4 | **12** Create Channel | Listen for controllers | `012 44 7 3001 0 3000 10000 "" 0` |

IP Client `cType` (SDK §5.3): **7** TLS if the controller accepts it (POC) · **263** TLS required · **519** + certificate verification · **775** TLS required + certificate verification · **1287** mutual TLS. (4/260/516/772/1284 are the **IP Server** equivalents.) `timer1` = 3000 ms is HID's recommendation for TCP/IP.

**Per controller connection:**

| # | Command | Purpose | Notes |
|---|---|---|---|
| 1 | — wait — | `SCPReplyCommStatus` + `SCPReplyIDReport` | The driver assigns `nSCPId` (ID programmed in the controller, else first free). **Never send 1013 in IP Client mode** — it tells the driver to stop using that ID for IP Client connections. |
| 2 | **1107** SCP Device Spec | **Accept** the controller — within `provisioning_time` (15 s) or the driver detaches it | §6.3 values. Unknown controllers are **not** accepted (§3.7 rule 4). |
| 3 | **1105** Access DB Spec | Must be the **next** command after 1107 | §6.3 |
| 4 | **1116** Daylight Saving | **Must precede the first 302 on every new IP Client connection** | Saudi: no DST entries needed, but the command is still sent |
| 5 | **302** Time Set | Sync controller clock; also downloads the DST table | Log the controller-vs-host clock difference from the ID report first (SDK §5.12) |
| 6 | **1853** / **1851** verify | Read structure allocations and the config marker in Remote Memory Storage | §3.7 rule 3 |
| 7 | configuration (if out of sync) | §6 order, then **1852** to write the new config marker, then **211** `4` / `5` to save host and card databases to flash | |
| 8 | **303** Transaction Index | Transaction reporting is **always disabled** on IP Client connect — enable after verification | `303 <scp> -2` (resume from last reported) |

### 5.4 Startup sequence — IP Server

| # | Command | POC values |
|---|---|---|
| 1 | **11** | `11 512 512 0 0 1 0 0 0 0 0` (`nPorts` = `nScps`) |
| 2 | **12** Create Channel | `012 257 4 0 0 3000 15000 "" 0` |
| 3 | **1013** Create SCP | `1013 <scp> 0 260 3 5000 "<controller-ip>" "" 15000` — **260** = connect with transactions disabled until verified (SDK §5.11 recommendation; 4 = report immediately) |
| 4 | **1116**, **1107**, **1105** | **Before** 207 (SDK §5.11). Failures while offline are expected; they allocate the host-side structures needed to interpret transactions. 1107/1105 are sent again after connection. |
| 5 | **207** Attach SCP | `0207 <scp> 257 0` |
| 6 | — wait — | Comm Status + ID Report → then the same steps 2–8 as IP Client (1107, 1105, 302, verify, configure if needed, 303) |

### 5.5 Connection validation

On `SCPReplyIDReport`, record and log: device ID/model, firmware, serial, MAC, `db_max`, `db_active`, `tls_status`, DIP switch state, `scp_id`. Reject/alert if DIP switches are not all off or `tls_status` does not meet policy.

### 5.6 Reconnect behaviour

- Comm status changes arrive as local driver messages (`enSCPReplyCommStatus`) and as `TypeSys` transactions (1:2 host offline, 1:3 host online).
- On reconnect: 1107 → 1105 → 1116 → 302, then **verify** (SDK §5.13–5.14): ID report (serial, firmware, `db_active` vs host card count) + **1853** structure allocations + **1851** config marker in Remote Memory Storage. Reload only what is out of sync (§3.7 rule 3). Then **303 -2**: the controller resumes from the last *reported* transaction; the host still de-duplicates on `(ControllerId, TransactionSerial)`.
- The controller keeps its configuration across power cycles when Auto-Save is ON and DIP switches are off (§2); the Gateway additionally issues **211** after a configuration download.

---

## 6. Configuring the controller

### 6.1 Order (from SDK spec §5.15 and Getting Started ch. 04/05)

```
1107 SCP device spec → 1105 cardholder DB spec → 1116 DST → 302 time
→ 108 MSP1 driver(s) → 109 SIO (enable = OFF) → 110 inputs → 111 outputs → 112 readers
→ 113 monitor points → 114 control points → 115 ACRs → 109 SIO again (enable = ON)
→ 1102 card formats → 1104 holidays → 3103 time zones → 2116 access levels → 8304 cards
→ 1852 write config marker (RMS) → 211 save host (4) and card (5) databases to flash
→ 401/403/404/407 status checks → 303 enable transactions (if disabled)
```

Sending 109 first with `enable` off and again after 115 with `enable` on avoids spurious change-of-state transactions (SDK spec §5.15).

### 6.2 Time

- Saudi Arabia: **UTC+03:00, no DST** → `gmt_offset` = `-1 × (3 × 3600)` = **-10800**, `nDstID` = **0**.
- Send **302** (`302 <scp>`, host UTC) on every (re)connect and daily.

### 6.3 SCP and cardholder database (POC values)

**1107 — SCP Device Specification**

| Field | Value | Why |
|---|---|---|
| nMsp1Port | 3 | internal + 2 RS-485 |
| nTransactions | **60000** | HID recommends ≥ 60000 to maximize memory (≈ 58,000 actually stored); covers "~50,000 events offline" (MoM) |
| nSio | 32 | |
| nMp / nCp | 615 / 388 | X1100 max |
| nAcr | 64 | X1100 max |
| nAlvl | 255 | |
| nTrgr / nProc | 1000 / 1000 | |
| gmt_offset | **-10800** | UTC+3 |
| nDstID | 0 | no DST |
| nTz / nHol | 255 / 100 | |
| nTranLimit | 10000 | unreported transactions |

**1105 — Access Database Specification**

| Field | Value | Why |
|---|---|---|
| nCards | 100000 | Verify against record size with HID Card Size Calculator (`HID Doc/Extracted/Tools/Tools/hidaerocardsizecalculator_1_0_0_6`) |
| nAlvl (per card) | **32** | Removes IDCube's 8-groups-per-card limit |
| Card number bytes / PIN digits | 4 / 4 (encoded `nPinDigits` = 68) | Revisit once VL70LF credential length is known (§12) |
| Issue code | yes | lost-card replacement |
| Activation / deactivation date | 2 (date + time) | handover activation, expiry, suspension |
| Use limit | yes | visitor cards (later) |
| Host response timeout | 5 s | |
| Exception list | yes | |

### 6.4 Hardware objects (POC)

```
# MSP1 driver 0 → internal SIO
108 0 <scp> 0 3 0 90 0 0
# SIO 0 = X1100 internal (model 196), first pass disabled
109 0 <scp> 0 7 4 4 196 0 0 -1 0 0 0 0 0 3 0
# Inputs: 0 door contact (NC), 1 REX (NO), 4 tamper, 5 AC fail, 6 battery fail
110 0 <scp> 0 0 0 4 0
110 0 <scp> 0 1 1 4 0
# … per wiring
# Outputs: 0 door strike
111 0 <scp> 0 0 0
```

Final input/output assignment follows the actual door wiring at the SANS test bench — record it in §4 when known.

---

## 7. VL70LF reader and door

### 7.1 Reader specification — Command 112

**Target (OSDP):**

| Field | Value | Meaning |
|---|---|---|
| dt_fmt | 0 | Not used for OSDP (Wiegand uses 1 = D1/D0) |
| keypad_mode | 0 | Set per VL70LF keypad support |
| led_drive_mode | **7** (`IDRDR_L_OSDP`) | Enable OSDP device interface |
| osdp_flags | bits 0–2 baud, bit 3 no-discover, bit 4 tracing, bits 5–6 address, bit 7 Secure Channel | `0` = defaults (9600 baud, address 0) |

```
# VL70LF IN on SIO 0 reader 0, OSDP defaults
112 0 <scp> 0 0 0 0 7 0
# VL70LF OUT on SIO 0 reader 1, OSDP, 9600, address 1, Secure Channel → 1 | (1<<5) | 0x80 = 161
112 0 <scp> 0 1 0 0 7 161
```

Target for production: **OSDP Secure Channel ON** (bit 7). For first bring-up use defaults + tracing (bit 4) to diagnose.

**Fallback (Wiegand):** `112 0 <scp> 0 0 1 <keypad> 1 0` (`dt_fmt` 1, LED mode 1 bicolor).

### 7.2 LED / buzzer

- **122** reader LED/buzzer function specs, **315** reader LED mode, **325** temporary LED control (e.g. visitor feedback).
- **337** OSDP passthrough for VL70LF-specific OSDP commands if needed.

### 7.3 ACR — Command 115 (door)

| Field | POC value | Notes |
|---|---|---|
| access_cfg | 0 single / paired for IN+OUT | |
| rdr_sio / rdr_number | 0 / 0 (IN), 0 / 1 (OUT) | |
| strk_sio / strk_number | 0 / 0 | door strike relay |
| strike_t_min / max | 1 / 5 s | |
| strike_mode | 1 (`ACR_S_OPEN`) | release on door open |
| door_sio / door_number | 0 / 0 | door contact |
| **dc_held** | **300–600** | Door-held-open alert 5–10 min (MoM). Confirm unit/range in SDK spec §8.2. |
| rex0 | SIO 0 input 1 | |
| cd_format | bitmap of defined formats | |
| default_mode | 5 card only (POC) | 8 = card or PIN if VL70LF has keypad |
| offline_mode | per security decision (§12) | MoM: local activation during outages is open |

### 7.4 Card formats — Command 1102

The VL70LF output format must be **discovered**, not assumed:

1. Before any format is defined, present each SANS sample card → `TypeCardBin` **3:1** transaction (raw bits + bit count).
2. Derive layout (length, facility code, card number, parity) and define with **1102**. Baseline example — H10301 26-bit, facility 99:
   `1102 0 <scp> 0 99 0 1 0 26 13 0 13 13 8 1 16 9 0 0`
3. Present again → expect **5:5** ("not in card file") with decoded facility + card number.

Max 8 formats per ACR; each facility code is a separate format.

### 7.5 Schedules and access levels

- **1104** holidays (type mask), **3103** time zones (1–12 intervals, day mask includes holiday bits), **2116** access levels (`tz[acr]` = timezone, 0 = no access).
- POC: TZ 1 = 24×7; AL 1 = ACR 0 + ACR 1 on TZ 1.

### 7.6 Credentials

- **8304** add/update card (requires firmware ≥ 1.30.0 and driver ≥ 4.7.1.32; otherwise 5304). Fields follow the 1105 layout.
- **3305** delete card, **1709** read card record, **3323** use limit, **343** card cleanup.
- Suspension = clear active flag or set deactivation time; replacement = new issue code (one active card per employee — MoM).

---

## 8. Communicating with the controller

### 8.1 Message loop

`SCPReplyMessage.GetMessage()` → `ReplyType`:

| ReplyType | Meaning | Handling |
|---|---|---|
| 2 CommStatus | driver-local connect/disconnect | update controller online state |
| 3 NAK | command rejected | fail the tagged command, log NAK reason |
| 4 IDReport | controller identity | §5.5 |
| 6 TranStatus | transaction log status | resume bookkeeping |
| 7 Transaction | event from controller | persist → dispatch |
| CmndStatus | OK/fail for a posted command | complete the tagged command |
| Sr* (status replies) | responses to 40x requests | update live state |

Persist the last processed transaction **serial number** per controller; use **303** to resume after reconnect. Use **214** demand-poll mode if user commands are starved during large backlogs.

### 8.2 Transactions the POC must handle

Notation `[type:code]` (SDK spec §11.10).

| Type | Name | Use |
|---|---|---|
| 1 | TypeSys | host comm online/offline (1:2 / 1:3), diagnostics |
| 2 | TypeSioComm | SIO online/offline |
| 3 | TypeCardBin | raw card data (format discovery, 3:1 invalid format) |
| 5 | TypeCardFull | decoded card, access denied reasons (5:5 not in card file …) |
| 6 | TypeCardID | access granted/denied with cardholder (6:1 deactivated card …) |
| 7 | TypeCoS | monitor / control point / reader-tamper change of state |
| 8 | TypeRex | REX and host-initiated unlock (8:2, 8:5) |
| 9 | TypeCoSDoor | door forced (accesspoint_status 0x04), held open (0x10) |
| 12 | TypeActivate | time zone active/inactive |
| 126 | TypeAscii | controller diagnostic text |

Unknown types are persisted raw and logged, never dropped.

### 8.3 Status requests

| Cmd | Purpose |
|---|---|
| 401 | ID report |
| 402 | transaction log status |
| 403 | SIO comm drivers (MSP1) |
| 404 | SIO status (`com_status` 5 = online) |
| 405 / 406 | monitor / control point status |
| 407 | ACR status incl. reader online/offline and OSDP reader identity (vendor code, model, serial, firmware) — used to detect "online but not reading" |
| 408 | time zone status |

### 8.4 Control commands

| Cmd | Purpose |
|---|---|
| 311 | momentary unlock (remote door open) |
| 307 | control point on/off/pulse |
| 308 / 334 | ACR mode (locked, card only, card+PIN …) / temporary mode |
| 309 / 310 | forced-open / held-open mask |
| 329 | host response (host-decided access, if used) |
| 331 / 342 | host-simulated card read (testing without a physical card) |

---

## 9. Data persistence (host)

**Development database:** local SQL Server default instance (`localhost`, MSSQLSERVER), Windows Authentication, database **`AccessControl_poc`**. Connection string key `ConnectionStrings:AccessControl`, supplied via user-secrets in Web and Gateway — never committed. (User decision, overrides the standards' "SQL Server in a container" default.)

Minimum tables for the POC: `Sites`, `Controllers` (+ connection mode, serial, status, last ID report, `PublishedVersion` / `AppliedVersion` / sync status), `ControllerConfigVersions` (published by / at), `PendingControllers`, `SioPanels`, `InputPoints`, `OutputPoints`, `Doors`, `Readers`, `CardFormats`, `Schedules`, `Holidays`, `AccessLevels`, `Cardholders`, `Credentials`, `AccessEvents` (raw + decoded transaction, controller serial number, UTC time, received-at; unique `(ControllerId, TransactionSerial)`), `ControllerCommands` (outbox: intent, status Pending/Sent/Succeeded/Failed, driver tag, NAK reason, attempts). All follow the auditing/soft-delete/rowversion rules in code-style. `AccessEvents` are append-only and keyed by host IDs so replacing a reader does not lose history.

---

## 10. Security

- TLS on host ↔ controller (IP Client cmd 12 `cType` 7 for POC; 263 / 775 / 1287 for production — §5.3).
- OSDP Secure Channel between controller and VL70LF in production.
- SIO ↔ SCP traffic is AES-encrypted by default.
- Change the controller's default web credentials; never commit controller passwords, IPs of production sites, or certificates. Use user-secrets / environment variables (`Aero__…`).
- The HID SDK specification is **HID confidential (NDA)**: do not publish it, its text, or the driver package outside the project.

---

## 11. Bench verification checklist

| # | Check | Expected |
|---|---|---|
| 1 | Driver loads, cmd 11 OK | no exception, CmndStatus OK |
| 2 | Controller connects | CommStatus 2 "Communication OK", ID report logged |
| 3 | 1107 within 15 s (IP Client) | connection stays up past 15 s |
| 4 | 404 | SIO 0 `com_status` 5 |
| 5 | 407 | ACR 0/1 configured, VL70LF online, OSDP identity shown |
| 6 | Unknown card | 3:1 (no format) → after 1102: 5:5 |
| 7 | Enrolled card | 6:x granted, strike pulses, door transactions |
| 8 | 311 | 8:5 host-initiated unlock |
| 9 | Door held open > `dc_held` | 9:x with held-open status |
| 10 | Unplug network 5 min, swipe cards, reconnect | buffered transactions received once, in order |
| 11 | Reboot controller | host reconnects and reconfigures without manual action |

Tools for cross-checking: HID Aero Panel Tester and Command Decoder (`HID Doc/Extracted/Tools/Tools`), AeroChat sample.

---

## 12. Open items

| # | Item | Owner |
|---|---|---|
| 1 | **VL70LF** datasheet: OSDP or Wiegand, OSDP address/baud/Secure Channel support, keypad, LED/buzzer, card technologies and output format | SANS / NERA |
| 2 | Controller variant and firmware (X1100A vs X1100C; ≥ 1.30.0 for cmd 8304) | NERA on receipt |
| 3 | Bench wiring: which inputs/outputs are door contact, REX, strike | NERA |
| 4 | Sample cards and their bit layouts / facility codes | SANS |
| 5 | **Password for `HID_aero_4_8_1_3.zip`** (all inner files encrypted) → run `tools/AeroSdkCompat` with `-p:AeroSdkVersion=4.8.1.3`; standardize on 4.8.1.3 if it passes | NERA / HID |
| 9 | Run the probe against the real X1100 (`--listen --accept`) to confirm reply decoding on .NET 10 | NERA on receipt |
| 10 | Product / solution name (placeholder `AccessControl`) | NERA |
| 11 | Gateway hosting: which Windows server, service account, firewall rule for TCP 3001 | NERA / SANS IT |
| 6 | Offline mode policy (`offline_mode`) and local activation during outages | SANS Security |
| 7 | Exact `dc_held` value (5 or 10 min) | SANS Security |
| 8 | IP Client TLS certificate strategy for production (263 / 775 / 1287 mutual TLS, peer certificate) | NERA / SANS IT |
| 13 | Bench-verify **1116 with no DST**: the Gateway sends one past entry (2000-01-01 → 2000-01-02) because HID requires 1116 before the first 302 on every IP Client connection; confirm the controller returns OK (a NAK is logged and the sequence continues) | NERA on receipt |
| 12 | Bench-verify the SCP ID flow: after **318** on an adopted controller, does the driver use the new ID only after reconnect (detach 208 → reconnect)? | NERA on receipt |

---

## 13. Reference index

| Topic | File |
|---|---|
| System overview | `HID Doc/Extracted/Docs/Docs/Aero_Getting_Started_Guide_v1_2/01 Intro.md` |
| Driver usage, message loop | `…/02 Using the driver.md` |
| IP Server / IP Client connection | `…/03 Establishing Connection.md` |
| Hardware init sequence + test logs | `…/04 Initializing the hardware.md` |
| Formats, holidays, TZ, access levels, cards | `…/05 Access Control Configuration.md` |
| Full command & transaction reference (NDA) | `HID Doc/Extracted/Docs/Docs/PLT-04238_SDK-Specification.pdf` — §5.9–5.17 startup, §6.7 cmd 112, §8.2 cmd 115, §10 cardholder DB, §11.10 transactions |
| X1100 wiring / DIP / LEDs | `HID Doc/Extracted/Docs/Docs/Installation-Guides/PLT-04233…X1100…pdf`, `PLT-08293…X1100C…pdf` |
| C# sample (structure-based) | `HID Doc/Extracted/Code Samples/Code Samples/hidaerosamplecs_1_0_0_2/` |
| C# sample (console, OSDP reader) | `HID Doc/Extracted/Code Samples/Code Samples/aerochat1_4_15/` (`OsdpReader.cs`, `Description.cs`, `StatusResponses.cs`) |
| Workshop decisions | `HID Doc/Extracted/MoM_Access_Visitor_Workshop_2026-11-12.docx`, `MoM_Attendance_Workshop_2026-11-11.docx` |
| House code style | `.claude/skills/code-style/SKILL.md` |
| SDK DLLs used by the build | `AccessControll_App_poc/lib/HIDAero/<sdk-version>/` |
| Engineering standards (planning / implementation / review baseline) | `dotnet10-engineering-standards/` (CORE, FEATURE-PATTERN, README) |
| .NET 10 compatibility probe | `tools/AeroSdkCompat/` |
