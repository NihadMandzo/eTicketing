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

Three things that will cost you an afternoon if skipped:

- **The servo needs its own 5V supply with a common ground**, plus a 470–1000 µF capacitor across
  its rail. Powering it from the ESP32-CAM's regulator browns out the board the instant the barrier
  moves — right after a valid ticket, every time.
- **The MG90S must be the continuous-rotation (360°) variant**, not the standard positional one.
  The firmware drives it in timed bursts and returns it to a calibrated neutral; a positional servo
  would ignore that entirely.
- **The barrier needs a mechanical stop at the closed position.** A continuous-rotation servo
  cannot report where the arm is, so the firmware re-establishes "closed" at boot by driving into
  that stop. Without one, a gate that reboots while open stays open.

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
| Valid | green | rising chime — 2000 Hz, then 3000 Hz | up 3s, then down |
| Rejected (wrong sector, already used, wrong event, forged, expired) | red | one long low tone, 700 Hz | stays down |
| `409` — another scanner has this ticket right now | green + red | two mid tones, 1200 Hz | stays down |
| Bad or revoked device key | red, slow blink, scanning stops | silent | stays down |
| Network error / timeout | red, three fast blinks | two short low tones, 700 Hz | stays down |

Pitch carries the meaning: rising = admitted, low = refused, mid = try again. The tones are
generated by bit-banging the piezo pin rather than with `tone()` — every LEDC channel on this board
is spoken for by the camera clock and the servo, and `tone()` would take the camera's.

## Diagnostics: live feed + decoder log

For testing, the gate serves a page showing **what the lens actually sees** beside a **live decoder
log**. Open `http://<device-ip>/` — the IP is printed at boot and in the serial banner.

```
[dbg] Dijagnostika: http://192.168.0.42/
```

Left panel is the camera; right panel is every decode and verdict as it happens, colour-coded
(blue informational, green admitted, amber retry/unreadable, red refused), with running counters
for decoded/failed and free heap.

The log distinguishes two failures that look identical from behind the scanner:

| Log line | Meaning | What to tell the holder |
|---|---|---|
| *(nothing)* | No QR located in the frame at all | Hold the ticket up to the camera |
| `QR pronađen ali nečitak (ECC failure)` | A QR **was** found; quirc could not read it | Move it further away, or kill the glare |

That second line is the one worth watching. It means aim is fine and something else — focus, glare
off a phone screen, a creased printout — is the problem.

**Two things to know before you rely on it:**

- **It slows scanning down.** The QR library runs the sensor in `GRAYSCALE` with a single frame
  buffer, so every frame sent to your browser is a frame the decoder does not get, and each one is
  JPEG-encoded in software on the ESP32. Watching the feed roughly halves the scan rate. Close the
  tab before judging how fast the gate is. Tune with `GATE_DEBUG_STREAM_DELAY_MS`.
- **It is unauthenticated.** Anyone on the venue WiFi can watch the camera. Set
  `GATE_DEBUG_SERVER 0` in `config.h` for anything beyond testing.

If you need the decoder library's own frame-by-frame trace (heap, stack, per-frame dimensions,
~10 lines/second), set `GATE_QR_LIBRARY_DEBUG 1` — but the gate's own log already reports
located-but-unreadable codes with quirc's reason, so try that first.

## Troubleshooting

| Symptom | Cause |
|---|---|
| WiFi connects, every request times out | Windows Firewall is blocking inbound 5000. Allow it, or check `GATE_API_BASE_URL` uses the LAN IP rather than `localhost`. |
| `Ključ uređaja je odbijen` | Key mistyped, rotated, or the device was set inactive in the back-office. |
| QR never decodes | Hold it further away — the OV2640's fixed focus starts around 8–10 cm. Glare on a phone screen is the other usual cause; try a printed ticket, and set `GATE_USE_FLASH 0` (a lit flash reflects straight back off a screen). |
| `ticket.wrong_sector` on a ticket you expect to pass | The device's sector list does not include that ticket's sector. Check the boot banner's `Sektori:` line against the back-office. |
| Board resets when the barrier moves | Servo power. See [docs/wiring.md](docs/wiring.md). |
| Servo spins and never stops | `GATE_SERVO_STOP_US` is not this servo's true neutral. It is a per-unit calibration — retune it. See [docs/wiring.md](docs/wiring.md). |
| Barrier still up after a reboot | No mechanical stop at the closed position for the boot re-datum to drive against. See [docs/wiring.md](docs/wiring.md). |
| Buzzer silent, LEDs fine | `GATE_BUZZER_ACTIVE` is `1` but the buzzer is a passive disc. Set it to `0`. |

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
| `src/camera_qr.cpp` | OV2640 + quirc, behind a few functions — the only file to touch if the decoder library changes |
| `src/gate_api.cpp` | `GET /gate/config`, `POST /gate/validate` |
| `src/gate_io.cpp` | LEDs, buzzer, MG90S servo |
| `src/wifi_link.cpp` | Connect and auto-reconnect |
| `src/debug_log.cpp` | Ring buffer of recent log lines, mirrored to Serial |
| `src/debug_server.cpp` | Testing-only diagnostics page, MJPEG stream and `/logs` |
| `include/config.example.h` | Every tunable, with the pin-choice reasoning |
