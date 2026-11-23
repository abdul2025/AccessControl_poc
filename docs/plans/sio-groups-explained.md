# SIO panels and their groups — detailed explanation

> Reference for the controller page "I/O panels (SIO)" · 2026-11-23
> Sources: X1100 install guide PLT-04233, SDK Specification PLT-04238 §6.2–6.7, Getting Started guide ch. 04
> Related: `inputs-outputs-explained.md`, `tamper-ac-battery-inputs.md`

---

## 1. What an SIO is

An **SIO** (Serial Input/Output board) is the hardware that the doors, sensors and locks are physically wired to.
The **X1100 controller (SCP)** is the brain — it holds the cards and decides who may enter — but it only "sees" the
world through its SIOs.

```
                       ┌──────────────────────────┐
  Gateway (TCP 3001) ──┤   X1100 controller (SCP)  │
                       │                           │
                       │  SIO 0 — built-in board   │ ← readers, inputs, relays on the X1100 itself
                       │                           │
                       │  RS-485 port 1 ─────────┐ │
                       │  RS-485 port 2 ───────┐ │ │
                       └───────────────────────┼─┼─┘
                                               │ └── X100 (addr 0) ── X200 (addr 1) ── …  up to 16 boards
                                               └──── X300 (addr 0) ── …                   up to 16 boards
```

| Rule | Value | Why |
|---|---|---|
| SIOs per controller | max **32** (numbers 0–31) | 1107 allocation `nSio = 32` |
| SIO 0 | always the **built-in** board of the X1100 | created automatically, cannot be removed |
| External SIOs | on **RS-485 port 1 or 2** | the two "IO MODULE PORTS" on the X1100 |
| Boards per RS-485 port | max **16** | HID limit per port |
| RS-485 address | **0–15**, unique on its port | set with the switches on each board |
| Model, port, address | fixed once added | remove and add again to change them |

---

## 2. The four SIO models

| Model | HID code | Inputs (general + fixed) | Relays | Reader ports | Typical use |
|---|---|---|---|---|---|
| **X1100 internal** | 196 | 4 + 3 = **7** | **4** | **2** (up to 4 readers) | The controller's own board — first 1-2 doors |
| **X100** | 193 | 4 + 3 = **7** | **4** | **2** (up to 4 readers) | **More doors**: same layout as the X1100 board |
| **X200** | 194 | 16 + 3 = **19** | **2** | — | **Many inputs**: alarm contacts, door contacts of doors without readers, panic buttons, motion sensors |
| **X300** | 195 | 2 + 3 = **5** | **12** | — | **Many relays**: sirens, lights, barriers, elevator floor buttons |

"Fixed" inputs = Tamper, AC fail, Battery fail (section 4). Every board has them.

**How many doors?** A fully equipped door uses 1 reader, 1 relay (lock) and 2 inputs (door contact, exit button).

| Board | Doors it can fully equip on its own |
|---|---|
| X1100 internal / X100 | **2** (4 inputs, 4 relays → or 1 door with IN + OUT readers and spare points) |
| X200 / X300 | 0 — no reader ports; they add inputs / relays to doors on other boards or for alarms |

---

## 3. The three groups on each SIO card

On the controller page every SIO is a card with three tables:

```
SIO 0 — Internal I/O          X1100 internal (196) · Internal · 7 inputs · 4 relays · 2 reader ports
┌─────────────────────────┐  ┌──────────────────────────────┐  ┌──────────────────────┐
│ Inputs                  │  │ Enclosure & power supply     │  │ Relays               │
│ 0 IN 1 …  1 IN 2 …      │  │ 4 Tamper  5 AC fail          │  │ 0 Relay 1 … 3 Relay 4│
│ 2 IN 3 …  3 IN 4 …      │  │ 6 Battery fail               │  │                      │
└─────────────────────────┘  └──────────────────────────────┘  └──────────────────────┘
```

Readers are not a table on the card — they are added in the **Readers** section of the page and placed on an SIO's
reader port (only X1100 internal and X100 have reader ports).

---

## 4. Group 1 — Inputs (general purpose)

**What:** the terminals **IN 1 … IN n** where the installer wires *contacts* — things that are open or closed.

| Board | Terminals | Input numbers |
|---|---|---|
| X1100 internal / X100 | IN 1–4 | 0–3 |
| X200 | IN 1–16 | 0–15 |
| X300 | IN 1–2 | 0–1 |

**Typical devices**

| Device | Contact type | Used by |
|---|---|---|
| Door contact (magnet on the frame) | usually **NC** | a **door** (forced / held-open detection) |
| Exit button (request to exit) | usually **NO** | a **door** (unlock to leave) |
| Panic button, alarm contact, motion sensor, fire-alarm signal | per device | **monitoring** (events) |

**Fields you set**

| Field | Meaning | Default |
|---|---|---|
| Name | what is wired there, e.g. "Main gate door contact" | IN 1, IN 2 … |
| Contact | **NC** = closed at rest · **NO** = open at rest | NO |
| Supervised | end-of-line resistors (1k/1k) fitted → also detects cut / shorted cable | No |
| Monitored | report every change as an event (for stand-alone alarm inputs; doors handle their own inputs) | No |
| Used by (read-only) | which door uses it, e.g. "Door 0 contact" | — |

**Sent to the controller (step 4):** command **110** for every input (contact + supervision → `icvt_num` 0–3),
command **113** for each *monitored* input, command **115** links door contact / exit button to a door.

---

## 5. Group 2 — Enclosure & power supply (fixed)

**What:** the last three inputs of every board — wired to the **controller cabinet** and its **power supply**,
not to doors. Shown as *fixed*; they can never be chosen for a door.

| Input | Board terminal | Input number (X1100/X100 · X200 · X300) | Wired to | Alerts when |
|---|---|---|---|---|
| **Tamper** | TAMPER | 4 · 16 · 2 | switch on the enclosure lid | someone **opens the box** (risk: shorting a lock relay inside) |
| **AC fail** | AC FAIL | 5 · 17 · 3 | power supply "mains lost" contact | controller **runs on battery** |
| **Battery fail** | BATT FAIL | 6 · 18 · 4 | power supply "battery low / faulty" contact | backup battery **will not hold** on the next power cut |

(SDK §6.5: "The last three inputs on the SIO are Tamper, AC Fail, and Battery Fail.")

**Fields and defaults**

| Field | Default | Change when |
|---|---|---|
| Contact | **NC** | the power supply's contact is the other type |
| Supervised | No | resistors are fitted |
| Monitored | **No** | **tick it once the installer confirms the contact is wired** — an unwired NC input reads "open" and would raise a permanent alarm (decision: option A) |

**Sent to the controller (step 4):** command **110** (always), command **113** only when *Monitored* is ticked.

---

## 6. Group 3 — Relays (outputs)

**What:** the terminals **RELAY 1 … RELAY n** — electrically controlled switches that turn equipment on and off.
Each relay has three contacts: **C** (common), **NO** (normally open), **NC** (normally closed).
X1100 rating: NO 5 A @ 30 V DC, NC 3 A @ 30 V DC; the lock has its own power supply, the relay only switches it.

| Board | Relays | Output numbers |
|---|---|---|
| X1100 internal / X100 | Relay 1–4 | 0–3 |
| X200 | Relay 1–2 | 0–1 |
| X300 | Relay 1–12 | 0–11 |

**Typical devices**

| Device | Wiring | Used by |
|---|---|---|
| Electric strike | NO + C (power on = unlock) | a **door** as its lock |
| Magnetic lock | NC + C (power off = unlock) | a **door** as its lock |
| Siren, beacon, barrier, gate motor, light | NO + C | **auxiliary** control point (switched by command or event) |

**Fields you set**

| Field | Meaning | Default |
|---|---|---|
| Name | what is wired there, e.g. "Main gate lock" | Relay 1, Relay 2 … |
| Pulse time | for auxiliary use: how long a momentary activation keeps the relay on (door locks use the door's own unlock times) | 1 s |
| Used by (read-only) | e.g. "Door 0 lock" | — |

**Sent to the controller (step 4):** command **111** for every relay, command **114** for relays **not** used as a
door lock (auxiliary), command **115** links the lock relay to its door.

---

## 7. Reader ports (X1100 internal and X100 only)

Not a table on the SIO card, but part of the board:

| Item | Value |
|---|---|
| Terminals | READER 1, READER 2 |
| Reader slots per board | 4 (numbers 0–3) — reader 0 = READER 1, reader 1 = READER 2 (open item 14: slots 2–3 to confirm) |
| OSDP | up to 4 readers on the 2 terminals using addresses 0–3 (to confirm) |
| Wiegand | 1 reader per terminal → 2 |
| Power | 12 V DC, 500 mA per reader |

Readers are managed in the **Readers** section; doors in the **Doors** section (they pick a reader, a relay and inputs
from these groups).

---

## 8. Worked example — "Main gate" on the X1100 alone

| Group | Terminal | Name | Settings | Used by |
|---|---|---|---|---|
| Reader port | READER 1 | Main gate IN | OSDP address 0 | Door 0 (entry) |
| Reader port | READER 2 | Main gate OUT | OSDP address 1 | Door 0 (exit) |
| Inputs | IN 1 | Main gate door contact | NC, supervised | Door 0 contact |
| Inputs | IN 2 | Main gate exit button | NO | Door 0 exit button |
| Inputs | IN 3 | Panic button lobby | NO, monitored | — (alarm events) |
| Inputs | IN 4 | (spare) | NO | — |
| Enclosure & power | Tamper | Cabinet tamper | NC, monitored once wired | — |
| Enclosure & power | AC fail | PSU mains | NC, monitored once wired | — |
| Enclosure & power | Battery fail | PSU battery | NC, monitored once wired | — |
| Relays | RELAY 1 | Main gate lock | — | Door 0 lock |
| Relays | RELAY 2 | Lobby siren | pulse 5 s | auxiliary |
| Relays | RELAY 3–4 | (spare) | — | — |

Adding a **second gate** → add an **X100** on RS-485 port 1, address 0 → it brings 2 more reader ports, IN 1–4 and
Relay 1–4 for that door.
