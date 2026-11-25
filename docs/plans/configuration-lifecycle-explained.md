# Controller configuration lifecycle — from the edit form to the X1100

| | |
|---|---|
| **Date** | 2026-11-25 |
| **Spec** | `docs/PROJECT_SPEC.md` §3.1, §3.7, §5, §6 |
| **Detailed plan** | [`step-4-publish-and-provisioning.md`](step-4-publish-and-provisioning.md) · command reference: [`step-4-sdk-commands.md`](step-4-sdk-commands.md) |
| **Built so far** | Stages 1–2 (milestone 4b-1). Stage 3 is milestone 4b-2; stages 4–6 are milestones 4a / 4c (need the X1100) |

This page explains **how a controller's configuration travels** from what a user types in the MVC to an X1100 that
either holds it (*In sync*) or rejected part of it (*Failed*). It is the "why" behind the step 4 plan.

---

## The big picture

```
 USER (Web)                    DATABASE                                 GATEWAY (Windows service + HID SDK)        X1100
 ──────────                    ────────                                 ──────────────────────────────────          ─────
 1. Edit controller,   ───►   Scps, SioPanels, Inputs,
    SIOs, readers, doors       Outputs, Readers, Doors   (the DRAFT)

 2. Press Publish      ───►   ScpConfigVersions  vN
                              (frozen JSON snapshot + hash)
                                        │
                                        └──────────────────────────►  3. Compile snapshot → ordered
                              ScpConfigCommands   ◄──────────────────      SDK command list
                              (Commands page)

                                                                       4. Controller connects  ◄───────────────  ID report
                              ScpConnectionStates ◄──────────────────     match serial, accept   ───────────────►  1107…302

                                                                       5. Verify marker; send   ───────────────►  108…115,
                              ScpApplyRuns /      ◄──────────────────     what is out of date,   ◄───────────────  OK / NAK
                              ScpApplyResults                             one command at a time

 6. Page shows         ◄───   derived sync status:
    In sync (vN) / Failed     Published vN vs Applied vM + apply state
```

**Ground rule:** the Web never talks to the controller. The Web writes the draft and the published versions; only the
Gateway talks to the X1100, and it writes its results into tables that the Web reads (spec §3.1).

---

## Stage 1 — Edit the draft ✅

The user creates a controller (SCP number, model, connection mode, allocation) and then adds SIO panels, inputs,
relays, readers and doors. All of this is saved in the normal tables (`Scps`, `SioPanels`, `InputPoints`,
`OutputPoints`, `Readers`, `Doors`).

These tables are the **draft** (desired state). Saving does **not** send anything to the hardware, so a user can save
half-finished work without affecting a live door.

Rules the Domain enforces while editing (so invalid configurations never reach the Gateway):

- one door per reader / lock relay / input; door and exit-ACR numbers unique; doors + exit ACRs ≤ 64;
- a door with an exit reader automatically gets a stable **exit ACR number** (`Doors.ExitAcrNumber`) — the SDK
  models entry + exit on one door as two *paired* ACRs;
- the SCP number can be changed only until the first publish.

## Stage 2 — Publish → `ScpConfigVersion` ✅

Pressing **Publish** on the controller page:

1. loads the whole controller aggregate;
2. builds a plain **snapshot** (`ScpConfigurationSnapshot`, by `ScpSnapshotBuilder`) — SIOs, points, readers and doors,
   referenced by hardware position (SIO + number), not by database IDs;
3. serialises it to **canonical JSON** (always the same order) and hashes it (SHA-256);
4. `Scp.Publish()` raises `PublishedVersion` to N, and a new `ScpConfigVersion` row (version N, who, when, JSON,
   hash) is saved in the same transaction, with the usual row-version check.

Why a snapshot: a version is **immutable**. Later edits change only the draft; the Gateway compiles from the snapshot,
so nothing unpublished can ever reach the hardware.

**"Unpublished changes"** on the page = hash of the current draft ≠ hash of the latest version. Publishing an
unchanged configuration is allowed — that is the "try again" path.

## Stage 3 — Compile into SDK commands ⏳ (milestone 4b-2)

The Gateway's `ConfigurationCompileService` checks every ~5 s for versions with no `CompiledAtUtc`. For each, the
`ScpConfigurationCompiler` (Infrastructure.Aero — the only place that knows HID structures) turns the snapshot into an
**ordered** list of commands in the order the SDK requires:

```
108 per RS-485 port → 109 (SIOs off) → 110 SIOs → 111 inputs → 112 readers → 113 / 114 monitor / control points
→ 115 ACRs (doors; paired entry + exit) → 109 (SIOs on)
```

Unit conversions happen here, e.g. held-open time is stored in seconds but sent in 2-second units.

Result, stored on the version:

| Outcome | What is saved | Commands page shows |
|---|---|---|
| Success | `ScpConfigCommands` rows (order, command number, target, HID text), `CompiledAtUtc`, `CommandHash` | the list, one line per command |
| Error | `CompileError` | the error message |
| Not yet | nothing | *"The Gateway has not compiled this version yet…"* ← what you see today, because this stage is not built |

The compiler lives in the Gateway because the Web may not reference HID code. The trade-off: the preview exists only
after publish and while the Gateway runs.

## Stage 4 — The controller connects and is identified ⏳ (milestone 4a)

The X1100 runs in **IP Client** mode: it connects to the Gateway and sends an ID report (serial, model, firmware, MAC,
the SCP ID it currently has).

| Serial in the database? | What happens |
|---|---|
| No | Saved under **Pending controllers**, **not accepted**. The driver drops it after ~15 s; it keeps retrying until a user adopts or rejects it. |
| Rejected | Ignored. |
| Yes | Accepted with 1107 → 1105 → 1116 → 302 using that controller's own allocation. `ScpConnectionStates` is updated (online, firmware, last seen). If the controller's SCP ID differs from the record, **318** programs the correct one and the run waits for the reconnect. |

**Adopt** writes the serial from the ID report into an existing controller record (or creates a new record with it).

## Stage 5 — Verify, then apply ⏳ (milestone 4c)

After acceptance the Gateway starts an **apply run** (`ScpApplyRuns`, outcome *Running*;
`ScpConnectionStates.ApplyState = Applying`):

1. **Allocation check (1853).** If the controller's memory layout differs from the record → stop: *Failed —
   re-initialize required*. (Changing the allocation erases the controller, so it is never done automatically.)
2. **Marker check (1851).** The Gateway stores a marker in the controller = (version, command hash). If it matches the
   published version → outcome **UpToDate**, nothing is sent.
3. **Download.** Otherwise the stored command rows are sent **one at a time**; each waits for its OK / NAK before the
   next is sent. Every result is saved in `ScpApplyResults` (Ok / Nak / NoReply / PostRejected + detail).
   The run **stops at the first non-OK command**.
4. **Finish.** On success: write the new marker (1852), save to flash (211), then turn on event reporting (303).

If the marker cannot be read yet (reply format still to confirm on the bench), the Gateway treats the controller as
out of date and does a full download — re-sending configuration is safe.

## Stage 6 — End states

| Run outcome | `ScpConnectionStates` | Page shows |
|---|---|---|
| **Succeeded** | `AppliedVersion = N`, `ApplyState = Idle` | **In sync (vN)** |
| **UpToDate** | unchanged (already N) | **In sync (vN)** |
| **Failed** | `ApplyState = Failed`, `LastError` = failing command + NAK reason | **Failed** + the run log with the failing line |
| **Interrupted** (connection lost) | `ApplyState` reset; `AppliedVersion` unchanged | **Pending** — the run starts again from step 1 on the next connection; no events are lost (303 resumes from the last reported one) |

**How to recover from Failed:** fix the draft → Publish (vN+1) → applied automatically within ~5 s while online, or on
the next connection. Publishing an unchanged version also retries.

### Sync status — derived, never stored

The badge is calculated (`ScpSyncStatuses.Derive`) from two halves written by different owners:

```
PublishedVersion (Scps — written by users)
AppliedVersion + ApplyState (ScpConnectionStates — written by the Gateway)

no published version          → Not published
ApplyState = Applying         → Applying
ApplyState = Failed           → Failed
Applied = Published           → In sync
otherwise                     → Pending
```

Keeping the Gateway's state in its own table (with no row version) means a Gateway update never triggers a
concurrency error on a user's open edit form.

---

## Why there are two hashes

A version carries two SHA-256 hashes, and they answer two different questions.

| | `SnapshotHash` | `CommandHash` |
|---|---|---|
| **Question** | *Has the user changed anything since publishing?* | *Does the controller really hold what we would send?* |
| **Hash of** | Canonical snapshot JSON (version number set to 0) | The compiled command lines (HID text) |
| **Set by** | Web, at publish (stage 2) | Gateway, at compile (stage 3) |
| **Used by** | Web — the *Unpublished changes* badge | Gateway — the config marker on the controller (stage 5) |
| **Protects** | Nothing on the hardware — a convenience | The hardware: decides whether to download |

### `SnapshotHash` — detecting unpublished changes (Web side)

The page needs to know whether the draft differs from the last published version. Instead of comparing every
field, the Web builds the snapshot of the current draft, hashes it and compares that with the latest version's
`SnapshotHash`:

- **same hash** → the draft matches what was published → no unpublished changes;
- **different hash** → something was edited since the last publish.

Two details make this reliable:

- `ScpSnapshotBuilder` sorts everything by hardware position, so the same configuration always gives the
  **same JSON** and therefore the same hash;
- the version number is **left out** of the hash (`Hash()` sets `Version = 0`), so an unchanged draft hashes equal to
  the version it was published as.

This hash is only a convenience: comparing the full JSON strings would also work, but a 64-character value is cheaper
to store and compare.

### `CommandHash` — knowing what is really on the controller (Gateway side)

After a successful download the Gateway writes a **marker** into the controller's memory: *(version, command hash)*
(1852). On every reconnect it reads the marker back (1851); if it matches the published version's marker, nothing is
sent.

The version number alone is **not** enough, because "v3" on the controller can mean something different from "v3" in
the database:

- **Compiler changes.** If a bug in how doors are translated is fixed, the same v3 snapshot now produces *different*
  commands. The version still matches, but the controller holds the old, wrong commands. The hash changes, so a new
  download is triggered.
- **Database reset or another environment.** Version numbers count from 1 per controller record. After a dev database
  is rebuilt, or when a controller moves between test and production, "v1" can be a completely different
  configuration.
- **Controller swapped or reused.** A unit configured elsewhere could happen to report the same version number.

The hash of the exact command lines proves the controller holds exactly these commands. It is also small enough to
fit in the controller's memory, where the whole configuration would not.

### When the marker is written to the controller

The marker is written at **one point only**: at the very end of a **successful** apply run (stage 5, step 4), after
every configuration command came back OK.

```
accept       1107 → 1105 → 1116 → 302
check        1853 allocation ok?   1851 read marker → differs from (v3, hash)
download     108 → 109 off → 110 → 111 → 112 → 113/114 → 115 → 109 on
                 each one waits for OK; the first NAK stops the run here ✗
             ─── only reached if ALL of the above were OK ───
marker       1852  write (v3, CommandHash) into the controller's memory   ◄── HERE
persist      211   save the controller's configuration + marker to flash
events       303   turn on event reporting
```

**Where it lives on the controller.** 1852 writes into the controller's **Remote Memory Storage**, a small area the
SDK sets aside for the host's own data. The controller does not interpret it; it just keeps it, and 1851 reads it back
on the next connection. **211** then saves to flash; without it a power cycle could lose the marker or the new
configuration, and the next connection would see a mismatch.

**Why last.** The marker means *"this controller fully holds v3"*. If it were written first and a later command got a
NAK (or the cable was pulled), the controller would claim v3 while holding only part of it. Every later reconnect
would see a matching marker, skip the download, and the broken configuration would never be fixed. Writing it last
means a failed or interrupted run leaves the **old** marker in place, so the next connection sees a mismatch and
downloads again.

**Still to confirm (open item D2 in the step-4 plan).** The exact layout of the 1852 marker and of the 1851 reply is
not verified yet. Until it is confirmed on the bench, the Gateway treats an unreadable marker as "out of date" and does
a full download: safe, just slower.

---

## Tables at a glance

| Table | Written by | Purpose |
|---|---|---|
| `Scps` … `Doors` | User (Web) | Draft configuration; `Scps.PublishedVersion` |
| `ScpConfigVersions` | Web (publish) + Gateway (compile result) | Immutable snapshot per version |
| `ScpConfigCommands` | Gateway | Compiled command list per version (Commands page) |
| `PendingScps` | Gateway; user adopts / rejects | Unknown controllers that tried to connect |
| `ScpConnectionStates` | Gateway | Online / identity / applied version / apply state |
| `ScpApplyRuns`, `ScpApplyResults` | Gateway | Log of every apply attempt and every command's result |
