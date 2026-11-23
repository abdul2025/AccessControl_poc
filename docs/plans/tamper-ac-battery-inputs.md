# Tamper / AC fail / Battery fail inputs — why they are in the project

> Explanation and proposed change · 2026-11-23 · milestone 3a · related: `docs/plans/inputs-outputs-explained.md`

**Question:** this is an access-control application for employee access management — are the Tamper, AC fail and
Battery fail inputs relevant?

**Short answer:** yes, but in a different way than door inputs. They do not control **who** gets in; they protect the
**access control system itself**. In an employee access system that matters — if someone can tamper with the controller
or its power, they can get past the doors.

## What each one is for

| Input | Wired to | Why it matters for employee access management |
|---|---|---|
| **Tamper** | A switch on the controller's enclosure | Alerts when someone **opens the controller box**. Inside are the relays that drive the door locks — anyone with access can short a relay and open a door without a card. This is the most common attack on access hardware. |
| **AC fail** | The power supply's "mains lost" contact | Alerts that the controller is **running on battery**. Doors still work for a while, but maintenance must react before the battery runs out. |
| **Battery fail** | The power supply's "battery low / faulty" contact | Alerts that the **backup battery will not hold**. On the next power cut the controller stops — depending on the lock type, doors either stay locked (people trapped) or unlocked (site open). |

Link to the requirements: the access-control workshop (MoM 12-Nov-2026) asked for **monitoring and alerts**
("some readers show online but do not read", security dashboard). Tamper and power alarms are the hardware side of that.

## Why they are in our data

- They are **physical terminals on every HID SIO board** — part of the "7 inputs" of the X1100 / X100
  (install guide PLT-04233: INPUTS 1-4 + TAMPER, AC FAIL, BATT FAIL), so the configuration must describe all 7 inputs
  (HID command 110 defines every input).
- They are marked **"fixed"** so they can never be chosen as a door contact or exit button.

## A practical problem with the current default

The current default for these three inputs is **normally closed + monitored**.

If the installer has **not wired** a tamper switch or the power-supply contacts — common on a test bench — an unwired
normally-closed input reads as **"open"**, and the controller would report a **permanent alarm**. That is noise, not
information.

## Options

| Option | Effect |
|---|---|
| **A. Keep them, not monitored by default** *(recommended)* | Stored and shown, but no events until the installer confirms they are wired — then tick **Monitored**. No false alarms on the bench or on sites without these contacts. |
| B. Keep as now — monitored by default | Best security if every site wires them; false alarms wherever they are not wired. |
| C. Hide them in the UI | Cleaner pages, but the alarms could never be switched on — not recommended for a security system. |

### Recommended presentation (with option A)

Group them on each SIO card under their own heading **"Enclosure & power supply"**, below the general inputs, so the
main input list shows only IN 1–n (the ones used for doors and alarms):

```
SIO 0 — Internal I/O
  Inputs                     Enclosure & power supply        Relays
  IN 1  Door contact   NC    Tamper        NC  not monitored  Relay 1  Main gate lock
  IN 2  Exit button    NO    AC fail       NC  not monitored  Relay 2  …
  IN 3  …                    Battery fail  NC  not monitored  …
  IN 4  …
```

## Change needed if option A is approved

| Where | Change |
|---|---|
| `Domain/Entities/InputPoint.cs` | Default `Monitored = false` for dedicated inputs (contact stays normally closed). |
| Migration `AddSioPoints` backfill (already applied) | A small follow-up migration sets `Monitored = 0` on the existing Tamper / AC fail / Battery fail rows of the 2 existing controllers — only where the user has not changed them. |
| `Web/Features/Scps/Views/_SioPanels.cshtml` | Separate "Enclosure & power supply" table per SIO card; hint "tick Monitored once the contact is wired". |
| Spec §6.4 | Note: dedicated inputs are not monitored until wiring is confirmed. |

**Decision (2026-11-23): option A — implemented.** Default `Monitored = false` for every input; migration
`DedicatedInputsNotMonitored` reset the existing dedicated rows; each SIO card shows a separate
"Enclosure & power supply" table.
