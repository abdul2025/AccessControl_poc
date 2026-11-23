# Plan — Step 3: SIO panels, I/O points, readers and doors

> Status: **implemented** — 3a and 3b done 2026-11-23 (decision on Tamper / AC / Battery: `tamper-ac-battery-inputs.md`) · spec reference: `docs/PROJECT_SPEC.md` §3.6 step 3, §3.7, §4, §6.4, §7.1–7.3
> Plan only — no code changed yet.

## 1. Outcome and acceptance criteria

**Outcome:** on a controller's page, an administrator manages its hardware layout — **SIO panels**, their **inputs and
outputs**, **readers** (VL70LF) and **doors** — saved in `AccessControl_poc` as desired configuration. No SDK commands
(the Gateway turns this into 108–115 in step 4).

Delivered in **two milestones**, each with its own migration, build and checks:

- **3a** — SIO panels and I/O points
- **3b** — readers and doors

### 3a — SIO panels and I/O points

1. **Add an external SIO** (X100 / X200 / X300) to a controller: RS-485 port 1 or 2, address 0–15. Unique number and
   address per port, max 16 per port, max 32 SIOs per controller (1107 `nSio`). The **internal SIO 0** cannot be
   removed or change port/model.
2. Every SIO automatically gets **all its physical points** from the hardware catalog, named as on the board:
   - X1100 internal / X100: inputs IN 1–4, Tamper, AC fail, Battery fail · outputs Relay 1–4
   - X200: inputs IN 1–16 + Tamper / AC fail / Battery fail · outputs Relay 1–2
   - X300: inputs IN 1–2 + Tamper / AC fail / Battery fail · outputs Relay 1–12
3. **Edit an input:** name, contact (normally closed / normally open), supervised (end-of-line resistors yes/no),
   *monitored* (raises events → monitor point, cmd 113). Tamper / AC fail / Battery fail are monitored by default.
4. **Edit an output:** name, pulse time in seconds (default 1; used for auxiliary control points, cmd 114).
5. **Rename an SIO.** **Remove an external SIO** — refused while a reader or door uses it.

### 3b — readers and doors

6. **Readers** — add / edit / remove:
   - SIO, reader number (0 … model's reader slots − 1), name, model (default `VL70LF`, free text)
   - **Interface:** OSDP (default) or Wiegand
   - OSDP: address 0–3, baud rate (9600 default, 19200, 38400, 57600, 115200, 230400), secure channel on/off
   - Wiegand: keypad yes/no
   - Reader number unique per SIO; name unique per controller
7. **Doors** (ACRs) — add / edit / remove:
   - Door number 0–63 (unique per controller, next free suggested), name
   - Entry reader (required), exit reader (optional)
   - Lock output (required), door contact input (optional), exit button input (optional)
   - Lock time min / max seconds (default 1 / 5), held-open alarm seconds (default 300)
   - Access mode: Card only (default), Card or PIN, Card and PIN, Locked, Unlocked
8. **Door rules:**
   - every reader / point used belongs to the same controller
   - a reader, lock output or input is used by **one door only**; entry and exit readers differ
   - door contact and exit button are different inputs and cannot be Tamper / AC fail / Battery fail
9. **Controller details page** shows each SIO with its inputs and outputs (and "used by Door x"), a **Readers** table
   and a **Doors** table, each with add / edit / remove.
10. Every edit is guarded by the **controller's row version** — a stale edit shows the concurrency message.
11. `dotnet build` 0 warnings; both migrations reviewed with you before being applied.

**Out of scope:** card formats, schedules, access levels (steps 5–7); publishing / SDK (step 4); elevator,
anti-passback; custom end-of-line tables (cmd 1101); debounce / hold-time tuning (SDK defaults).

## 2. Current state and evidence

| Evidence | Finding |
|---|---|
| `Domain/Entities/Scp.cs` | Aggregate root with `_sioPanels`; `Create` adds SIO 0 via `SioPanel.CreateInternal`; no child mutators yet. |
| `Domain/Entities/SioPanel.cs` | `Number`, `Model`, `Port`, `Address`, `Name`; only `internal static CreateInternal`. |
| `Domain/Catalog/HardwareCatalog.cs` | `SioModelSpec(Inputs, Outputs, ReaderPorts)` for 196/193/194/195; `MaxSiosPerRs485Port = 16`. No point labels or reader slots. |
| X1100 install guide PLT-04233 | "Up to 4 Readers, 7 Inputs, 4 Outputs"; terminals READERS 1–2, INPUTS 1–4 + TAMPER / AC FAIL / BATT FAIL (all NO/NC, supervised or not), RELAYS 1–4. |
| Getting Started ch. 04 | Input numbering: X1100/X100 0–3 = IN 1–4, 4 Tamper, 5 AC fail, 6 Batt fail; X200 0–15 + 16/17/18; X300 0–1 + 2/3/4; `nReaders = 4` for X1100/X100. |
| SDK §6.5 / §6.7 / §8.2 (spec §7.1, §7.3) | 110 `icvt_num` (NC/NO, supervised); 112 `led_drive_mode` 7 = OSDP, `osdp_flags` baud (bits 0–2), **address 0–3** (bits 5–6), secure channel (bit 7), Wiegand `dt_fmt` 1 + keypad; 115 reader, strike output, door sensor, REX, `strike_t_min/max`, `dc_held`, `default_mode` (1 disabled … 5 card only … 8 card or PIN). |
| `Infrastructure/.../ScpConfiguration.cs`, `ScpRepository.cs` | SIO children with field access and cascade; `GetForUpdateAsync` includes SIOs; `UpdateAsync` marks the root Modified (concurrency for child changes); soft delete keeps child rows. |
| `Web/Features/Scps/ScpsController.cs`, `Views/Details.cshtml` | Details shows SIO table read-only; no child actions yet. |
| code-style `patterns.md` §1 | Children `: BaseEntity`, `internal` factory, changed only through the root; child actions on the parent controller; root row version guards every child write. |
| Spec open items 1 / 3 / 6 / 7 | VL70LF interface, wiring, offline mode, `dc_held` not decided → **selectable or defaulted, not hard-coded**. |

## 3. Design

### Domain — everything inside the `Scp` aggregate (configuration is published per controller)

| Type | Content |
|---|---|
| `Catalog/HardwareCatalog.cs` (extended) | `SioModelSpec` + `ReaderSlots` (4 / 4 / 0 / 0), input labels, output labels, dedicated inputs (Tamper / AcFail / BatteryFail); `MaxSiosPerScp = 32`, `MaxDoorsPerScp = 64`. |
| `SioPanel` (extended) | `Inputs`, `Outputs` lists; `CreateInternal` / new `CreateExternal` create all points from the catalog; internal `Rename`, `UpdateInput`, `UpdateOutput`. |
| `InputPoint : BaseEntity` | `Number`, `Name`, `Contact` (NormallyClosed / NormallyOpen), `Supervised`, `Monitored`, `Dedicated` (None / Tamper / AcFail / BatteryFail — from catalog, read-only). |
| `OutputPoint : BaseEntity` | `Number`, `Name`, `PulseSeconds` (1–255). |
| `Reader : BaseEntity` | `SioPanelId`, `Number`, `Name`, `Model` (default "VL70LF"), `Interface` (Osdp / Wiegand), `OsdpAddress` 0–3, `OsdpBaudRate`, `SecureChannel`, `HasKeypad`. |
| `Door : BaseEntity` | `Number` 0–63, `Name`, `EntryReaderId`, `ExitReaderId?`, `LockOutputId`, `DoorContactInputId?`, `ExitButtonInputId?`, `StrikeMinSeconds`, `StrikeMaxSeconds`, `HeldOpenSeconds`, `AccessMode` (values = SDK modes: Disabled 1, Unlocked 2, Locked 3, FacilityCodeOnly 4, CardOnly 5, PinOnly 6, CardAndPin 7, CardOrPin 8). |
| `Scp` (new methods, all return `Result`) | `AddSio`, `RenameSio`, `RemoveSio`, `UpdateInput`, `UpdateOutput`, `AddReader`, `UpdateReader`, `RemoveReader`, `AddDoor`, `UpdateDoor`, `RemoveDoor`, `NextFreeSioNumber`, `NextFreeDoorNumber`. All rules from section 1 live here. |
| `Enums/Scps/` | `InputContact`, `DedicatedInput`, `ReaderInterface`, `OsdpBaudRate` (SDK bit values 1–6), `DoorAccessMode` (SDK mode values). |

### Application — `Features/Scps/` (same feature)

- `IScpService` + `AddSioAsync`, `UpdateSioAsync`, `RemoveSioAsync`, `UpdateInputAsync`, `UpdateOutputAsync`,
  `AddReaderAsync`, `UpdateReaderAsync`, `RemoveReaderAsync`, `AddDoorAsync`, `UpdateDoorAsync`, `RemoveDoorAsync`
  → `Result`. Every write DTO carries the **controller's RowVersion**.
- `ScpDto` + inputs / outputs per SIO (with "used by door"), `Readers` (`ReaderDto`), `Doors` (`DoorDto` with names of
  the referenced reader and points).
- `ScpErrors` + `Scp.SioNotFound`, `Scp.SioInUse`, `Scp.ReaderInUse`, `Scp.PointInUse`, `Scp.DuplicateSioAddress`,
  `Scp.DuplicateDoorNumber`, ….

### Infrastructure

| Table | Keys / relationships |
|---|---|
| `InputPoints`, `OutputPoints` | FK `SioPanelId` cascade; unique `(SioPanelId, Number)` |
| `Readers` | FK `ScpId` cascade, FK `SioPanelId` **NoAction**; unique `(SioPanelId, Number)`, `(ScpId, Name)` |
| `Doors` | FK `ScpId` cascade; FKs to reader / point tables **NoAction** (avoids SQL Server "multiple cascade paths"); unique `(ScpId, Number)`, `(ScpId, Name)`; filtered unique on `EntryReaderId`, `LockOutputId`, `DoorContactInputId`, `ExitButtonInputId` |

- Repository: `GetForUpdateAsync` loads the whole aggregate with `AsSplitQuery()`; `GetByIdAsync` projects all DTOs;
  soft delete keeps all child rows.
- Migrations: **3a `AddSioPoints`** (+ backfill of default points for any existing SIO rows), **3b `AddReadersAndDoors`**.

### Web — child actions on `ScpsController` (code-style pattern 1)

- Actions (GET + POST): `AddSio`, `EditSio`, `RemoveSio`, `EditInput`, `EditOutput`, `AddReader`, `EditReader`,
  `RemoveReader`, `AddDoor`, `EditDoor`, `RemoveDoor` — modal forms with full-page fallback.
- Controller details page:
  - **SIO panels** — one card per SIO with Inputs and Outputs tables (name, NC/NO, supervised, monitored,
    "used by Door 1 (contact)", Edit) + **Add SIO**
  - **Readers** table + **Add reader**
  - **Doors** table (reader → lock / contact / exit button, mode, timings) + **Add door**
- Dropdowns list only valid, free points / readers (re-checked on the server).
- `scp-form.js`: OSDP fields for OSDP, keypad for Wiegand.

### Effects

| Area | Effect |
|---|---|
| Data | Two additive migrations; NoAction FKs for door references; backfill only if SIO rows exist. |
| Concurrency | Every child write checks / bumps the controller row version. |
| Gateway | Unchanged now; step 4 translates these records to 108–115 (`osdp_flags`, `icvt_num` derived in `Infrastructure.Aero`). |
| Security | Anti-forgery global; authorization still deferred. |

## 4. Implementation steps

| # | Location (under `AccessControll_App_poc/src/`) | Change | Done when |
|---|---|---|---|
| **3a.1** | `AccessControl_poc` (read-only) | Count Scps / SioPanels rows → backfill needed? | known |
| 3a.2 | `Domain/Catalog/HardwareCatalog.cs` | Reader slots, point labels, dedicated inputs, `MaxSiosPerScp` | builds |
| 3a.3 | `Domain/Enums/Scps/*`, `Entities/InputPoint.cs`, `OutputPoint.cs`, `SioPanel.cs` | Point entities; SIO creates its points | builds |
| 3a.4 | `Domain/Entities/Scp.cs` | SIO and point methods + rules | builds |
| 3a.5 | `Application/Features/Scps/**` | DTOs, errors, service methods | builds |
| 3a.6 | `Infrastructure/Persistence/Features/Scps/*` | Configurations, repository includes / projections | builds |
| 3a.7 | `Migrations/…AddSioPoints` | Generate → **review with you** → backfill if needed → apply after OK | applied |
| 3a.8 | `Web/Features/Scps/**`, `Models/ScpLabels.cs`, `site.css` | SIO / point actions, views, details cards | pages work |
| 3a.9 | — | Build + checks → **you try it in the browser** | 3a done |
| **3b.1** | `Domain/Enums/Scps/*`, `Entities/Reader.cs`, `Door.cs` | Entities with validation | builds |
| 3b.2 | `Domain/Entities/Scp.cs` | Reader / door methods + door rules + remove guards | builds |
| 3b.3 | `Application/Features/Scps/**` | DTOs, errors, service methods | builds |
| 3b.4 | `Infrastructure/Persistence/Features/Scps/*` | Configurations (NoAction FKs, filtered unique indexes) | builds |
| 3b.5 | `Migrations/…AddReadersAndDoors` | Generate → **review with you** → apply after OK | applied |
| 3b.6 | `Web/Features/Scps/**`, `wwwroot/js/scp-form.js` | Reader / door actions, views, OSDP / Wiegand toggle | pages work |
| 3b.7 | `docs/PROJECT_SPEC.md`, `README.md` | Step 3 done, table names, new open items 14 / 15 | docs match |

## 5. Verification

1. `dotnet build AccessControl.slnx` — 0 warnings after 3a and after 3b.
2. Migration review each time (tables, FK delete behaviours, unique / filtered indexes, backfill) before applying.
3. **3a checks** (running Web, real form posts):
   - new controller → SIO 0 has 7 inputs (IN 1–4, Tamper, AC fail, Battery fail) and 4 outputs (Relay 1–4)
   - add X200 on RS-485 port 1 address 1 → SIO 1 with 19 inputs, 2 outputs
   - duplicate address on the same port → 409; address 16 → 422; removing SIO 0 → refused
   - edit IN 1 (name "Door 1 contact", NC, supervised) → shown on details; stale edit → 409
4. **3b checks:**
   - add reader "Main gate IN" (SIO 0, reader 0, OSDP, address 0, 9600) and "Main gate OUT" (reader 1, address 1)
   - add Door 0 "Main gate": entry IN, exit OUT, lock Relay 1, contact IN 1, exit button IN 2, Card only
   - second door using Relay 1 → field error; Tamper as door contact → refused
   - removing a reader used by a door → refused; remove door, then reader → OK; removing an SIO used by a reader → refused
   - Wiegand shows keypad and hides OSDP fields
   - soft-deleting the controller keeps all child rows

No check has been run yet.

## 6. Risks, assumptions, open decisions

| Type | Item |
|---|---|
| **Assumption** | Reader number ↔ terminal: reader 0 = READER 1, reader 1 = READER 2 (spec §4). Slots 2–3 allowed (`nReaders = 4`) but their physical meaning is unconfirmed → **new open item 14** (bench). |
| **Assumption** | Entry + exit readers on one door are stored as one door with an optional exit reader. Mapping to SDK ACRs (two paired ACRs vs one ACR + alternate reader) is decided and verified in step 4 → **new open item 15**. The data supports both. |
| **Assumption** | Held-open default 300 s, editable 1–3600; unit / range of `dc_held` and SANS's 5 vs 10 min stay open item 7. |
| **Assumption** | Offline mode not exposed yet (open item 6); step 4 uses the spec default. Debounce / hold time use SDK defaults. |
| **Risk** | The `Scp` aggregate grows (≈ 30–600 point rows per controller) — fine at POC scale; `AsSplitQuery` avoids cartesian explosion. |
| **Risk** | Many actions on one MVC controller (~22) — accepted; follows code-style pattern 1 and keeps the feature in one folder. |
| **Open** | Two milestones (3a, then 3b, with a browser check in between) — recommended — or all of step 3 in one go? |
