# eTicketing — ESP32-CAM gate scanner

A physical gate for the eTicketing platform. An ESP32-CAM reads the QR on a ticket, sends the
decoded string to the backend, and performs the verdict: green LED + a beep + the barrier servo
lifts, or red LED + three beeps and the barrier stays down.

## What the device decides: nothing

This is the point of the design, and it is worth being explicit about.

The firmware does **not** hold the QR signing key, does **not** know which product or which sectors
this door serves, and does **not** cache verdicts. It forwards a scanned string and obeys the
answer. Everything that determines whether a ticket is good — HMAC signature, right event, right
sector, not already used, valid today, right organization — is checked server-side against the
`GateDevice` row that this device's API key resolves to.

Two consequences:

- **Re-flashing this board with different constants cannot widen what the gate admits.** There is no
  product id or sector list in the request to tamper with.
- **Re-scoping a gate needs no re-flash.** Add "Loža" to this device in the desktop back-office and
  it takes effect at the door within five minutes (or immediately on reboot).

It also **fails closed**: no network, a timeout, an unparseable response, a rejected key — the
barrier does not move. The only path that opens it is HTTP 200 with `isValid: true`.

## Hardware

See [docs/wiring.md](docs/wiring.md) for the full pin map and the bill of materials.

**The one thing you must not skip:** the MG90S servo needs its **own 5V supply with a common
ground**, plus a 470–1000 µF capacitor across its rail. Powering it from the ESP32-CAM's regulator
browns out the board the instant the barrier moves — right after a valid ticket, every time.

## Setup

### 1. Register the gate in the desktop app

Log into the Flutter desktop back-office as an organizer, open **Ulazni uređaji → Novi uređaj**:

- **Naziv** — something you will recognise on a list, e.g. `Ulaz A — VIP + Loža`.
- **Proizvod** — the event this door serves.
- **Sektori** — tick every sector this door admits. Ticking several is the supported way to run one
  scanner across multiple sectors. Or switch on **Svi sektori** for a main entrance.

Save. The API key is shown **once**. Use the **Kopiraj konfiguraciju za uređaj** button — it copies a
ready-to-paste block for the next step. If you lose the key, rotate it; it cannot be recovered.

### 2. Configure the firmware

```bash
cd IoT
cp include/config.example.h include/config.h
```

Fill in the four values at the top of `include/config.h`:

```c
#define GATE_WIFI_SSID     "..."
#define GATE_WIFI_PASSWORD "..."
#define GATE_API_BASE_URL  "http://192.168.1.10:5000/api"   // your PC's LAN IP, not localhost
#define GATE_DEVICE_KEY    "etk_gate_..."
```

`config.h` is gitignored — it holds a WiFi password and a working credential for a door.

### 3. Build and flash

Requires [PlatformIO](https://platformio.org/install) (the VS Code extension, or `pip install platformio`).

```bash
cd IoT
pio run -t upload -t monitor
```

The ESP32-CAM-MB handles the boot-mode dance itself — no IO0→GND jumper needed. If upload fails,
hold the MB's **RST** button, start the upload, release when it says `Connecting...`.

### 4. Confirm it came up

The serial monitor (115200 baud) should print:

```
[gate] eTicketing — ulazni skener
[cam] Slobodan PSRAM: 4194252 B, heap: 213...
[cam] Čitač QR koda je spreman.
[wifi] Povezan. IP: 192.168.1.42, RSSI: -54 dBm
------------------------------------------------------------
[gate] Uređaj  : Ulaz A — VIP + Loža
[gate] Proizvod: Ljetni Festival
[gate] Sektori : VIP, Loža (2)
------------------------------------------------------------
[gate] Spreman. Prislonite ulaznicu ispred kamere.
```

If the product and sector names are right, the key works and the gate is scoped correctly.

## Using it

Hold a ticket QR **8–20 cm** from the lens. Print a ticket PDF (`/api/tickets/{id}/pdf`) or show the
QR on a phone. Every scan is logged to serial with the decoded payload before the HTTP call, so a
decode failure is always distinguishable from an API failure.

| Outcome | LED | Buzzer | Barrier |
|---|---|---|---|
| Valid | green, 2s | one 400 ms beep | up 3s, then down |
| Rejected (wrong sector, already used, wrong event, forged, expired) | red, 2s | three short | stays down |
| `409` — another scanner has this ticket right now | green + red | two medium | stays down |
| Bad or revoked device key | red, slow blink, scanning stops | silent | stays down |
| Network error / timeout | red, three fast blinks | two short | stays down |

## Troubleshooting

| Symptom | Cause |
|---|---|
| WiFi connects, every request times out | Windows Firewall is blocking inbound 5000. Allow it, or check `GATE_API_BASE_URL` uses the LAN IP rather than `localhost`. |
| `Ključ uređaja je odbijen` | Key mistyped, rotated, or the device was set inactive in the back-office. |
| QR never decodes | Hold it further away — the OV2640's fixed focus starts around 8–10 cm. Glare on a phone screen is the other usual cause; try a printed ticket, and set `GATE_USE_FLASH 0` (a lit flash reflects straight back off a screen). |
| `ticket.wrong_sector` on a ticket you expect to pass | The device's sector list does not include that ticket's sector. Check the boot banner's `Sektori:` line against the back-office. |
| Board resets when the barrier moves | Servo power. See [docs/wiring.md](docs/wiring.md). |

## Known limitation: plain HTTP

The device key travels **unencrypted** on the local network, because the dev gateway has no TLS. For
a LAN gate demo that is an accepted trade-off; anyone who can already sniff the venue's WiFi could
capture the key and impersonate this door (they still could not admit tickets for another event or
sector — the key's scope is fixed server-side — but they could burn valid tickets for this one).

A production deployment needs the gateway behind TLS and `gate_api.cpp` switched from `WiFiClient`
to `WiFiClientSecure` with a pinned CA certificate.

## File map

| File | Responsibility |
|---|---|
| `src/main.cpp` | State machine and verdict dispatch |
| `src/camera_qr.cpp` | OV2640 + quirc, behind three functions — the only file to touch if the decoder library changes |
| `src/gate_api.cpp` | `GET /gate/config`, `POST /gate/validate` |
| `src/gate_io.cpp` | LEDs, buzzer, MG90S servo |
| `src/wifi_link.cpp` | Connect and auto-reconnect |
| `include/config.example.h` | Every tunable, with the pin-choice reasoning |
