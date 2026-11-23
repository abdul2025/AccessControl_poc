# Input points and output points — what they represent

> Explanation for milestone 3a · 2026-11-23 · spec reference: `docs/PROJECT_SPEC.md` §6.4, §7.3

They are the **physical terminals on the board** — the screw connectors where the installer wires sensors (inputs)
and locks (outputs). Each row in our tables `InputPoints` / `OutputPoints` is one terminal, so the software knows what
is connected where and how to treat it.

## On the X1100 board

```
            ┌──────────────── HID Aero X1100 ────────────────────┐
 INPUTS  →  │ IN 1  IN 2  IN 3  IN 4 │ TAMPER  AC FAIL  BATT FAIL │
 (7)        │   general purpose      │  fixed: enclosure / power  │
            │                                                    │
 OUTPUTS →  │ RELAY 1   RELAY 2   RELAY 3   RELAY 4              │
 (4)        │  (each: NO · C · NC contacts)                      │
            │                                                    │
 READERS →  │ READER 1   READER 2          (milestone 3b)        │
            └────────────────────────────────────────────────────┘
```

- **SIO 0** of every controller = the X1100's own board → **7 `InputPoints` rows + 4 `OutputPoints` rows**.
- An external **X200** adds 19 input rows (IN 1–16 + Tamper / AC fail / Battery fail) and 2 relays.
- An external **X300** adds 5 input rows (IN 1–2 + Tamper / AC fail / Battery fail) and 12 relays.
- An external **X100** adds the same as the X1100 board: 7 inputs, 4 relays, 2 reader ports.

Source: X1100 installation guide PLT-04233 ("Up to 4 Readers, 7 Inputs, 4 Outputs"; INPUTS 1-4, TAMPER, AC FAIL,
BATT FAIL; RELAYS 1-4) and the Getting Started guide ch. 04 (input numbering).

---

## InputPoint — something the controller *senses*

An input is a contact that is either **open or closed**. The controller watches it and reacts when it changes.

| Typical device | Wired to | Used by |
|---|---|---|
| **Door contact** (magnet on the door frame) | IN 1 | A door — knows if the door is open; raises *door forced* / *held open* |
| **Exit button** (request-to-exit) | IN 2 | A door — unlocks to let someone out without a card |
| Alarm contact, panic button, motion sensor (PIR), fire-alarm signal | IN 3 / IN 4 | Monitoring — an event when it changes |
| Enclosure **tamper** switch | TAMPER | Alarm if someone opens the controller box |
| Power supply **AC fail** / **battery fail** | AC FAIL / BATT FAIL | Alarm on mains loss or low battery |

### Input fields

| Field | Meaning | HID SDK |
|---|---|---|
| **Name** | What is wired there, e.g. "Main gate door contact" | (ours) |
| **Contact** | **Normally closed (NC):** closed at rest, opens when triggered — a door contact is usually NC (closed when the door is shut). **Normally open (NO):** open at rest, closes when triggered — an exit button is usually NO. | command 110 `icvt_num` |
| **Supervised** | End-of-line resistors (1k + 1k, 1 %) are fitted at the sensor, so the controller can also detect a **cut or shorted cable** (wiring tamper), not only open / closed. | command 110 `icvt_num` |
| **Monitored** | Report every change as an event ("Alarm input 3 active"). Doors handle their own inputs, so this is for stand-alone alarm inputs. Tamper / AC fail / Battery fail are monitored by default. | command 113 (monitor point) |
| **Dedicated** | Tamper / AC fail / Battery fail are fixed by the board and **cannot be used by a door**. Shown as "fixed" on the page. | from the hardware catalog |

Defaults when a board is added: IN 1–n = normally open, not supervised, not monitored; Tamper / AC fail / Battery fail =
normally closed, not supervised, monitored.

---

## OutputPoint — something the controller *switches*

An output is a **relay**: an electrically controlled switch that turns external equipment on or off. The X1100 relays
are *Form-C dry contacts* with three connections — **C** (common), **NO** (normally open), **NC** (normally closed).
Ratings: NO 5 A @ 30 V DC, NC 3 A @ 30 V DC. The relay only *switches*; the lock is powered by its own supply
(install guide: fit a protection diode, e.g. 1N4002, across DC locks).

| Typical device | Wiring | Used by |
|---|---|---|
| **Electric strike** | NO + C — power applied to unlock | A door's lock |
| **Magnetic lock** | NC + C — power removed to unlock | A door's lock |
| Siren, beacon, barrier, gate motor | NO + C | Auxiliary "control point" — switched by command or by an event |

### Output fields

| Field | Meaning | HID SDK |
|---|---|---|
| **Name** | What is wired there, e.g. "Main gate lock" | (ours) |
| **Pulse time** | For auxiliary use: how long a momentary "pulse" keeps the relay on (e.g. open a barrier for 1 s). Door locks use the door's own lock times instead. Default 1 s. | command 114 `dflt_pulse` (control point) |

---

## How they fit together — doors (milestone 3b)

A **door** links a reader with points, and the door's behaviour comes from these assignments:

```
Door "Main gate"  (ACR, command 115)
  ├─ entry reader  → READER 1 (VL70LF)
  ├─ lock          → RELAY 1       (OutputPoint)
  ├─ door contact  → IN 1  (NC)    (InputPoint)
  └─ exit button   → IN 2  (NO)    (InputPoint)
```

1. A valid card at READER 1 → the controller switches **RELAY 1** → the lock opens.
2. It watches **IN 1** → sees the door open and close; raises *door forced* (opened without a card) or *held open*
   (open too long).
3. Pressing the button on **IN 2** → the controller unlocks RELAY 1 to let someone out.

With the X1100 alone (IN 1–4, Relay 1–4) you can fully equip **2 doors** (lock + contact + exit button each), or
**1 door with an IN and an OUT reader** (like "Main gate"). More doors → add X100 boards.

## What the software does with these rows

- **Now (step 3):** stored as the desired configuration only — nothing is sent to the controller.
- **Step 4:** the Gateway turns them into HID commands:

| Our data | HID command |
|---|---|
| Every input (contact, supervision) | 110 Input point specification |
| Every relay | 111 Output point specification |
| Monitored inputs | 113 Monitor point configuration |
| Relays not used as door locks (auxiliary) | 114 Control point configuration |
| Doors (reader, lock, contact, exit button, timings) | 115 Access control reader (ACR) configuration |
