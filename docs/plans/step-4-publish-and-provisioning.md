# Step 4 — Publish and provisioning engine (implementation plan)

| | |
|---|---|
| **Status** | 4b-1 implemented 2026-11-24 (steps 1–6) · 4b-2, 4a, 4c not started |
| **4b-1 notes** | `PendingScps` table moved to milestone 4a (it has no code until then). `Scp.Publish()` has no "needs an SIO" rule — every controller always has its built-in SIO. Publishing an unchanged configuration is allowed: it is the retry path. |
| **Date** | 2026-11-24 |
| **Spec** | `docs/PROJECT_SPEC.md` §3.3, §3.4, §3.7, §5, §6, §7, §9, §12 |
| **Overview** | [`step-4-sdk-commands.md`](step-4-sdk-commands.md) (flow, command reference, translation rules) — this plan adds the file-level detail |
| **Sources** | HID Aero SDK Specification PLT-04238 §5.13–5.15, §6.5, §6.7, §8.1.3, §8.2 · Getting Started guide ch. 04 · AeroChat sample · wrapper 4.7.1.41 (reflection) |
| **Verification** | `dotnet build` + manual checks (no test project) |

---

## Contents

1. [Outcome and acceptance criteria](#1-outcome-and-acceptance-criteria)
2. [Current code](#2-current-code)
3. [Design](#3-design)
4. [Decisions from the HID documents](#4-decisions-from-the-hid-documents)
5. [Implementation steps](#5-implementation-steps)
6. [Verification](#6-verification)
7. [Risks, assumptions and open items](#7-risks-assumptions-and-open-items)

---

## 1. Outcome and acceptance criteria

A user presses **Publish** on a controller. The Gateway compiles the published version into an ordered list of SDK
commands and stores it. When that X1100 connects, the Gateway recognises it by serial number, accepts it, checks the
version marker on the controller, sends only what is out of date, records OK / NAK for every command, and the
controller page shows *In sync (vN)* or *Failed* with the failing command.

| # | Acceptance criterion |
|---|---|
| A1 | **Publish** creates an immutable version (number, by, at, configuration snapshot). Later edits do not change it. The SCP number is locked from then on. |
| A2 | Within ~5 s the Gateway compiles each new version into a stored command list. The **Commands** page shows it in HID string form with a description per line. For HIDREF it matches the Getting Started ch. 04 commands (with our allocation and time zone). |
| A3 | A controller whose serial is not in the database appears under **Pending controllers** (serial, model, firmware, MAC, reported SCP ID, first / last seen) and is **not** accepted. |
| A4 | **Adopt** writes the serial into an existing serial-less IP Client record; **Create from pending** opens the create form with the serial filled in; **Reject** hides it. |
| A5 | A known serial is accepted (1107 → 1105 → 1116 → 302 with that controller's own allocation and GMT offset). If the controller's programmed SCP ID differs from the record, **318** is sent. |
| A6 | After acceptance the Gateway reads the marker (1851) and allocations (1853). Same marker → nothing downloaded. Otherwise 108 … 115 → 109 (enable) → 1852 → 211 → 303 are sent **one at a time**, each awaiting its command status; the run stops at the first NAK / timeout. |
| A7 | Every apply run and every command result is stored. The controller page shows online / offline, firmware, last seen, published vs applied version, sync status and the latest run's log. |
| A8 | Publishing while the controller is online triggers an apply within ~5 s. A disconnect mid-apply marks the run *Interrupted*; it resumes on the next connection. |
| A9 | If the allocation (1853) differs, nothing is downloaded and the status is *Failed: re-initialize required*. |
| A10 | A door with an exit reader is compiled as **two paired ACRs** (§4.1); `dc_held` is sent in 2-second units (§4.2). |

**Non-goals:** IP Server mode (1013 / 207) · "Re-initialize controller" (301) · live SIO / reader / network status
(404 / 407 / 900 — milestone 4d, separate plan) · a Retry button (publish again or reconnect) · simulated controller ·
card formats and later steps.

---

## 2. Current code

| Area | Today | Where |
|---|---|---|
| Acceptance | Posts 1107/1105/1116/302/303 fire-and-forget, gated by `AutoAcceptControllers` | `Infrastructure.Aero/Sessions/ControllerAcceptance.cs` |
| Posting | Per-SCP lock, tag via `scpGetTagLastPosted`, registry entry | `Driver/AeroCommandWriter.cs`, `Driver/PendingCommandRegistry.cs` (no awaitable result) |
| Replies | Pump → channel; `ReplyProcessingService` only logs. NAK is not matched to a tag | `Messages/AeroReplyMapper.cs`, `Gateway/Services/ReplyProcessingService.cs` |
| Session | One acceptance per connection | `Sessions/ControllerSessionTracker.cs` |
| Values | 1107 / 1105 from `ControllerAllocationDefaults` + **global** `AeroOptions.GmtOffsetSeconds`; the stored `Scp.Allocation` is ignored | `Commands/ControllerSessionCommands.cs`, `AeroOptions.cs` |
| Wrapper 4.7.1.41 | `enCfgCmnd` has 108–115, 318 (`CC_SCPID`), 1851 `enCcMemRead` (`CC_READ`), 1852 `enCcRMS` (structure not named), 1853 `enCcStrSRq` (`CC_STRSRQ`), 211 `enCcDualPortControl` (`CC_DUALPORTCONTROL`), 209 `enCcConfigSave` (`CC_CONFIGSAVE`). Structures: `CC_MSP1`, `CC_SIO`, `CC_IP`, `CC_OP`, `CC_RDR`, `CC_MP`, `CC_CP`, `CC_ACR` | `lib/HIDAero/4.7.1.41/HIDAeroWrap64.dll` |
| Domain | `Scp.SyncStatus`, `PublishedVersion`, `AppliedVersion` exist but never change; `CanChangeScpNumber` depends on `SyncStatus` | `Domain/Entities/Scp.cs`, `Enums/Scps/ScpSyncStatus.cs` |
| Door | One `Number` (0–63), `EntryReaderId`, optional `ExitReaderId`, `HeldOpenSeconds` (seconds) | `Domain/Entities/Door.cs`, `Scp.ReadersAndDoors.cs` |
| Aggregate load | `GetForUpdateAsync` includes SIOs, points, readers, doors (split query) | `Infrastructure/Persistence/Features/Scps/ScpRepository.cs` |
| Concurrency | Every `AuditableEntity` has a row version + soft-delete filter | `Persistence/AccessControlDbContext.cs` |

---

## 3. Design

### 3.1 Key decisions

1. **Snapshot at publish.** Publish serialises the hardware configuration into a plain `ScpConfigurationSnapshot`
   (Application DTO, JSON) stored on the version row. The Gateway compiles from the snapshot, never from the live
   draft — half-finished edits cannot reach the hardware (spec §3.7 rule 1).
2. **The Gateway compiles and stores the command list; the Web reads it.** The compiler is HID-specific
   (Infrastructure.Aero) and the Web may not reference that project. Trade-off: the preview exists only after publish
   and while the Gateway runs.
3. **One spec type per SDK command.** Each holds its values once and produces both the wrapper structure
   (`ToStruct()`) and the HID string (`ToText()`) — the preview is exactly what is sent.
4. **One command in flight per controller.** The registry returns a `Task<CommandOutcome>` per tag. With one command in
   flight, a NAK for that SCP belongs to that command, which solves the NAK ↔ tag correlation. ~100 round trips for a
   full HIDREF download is acceptable.
5. **Gateway-written state in its own table.** Online state and apply progress go to a 1:1 `ScpConnectionStates` table
   without a row version. Writing them to `Scps` would bump its row version and break users' open edit forms.
   `Scps` keeps `PublishedVersion` (user-written). Sync status is **derived**: no published version → NotPublished;
   else apply state Applying / Failed; else InSync when Applied = Published, otherwise Pending. `Scps.SyncStatus` and
   `Scps.AppliedVersion` are dropped (dev database: all NotPublished).
6. **No `IControllerDriver` port.** Application never calls the driver; the Gateway composes Application services
   (DB decisions, records) with Infrastructure.Aero (SDK sequence). Spec §3.4 is updated in the same change.
7. **Verify is an optimisation.** If 1851 / 1853 cannot be read or decoded, treat the controller as out of sync and do
   a full download (re-sending configuration is safe). 4c works before the reply formats are confirmed on the bench.

### 3.2 New data

| Table | Kind | Columns |
|---|---|---|
| `ScpConfigVersions` | Domain entity `ScpConfigVersion` (`BaseEntity`), saved with the `Scp` update | ScpId, Version, PublishedAtUtc, PublishedBy, SnapshotJson, SnapshotHash, CompiledAtUtc?, CommandHash?, CompileError? — unique (ScpId, Version) |
| `ScpConfigCommands` | Persistence-only | VersionId, Order, CommandNumber, Target (e.g. "SIO 0 input 3 'Door 1 REX'"), Text — unique (VersionId, Order) |
| `ScpApplyRuns` | Persistence-only | ScpId, Version, StartedAtUtc, FinishedAtUtc?, Outcome (Running / Succeeded / UpToDate / Failed / Interrupted), FailedOrder?, Reason? |
| `ScpApplyResults` | Persistence-only | RunId, Order, CommandNumber, Target, Tag, Result (Ok / Nak / NoReply / PostRejected), Detail?, AtUtc |
| `ScpConnectionStates` | Persistence-only, 1:1 with `Scps` | ScpId (PK), IsOnline, SessionScpNumber, LastSeenAtUtc, FirmwareVersion, MacAddress, ReportedScpId, ClockOffsetSeconds, LastIdReportAtUtc, MatchedAtUtc, AppliedVersion?, ApplyState (Idle / Applying / Failed), LastError? |
| `PendingScps` | Domain entity `PendingScp` (adopt / reject) | SerialNumber (unique), DeviceId, FirmwareVersion, MacAddress, ReportedScpId, FirstSeenAtUtc, LastSeenAtUtc, Status (Pending / Adopted / Rejected) |
| `Doors` (new column) | — | `ExitAcrNumber` short? — see §4.1 |

### 3.3 Boundaries

```
Web            Features/Scps (Publish, Commands, apply log)      Features/PendingScps (Index, Adopt, Reject)
                    ↓ IScpService                                     ↓ IPendingScpService
Application    Features/Scps · Features/PendingScps · Features/ScpProvisioning (IScpProvisioningService — Gateway only)
                    ↓ IScpRepository · IPendingScpRepository · IScpProvisioningRepository
Infrastructure Persistence/Features/{Scps, PendingScps, ScpProvisioning}
Infra.Aero     Configuration/ScpConfigurationCompiler · Commands/Specs/* · Driver (awaitable) · Provisioning/ControllerProvisioner
Gateway        Services/ProvisioningService (per-SCP session tasks) · Services/ConfigurationCompileService (polls new versions)
```

---

## 4. Decisions from the HID documents

### 4.1 Door with an exit reader → two paired ACRs (resolves spec open item 15)

The SDK offers two mechanisms on command 115; only one fits entry + exit:

| Mechanism | SDK fields | Meant for | Fit |
|---|---|---|---|
| **Paired readers** (SDK §8.1.3, §8.2 `access_cfg`) | Primary ACR `access_cfg = 1` controls the door; secondary ACR `access_cfg = 2` does not control the door and passes its access requests to the primary. Both name each other in `pair_acr_number` | Two readers on one door (inside / outside) | ✅ **Use this** |
| Alternate reader (`altrdr_sio`, `altrdr_number`, `altrdr_spec = 1`) | Second read head on the **same** ACR | Two read heads to the same processor (e.g. card + biometric on one side) | ❌ Not in/out: events would carry one ACR, so IN and OUT could not be told apart |

Why paired readers also match our requirements: each ACR reports its own transactions, so entry and exit events come
from different ACR numbers — exactly what the attendance feed needs (spec §1.1, §8.2 "5:x / 6:x from IN and OUT ACRs").
The SDK also notes that some SIOs support double the readers in paired configurations, which is consistent with our
4 reader slots on 2 ports (open item 14 still confirms the terminal mapping on the bench).

**Mapping (compiler):**

| | Primary ACR (entry) | Secondary ACR (exit) |
|---|---|---|
| `acr_number` | `Door.Number` | `Door.ExitAcrNumber` |
| `access_cfg` | 1 (paired, primary) — 0 (single) when the door has no exit reader | 2 (paired, secondary) |
| `pair_acr_number` | secondary's number (−1 when single) | primary's number |
| `rdr_sio` / `rdr_number` | entry reader | exit reader |
| strike / door contact / REX | from the door | −1 (not used) |
| times, `dc_held`, mode, `cd_format`, `offline_mode` | from the door | same values as the primary |

**ACR numbering — stored, not computed.** Events and access levels (step 7, command 2116 `tz[acr]`) refer to ACR
numbers, so the exit ACR number must be stable across versions. The Domain assigns it:

- `Door.ExitAcrNumber` is set when an exit reader is added (lowest number 0–63 not used by any door number or other
  exit ACR) and cleared when it is removed.
- A new door number may not collide with an existing exit ACR number.
- Total ACRs (doors + exit readers) ≤ 64 (`nAcr`).
- Shown on the door row ("ACR 0 / 2").

Step 7 consequence: an access level that opens a door must include **both** ACRs of that door.

ACC after this change: Door 1 → ACR 0 (entry, primary) + ACR 2 (exit, secondary); Door 2 → ACR 1 + ACR 3.

### 4.2 `dc_held` is in 2-second units (resolves the unit part of spec open item 7)

SDK §8.2: the held-open delay is expressed in **2-second units** (1 to 32767). `Door.HeldOpenSeconds` stays in seconds
for users; the compiler sends `ceil(seconds / 2)`. Examples: 300 s → 150 · 10 s → 5. (The Getting Started guide's
`dc_held 10` therefore means 20 s.) The 5- vs 10-minute choice itself remains with SANS Security. Door validation
changes to even values or the form shows the rounded result.

### 4.3 Other values taken from the SDK / guide

| Field | Value | Source |
|---|---|---|
| `strike_mode` | 1 (deactivate strike when the door opens) | SDK §8.2, guide ch. 04 |
| `strike_t_min` / `strike_t_max` | from the door (1 ≤ min ≤ max ≤ 255) | SDK §8.2 |
| `rex1_*` | −1 (SDK advises triggers instead of REX 1) | SDK §8.2 |
| `cd_format` | 0 until step 5 (raw 3:1 events for format discovery) | spec §7.4 |
| `offline_mode` | 1, as in the guide — **policy still open** (spec open item 6) | guide ch. 04 |
| `actl_flags` | 32 (`ACR_F_FILTER`), as in the guide | guide ch. 04 |

---

## 5. Implementation steps

### Milestone 4b-1 — Publish and version snapshot (Web / Application / Domain / Infrastructure; no Gateway)

| # | Where | Change | Depends | Done when |
|---|---|---|---|---|
| 1 | `Domain/Entities/Door.cs`, `Scp.ReadersAndDoors.cs` | `ExitAcrNumber` assigned / cleared with the exit reader; door number may not collide with an exit ACR; ACR total ≤ 64 (§4.1) | — | build |
| 2 | `Domain/Entities/Scp.cs`, new `ScpConfigVersion.cs` | `Scp.Publish()` → next version, sets `PublishedVersion`, rejects a controller without SIOs; `CanChangeScpNumber` = `PublishedVersion is null`; remove `SyncStatus` / `AppliedVersion` from the entity (enum stays, derived) | 1 | build |
| 3 | `Application/Features/Scps/DTOs/ScpConfigurationSnapshot.cs`, `ScpSnapshotBuilder.cs` | Plain records: SCP number, allocation, SIOs, inputs, outputs, readers, doors (readers / points as SIO + number, `ExitAcrNumber`); canonical JSON + SHA-256 | 2 | build |
| 4 | `IScpService` / `ScpService`, `ScpDto`, `ScpErrors` | `PublishAsync(id, rowVersion)`; `ScpDto` gains `HasUnpublishedChanges` (draft hash ≠ last published hash), derived `SyncStatus`, connection-state sub-DTO, latest version (compiled? error?); `GetCommandsAsync(scpId, version)`, `GetLatestApplyRunAsync(scpId)` | 3 | build |
| 5 | `Infrastructure/Persistence/Features/Scps/*` | Configurations for the §3.2 tables; `ScpRepository.PublishAsync` (one `SaveChanges`, row-version check) + queries; migration `AddPublishAndProvisioning` (new tables incl. `PendingScps`, `Doors.ExitAcrNumber` backfilled for existing exit readers, drop `Scps.SyncStatus` / `AppliedVersion`) | 4 | build + reviewed migration |
| 6 | `Web/Features/Scps/ScpsController.cs`, `Views/Details.cshtml`, new `Views/Commands.cshtml` | `POST Publish`, `GET Commands(id, version?)`; Configuration card: Publish button, *Unpublished changes*, Published vN / Applied vM, sync badge, online / last seen / firmware, latest run log; door rows show ACR numbers | 5 | manual A1 |

### Milestone 4b-2 — Compiler and command preview (no hardware)

| # | Where | Change | Depends | Done when |
|---|---|---|---|---|
| 7 | `Infrastructure.Aero/Commands/Specs/*` | One record per command: 1107, 1105, 1116, 302 (using the controller's allocation — removes `AeroOptions.GmtOffsetSeconds`), 108, 109, 110, 111, 112, 113, 114, 115, 318, 1852, 211, 303; each with `ToStruct()`, `ToText()`, `Target`; extend `AeroCommand` | — | build |
| 8 | `Infrastructure.Aero/Configuration/ScpConfigurationCompiler.cs` | Snapshot → ordered specs: 108 per port in use → 109 off → 110 → 111 → 112 → 113 → 114 → 115 (paired ACRs per §4.1, `dc_held` per §4.2) → 109 on. Rules: `icvt` from contact + supervised; debounce 4 (exit buttons 2); OSDP flags = baud + address×32 + 128 if secure; Wiegand `dt_fmt` 1 / LED 1 / keypad 2; MP = global input position, CP = global relay position, CP only for relays that are not door locks. Command hash = SHA-256 of the text lines | 7 | build |
| 9 | `Application/Features/ScpProvisioning/` + `Infrastructure/Persistence/Features/ScpProvisioning/` | `IScpProvisioningService` / port: `ListUncompiledVersionsAsync`, `SaveCompiledCommandsAsync`, `SaveCompileErrorAsync` (identify / record methods added in 11 and 13); register in both `DependencyInjection.cs` | 5 | build |
| 10 | `Gateway/Services/ConfigurationCompileService.cs`, `Program.cs` | `BackgroundService`, every 5 s in a fresh scope: compile uncompiled versions, store rows or the error | 8, 9 | manual A2 |

### Milestone 4a — Identification and pending controllers

| # | Where | Change | Depends | Done when |
|---|---|---|---|---|
| 11 | `Domain/Entities/PendingScp.cs`; `Scp.Adopt(serial)`; `Application/Features/PendingScps/`; `Infrastructure/Persistence/Features/PendingScps/`; `Web/Features/PendingScps/` | Pending list, Adopt (choose a serial-less IP Client controller), Reject; `ScpsController.Create` accepts `?serial=`; `ScpService.UpdateAsync` refuses a serial change once `MatchedAtUtc` is set (spec §3.7 rule 4); nav link | 5 | manual A3–A4 (bench) |
| 12 | `Gateway/Services/ProvisioningService.cs`, `ReplyProcessingService.cs` | One cancellable session task per SCP number. ID report → `IdentifyAsync(serial, identity)`: unknown → upsert `PendingScps`, do not accept; rejected → ignore; known → update `ScpConnectionStates`, go to 14. Offline → cancel the session, `IsOnline = false`. Delete `ControllerAcceptance` and `AeroOptions.AutoAcceptControllers` | 11 | bench |

### Milestone 4c — Apply engine

| # | Where | Change | Depends | Done when |
|---|---|---|---|---|
| 13 | `Driver/PendingCommandRegistry.cs`, `AeroCommandWriter.cs`, `Messages/*`, `ReplyProcessingService.cs` | `Register` returns `Task<CommandOutcome>` (`TaskCompletionSource`, `RunContinuationsAsynchronously`); `Complete` → OK / failed; sweep → NoReply; last NAK per SCP attached to the failed outcome; `AeroCommandWriter.SendAsync(scp, spec, ct)`; map 1851 / 1853 replies (after D2) | 7 | build |
| 14 | `Infrastructure.Aero/Provisioning/ControllerProvisioner.cs` (+ `IApplyRecorder` callback so Aero stays DB-free) | (1) 1107 / 1105 / 1116 / 302 within 15 s. (2) Reported SCP ID ≠ record → 318, stop, run = "awaiting reconnect with SCP ID n" (D4). (3) 1853 differs → Failed "re-initialize required". (4) 1851 marker = (version, command hash) → UpToDate. (5) Else stored command rows in order, stop at first non-OK, then 1852, 211 (4), 211 (5). (6) 303 (−2) in every outcome except a lost connection. Each result recorded; run finished Succeeded / Failed / Interrupted | 12, 13 | bench A5–A9 |
| 15 | `ProvisioningService` | 5 s check: online + accepted + Published > Applied + compiled + not applying → steps 14 (4)–(6) | 14 | bench A8 |
| 16 | `docs/PROJECT_SPEC.md`, `GATEWAY-COMMANDS.md`, `step-4-sdk-commands.md` | Spec: §3.4 (no `IControllerDriver`, new services), §3.6 step 4 status, §3.7 derived sync status + connection-state table, §4 ACR plan (paired IN / OUT), §7.3 `dc_held` unit + paired ACRs, §9 new tables, §12 close items 15 and the unit part of 7. Gateway doc: remove AutoAccept, add adoption. Overview: link to this plan | all | review |

**Suggested order:** 1–10 now (no hardware) → 11–15 when the X1100 kit arrives.

---

## 6. Verification

**Build:** `dotnet build AccessControl.slnx` (x64) after every step, 0 warnings. Review the generated migration SQL
before `dotnet ef database update`.

**Without hardware**

- **A1** — Publish HIDREF: version 1, SCP number field locked. Edit an input → *Unpublished changes*; v1's snapshot unchanged.
- **A2** — Start the Gateway: within ~5 s HIDREF's Commands page lists 1107 … 303. Compare line by line with Getting
  Started ch. 04 (differences expected only in 1107 allocation / GMT offset, `cd_format` 0, `dc_held` 5 = 10 s ÷ 2).
  Spot-check 112 / 115 with the HID Command Decoder tool.
- **A10** — ACC: 112 OSDP flags 1 / 33; four 115 lines — ACR 0 `access_cfg 1` pair 2, ACR 2 `access_cfg 2` pair 0
  with strike / door / REX −1; same for ACR 1 / 3; `dc_held` 150. HQ SCP 2: no 112 / 115.
- **Concurrency** — with the Gateway running, open a controller edit form, let the Gateway update its connection
  state, then save: no concurrency error.

**Bench (X1100)**

- **A3** — unknown serial appears under Pending; log shows not accepted.
- **A4–A5** — adopt into ACC (SCP 3); 318 sent; after reconnect the session runs as SCP 3.
- **A6–A7** — run log all OK, *In sync (v1)*; reconnect → run UpToDate, nothing downloaded.
- **A6 NAK path** — deliberate failure (e.g. a reader slot the board rejects) → Failed with command + NAK reason.
- **A8** — pull the cable mid-apply → Interrupted, resumes on reconnect; publish v2 while online → apply within ~5 s.
- **A10** — swipe at the exit reader → transaction carries the secondary ACR number; the lock on the primary's relay
  releases.
- **A9** — not safely forceable; code review + log only.
- Cross-check SIOs and ACRs exist with 404 / 407 in the HID Panel Tester.

---

## 7. Risks, assumptions and open items

### Open items (decide before the step that needs them)

| # | Item | Needed by | Approach |
|---|---|---|---|
| D2 | 1852 marker structure (no `CC_` type named in the wrapper) and the 1851 reply layout | 13–14 | Read SDK §5.13–5.14; until confirmed use the full-download fallback (§3.1 decision 7) |
| D3 | 211 arguments (`CC_DUALPORTCONTROL.command` 4 / 5) vs 209 `CC_CONFIGSAVE` | 14 | Confirm in the SDK spec; spec §5.3 currently says 211 4 / 5 |
| D4 | Does 318 take effect only after a reconnect? (spec open item 12) | 14 | Stop after 318 and wait for reconnect; confirm on the bench |
| D5 | `offline_mode` policy (spec open item 6); 5 vs 10 min held-open (open item 7) | 8 | Guide value 1 and the door's stored value; labelled on the Commands page |

Resolved by this plan: exit reader mapping (§4.1, spec open item 15) and the `dc_held` unit (§4.2).

### Risks

- **15 s acceptance window** — a slow database could delay 1107. Only one indexed serial lookup runs before 1107;
  everything else is recorded afterwards.
- **Reply-loop deadlock** — session tasks must never block the reply loop. Sessions run on their own tasks; the loop
  only completes `TaskCompletionSource`s; continuations run asynchronously.
- **Interrupted apply** — the next connection starts again from verify; 303 −2 resumes events from the last reported
  one (spec §5.6), so no event is lost.
- **MP / CP renumbering** — removing a middle SIO shifts monitor / control point numbers; the new version recompiles
  everything, the marker changes, a full download follows. Acceptable.
- **Exit ACR numbers** — stored per door (§4.1) so they stay stable for events and access levels; reassigned only when
  the exit reader is removed and re-added.
- **Wrapper 4.8.1.3** may change `CC_` field sets (spec open item 5); the spec records are the only code that touches
  the structures.

### Assumptions

- IP Client only; one Gateway instance (spec §3.5 rule 9).
- Without hardware, verification ends at the Commands preview.
- The HID SDK specification is confidential: this plan paraphrases it and quotes no text.
