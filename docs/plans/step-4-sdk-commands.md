# Step 4 — Sending the configuration to the X1100 (SDK commands)

| | |
|---|---|
| **Status** | Proposed overview — each milestone gets its own detailed plan before coding |
| **Date** | 2026-11-23 |
| **Spec** | `docs/PROJECT_SPEC.md` §3.3, §3.7, §5, §6, §7 |
| **Sources** | HID Aero SDK Specification PLT-04238 · Getting Started guide ch. 03–05 · AeroChat sample |

---

## Contents

1. [Summary](#1-summary)
2. [The flow, step by step](#2-the-flow-step-by-step)
3. [Who does what](#3-who-does-what)
4. [New data](#4-new-data)
5. [Command reference](#5-command-reference)
6. [Translation rules](#6-translation-rules-our-data--sdk-values)
7. [Error handling](#7-error-handling)
8. [Mock-up without hardware: command preview](#8-mock-up-without-hardware-command-preview)
9. [Milestones](#9-milestones)
10. [Open items](#10-open-items-to-confirm-on-the-bench)

---

## 1. Summary

- Users enter the **desired configuration** in the MVC (controllers, SIOs, inputs, relays, readers, doors).
- Users press **Publish** → a new configuration **version** is saved.
- The **Gateway** recognises the X1100 by its **serial number**, translates the data into **HID SDK commands**, and
  sends them in HID's required order.
- Every command's result (**OK / NAK**) is logged; the controller page shows **Online · In sync (v1)**.
- The **Web never talks to the controller** — only the Gateway does (spec §3.1).

```
  MVC                      Database                    Gateway                     X1100
  ───                      ────────                    ───────                     ─────
  Publish v1  ─────────►   version 1  ───────────►   build commands  ─────────►   configured
  page shows  ◄─────────   status + log  ◄────────   OK / NAK         ◄─────────   replies
```

---

## 2. The flow, step by step

### When a controller connects

| # | What happens | SDK |
|---|---|---|
| 1 | Controller sends its identity (serial, model, firmware, MAC) | ID report |
| 2 | Gateway looks up the serial in our database | — |
| 2a | **Unknown serial** → listed under *Pending controllers*, **not accepted**; the driver disconnects it after 15 s | — |
| 2b | **Known serial, first time** (adoption) → program our SCP number into the controller | 318 |
| 3 | Accept the connection (must start within 15 s) | 1107 → 1105 → 1116 → 302 |
| 4 | Check what is already on the controller | 1853 (memory), 1851 (version marker) |
| 5 | Marker = published version → **nothing to send**, go to step 7 | — |
| 6 | Marker differs → **send the configuration** (section 5.3), then store the new marker and save to flash | 108 … 115, 1852, 211 |
| 7 | Turn on event reporting | 303 |
| 8 | Read live status for the page | 401, 404, 407, 900 |

### When a user publishes while the controller is online

The Gateway notices the new version within a few seconds and runs steps 4–8 straight away.

---

## 3. Who does what

| Project | Responsibility | Main new parts |
|---|---|---|
| **Web** (MVC) | Publish, show status and history | *Publish* button · sync status + command log on the controller page · *Preview commands* page · *Pending controllers* page |
| **Application** | Use cases, no HID types | Publish use case · adopt / reject pending controller · `IControllerDriver` port |
| **Domain** | Rules | Configuration version · sync status transitions (Pending → Applying → In sync / Failed) |
| **Infrastructure** | Database | Tables in section 4 |
| **Infrastructure.Aero** | Everything HID-specific | **Configuration compiler** (our data → ordered command list) · command builders · driver implementation |
| **Gateway** | Runs it | **Provisioning service** (replaces the step-1 auto-accept) · stores live status |

---

## 4. New data

| Table / columns | Holds |
|---|---|
| **ScpConfigVersions** | Controller · version number (1, 2, …) · published at / by · hash of the command list |
| **ScpCommandLog** | Controller · version · order · command (e.g. *110 Input*) · description (*SIO 0 input 0 "IN 1"*) · result (OK / FAILED / NAK / no reply) · NAK reason · times |
| **PendingScps** | Serial · model · firmware · MAC · first / last seen · status (Pending / Adopted / Rejected) |
| **Scps** (new columns) | Online · last seen · firmware · MAC · SCP ID reported by controller · clock offset · last ID report time |

---

## 5. Command reference

### 5.1 Connection and acceptance — every connection

| Cmd | Purpose | Values come from | Notes |
|---|---|---|---|
| **318** | Set SCP ID | Controller's SCP number | Once, on adoption (open item 12) |
| **1107** | Controller memory layout | Controller allocation + fixed limits | Within 15 s · 60,000 events · 32 SIOs · 615 MPs · 388 CPs · 64 doors · GMT offset |
| **1105** | Card database layout | Controller allocation | Must follow 1107 |
| **1116** | Daylight saving table | Fixed (Saudi: none) | Required before the first 302 (open item 13) |
| **302** | Set controller clock | Host clock | Clock difference logged first |

### 5.2 Check before sending

| Cmd | Purpose | Result |
|---|---|---|
| **1853** | Read memory layout | Differs from 1107 / 1105 → controller needs *Re-initialize* (spec §3.7 rule 4a) |
| **1851** | Read our version marker | Same as published → skip sending |

### 5.3 Hardware configuration — in this order (SDK §5.15.1)

| # | Cmd | Purpose | Sent once per | Values come from |
|---|---|---|---|---|
| 1 | **108** | SIO communication driver | Port in use | Internal → port 3 · RS-485 → port 1 / 2 · 115200 baud · 90 ms |
| 2 | **109** | SIO board — **disabled** | SIO | Model code · inputs / relays / readers count · port · address |
| 3 | **110** | Input | Input | Contact + supervised → `icvt_num` (§6.1) |
| 4 | **111** | Relay | Relay | Normal mode |
| 5 | **112** | Reader | Reader | OSDP or Wiegand settings (§6.2) |
| 6 | **113** | Monitor point | *Monitored* input | MP number (§6.3) |
| 7 | **114** | Control point | Relay **not** used as a door lock | CP number (§6.3) · pulse time |
| 8 | **115** | Door (ACR) | Door | Reader · lock · contact · exit button · times · mode (§6.4) |
| 9 | **109** | SIO board — **enabled** | SIO | Same as #2 · avoids false change-of-state events (SDK §5.15) |

### 5.4 Finish

| Cmd | Purpose |
|---|---|
| **1852** | Store the new version marker on the controller |
| **211** | Save configuration (4) and card database (5) to flash |
| **303** | Turn on event reporting (always off after an IP Client connect) |

### 5.5 Status for the controller page (read only)

| Cmd | Shows |
|---|---|
| **401** | Identity: serial, firmware, MAC, card count |
| **404** | Each SIO: online / offline, model, serial, firmware, tamper / power |
| **407** | Each reader / door: online, OSDP identity, door / lock / exit button state |
| **900** | Network settings (read type 2) — **read only**, never changed (spec §5.2.1) |
| **402** | Event buffer usage |

### 5.6 Later steps — same pipeline

| Step | Commands |
|---|---|
| 5 Card formats | 1102 |
| 6 Holidays and schedules | 1104, 3103 |
| 7 Access levels | 2116 |
| 8 Cards | 8304, 3305 |
| 9 Events | transactions received by the Gateway |
| 10 Door commands (unlock, mode, LED) | 311, 308, 307, 315 — sent immediately, not versioned |

---

## 6. Translation rules (our data → SDK values)

### 6.1 Input → `icvt_num` (command 110, SDK §6.5)

| Contact | Supervised | `icvt_num` |
|---|---|---|
| Normally closed | No | **0** |
| Normally open | No | **1** |
| Normally closed | Yes (1k / 1k: 1 k normal, 2 k active) | **2** |
| Normally open | Yes (2 k normal, 1 k active) | **3** |

Other fields: debounce **4** (exit buttons **2**, as HID recommends) · hold time **0**.

> SDK §6.5 confirms the last three inputs of every board are Tamper, AC fail, Battery fail
> (X1100 / X100: 4-6 · X200: 16-18 · X300: 2-4) — matches our hardware catalog.

### 6.2 Reader (command 112, SDK §6.7)

| Interface | `dt_fmt` | `keypad_mode` | `led_drive_mode` | `osdp_flags` |
|---|---|---|---|---|
| **OSDP** | 0 | 0 | **7** | formula below |
| **Wiegand** | **1** | 2 if keypad, else 0 | **1** | 0 |

**OSDP flags formula**

```
osdp_flags = baud code  +  (address × 32)  +  (128 if secure channel)
```

| Baud | Code |
|---|---|
| 9600 | 1 |
| 19200 | 2 |
| 38400 | 3 |
| 115200 | 4 |
| 57600 | 5 |
| 230400 | 6 |

| Example | Calculation | `osdp_flags` |
|---|---|---|
| Address 0, 9600, no secure channel | 1 + 0 + 0 | **1** |
| Address 1, 9600, no secure channel | 1 + 32 + 0 | **33** |
| Address 1, 9600, secure channel | 1 + 32 + 128 | **161** |

### 6.3 Point numbers (commands 113 / 114 / 115)

| Object | Number | Why it fits |
|---|---|---|
| **Monitor point (MP)** | Position of the input among **all** inputs, ordered by SIO, then input | Max 32 × 19 + 7 = **615** = `nMp` |
| **Control point (CP)** | Position of the relay among **all** relays, ordered by SIO, then relay | Max 32 × 12 + 4 = **388** = `nCp` |
| **Door (ACR)** | The door number (0–63) | **64** = `nAcr` |

Toggling *Monitored* never renumbers other inputs. Removing an SIO in the middle shifts later numbers — the next
publish then re-sends everything (the marker changes).

### 6.4 Door → command 115

| Door field | 115 field(s) |
|---|---|
| Entry reader | `rdr_sio`, `rdr_number` |
| Lock relay | `strk_sio`, `strk_number` · `strike_mode` 1 (lock again when the door opens) |
| Lock time min / max | `strike_t_min`, `strike_t_max` |
| Door contact | `door_sio`, `door_number` (none → −1) |
| Exit button | `rex0_sio`, `rex0_number` (none → −1) |
| Held-open seconds | `dc_held` (open item 7) |
| Access mode | `default_mode`: 5 card only · 7 card and PIN · 8 card or PIN · 3 locked · 2 unlocked |
| Card formats | `cd_format` — from step 5 (none until then) |
| Offline mode | Spec default (open item 6) |

**Door with an exit reader** — decided on the bench (open item 15):

| Option | How |
|---|---|
| **A** | Two ACRs (entry + exit), linked with `access_cfg` / `pair_acr_number` |
| **B** | One ACR with an alternate reader (`altrdr_sio`, `altrdr_number`, `altrdr_spec`) |

Our stored door data supports both.

---

## 7. Error handling

| Situation | What happens |
|---|---|
| Driver refuses to queue a command | Stop · status **Failed** · reason logged |
| Controller answers **NAK** | Stop at that command · status **Failed** · command + reason shown on the page |
| No answer within 30 s | Stop · status **Failed** · "no reply" logged |
| User fixes the data | Publish a new version, or press **Retry** |
| Controller disconnects during sending | Resumes from the check (§5.2) on the next connection |
| Memory layout differs (1853) | No download · page asks for an explicit **Re-initialize** (erases the controller database — spec §3.7 rule 4a) |

---

## 8. Mock-up without hardware: command preview

The configuration compiler is pure code (our data → ordered command list), so it can be built and reviewed **before the
kit arrives**.

**Controller page → "Preview commands"** shows exactly what would be sent:

| # | Command (SDK string form) | Meaning |
|---|---|---|
| 1 | `1107 0 1 0 0 0 0 3 60000 32 615 388 64 255 1000 1000 -10800 0 255 100 0 10000 0 1 0 0` | SCP 1 memory layout |
| 2 | `108 0 1 0 3 0 90 0 0` | SIO driver: internal port |
| 3 | `109 0 1 0 7 4 4 196 0 0 -1 0 0 0 0 0 3 0` | SIO 0 — X1100 internal (disabled) |
| 4 | `110 0 1 0 0 1 4 0` | SIO 0 input 0 "IN 1" — normally open |
| 5 | `112 0 1 0 0 0 0 7 1` | Reader 0 "Main gate IN" — OSDP address 0 |
| 6 | `115 0 1 0 0 -1 0 0 0 0 1 5 1 0 0 300 0 1 …` | Door 0 "Main gate" |
| … | … | … |

Benefits:

- Every value can be checked against the SDK documentation and the **HID Command Decoder** tool.
- Optional later: a **simulated controller** in the Gateway that answers OK to each command, to test the whole
  publish → log → *In sync* flow without hardware.

---

## 9. Milestones

| Milestone | Content | Needs the X1100? |
|---|---|---|
| **4a** | Live status (online / offline, identity) stored by the Gateway · *Pending controllers* page (adopt / reject, 318) · acceptance by serial replaces the temporary auto-accept | To verify |
| **4b** | Configuration compiler + command builders (108–115, 1107, 1105) + **Preview commands** page | **No** |
| **4c** | Publish + versions + sending + marker + save to flash + command log + sync status | Yes |
| **4d** | Live SIO, reader / door and network status on the controller page (404, 407, 900, 402) | Yes |

**Suggested order:** 3b (readers, doors) → **4b** (no hardware) → 4a, 4c, 4d when the kit arrives.

---

## 10. Open items to confirm on the bench

| # | Question |
|---|---|
| 1 | VL70LF: which OSDP address / baud / secure channel it really supports |
| 6 | Offline mode policy for doors |
| 7 | `dc_held` unit and range; SANS choice of 5 or 10 minutes |
| 12 | Does the new SCP ID (318) apply only after a reconnect? |
| 13 | Does the controller accept 1116 with no daylight saving? |
| 14 | Which reader number matches which board terminal (reader 0 = READER 1?) |
| 15 | Door with exit reader: option A (two ACRs) or B (alternate reader)? |
