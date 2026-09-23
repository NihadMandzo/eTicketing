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

It also **fails closed**: no network, a timeout, an unparseable response, a rejected key, a server
certificate it does not trust — the barrier does not move. The only path that opens it is HTTP 200
with `isValid: true`, over HTTPS to a server that proved who it is (see [HTTPS](#https)).

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

### 2. Pick a build

The firmware comes in two builds, chosen in `platformio.ini` and never in `config.h`, so a copied
settings file cannot turn a real gate into a development one:

| Build | For | `GATE_API_BASE_URL` | Diagnostic page |
|---|---|---|---|
| **`esp32cam`** (the default) | **The real gate**, against the Container Apps gateway | `https://<gateway host>/api`, HTTPS only | Compiled out |
| **`esp32cam-dev`** | Local development, against `docker compose up -d --build` | `http://<your PC's LAN IP>:5000/api` | Per `GATE_DEBUG_SERVER` |

Wrong combinations do not compile: the real build refuses any `http://` address, and the
development build accepts `http://` only to a literal private IPv4 address (`10.x`, `172.16–31.x`,
`192.168.x`), never to a host name, so it cannot be pointed at Azure over plain HTTP. The
development build also says so at every boot: `RAZVOJNA VERZIJA (esp32cam-dev) — nije za stvarni
ulaz`, plus a red line when the connection is plain HTTP.

Nothing has to change on the server for either. Locally the gateway already listens on plain HTTP
port 5000; in Azure the Container Apps ingress already serves HTTPS.

### 3. Configure the firmware

```bash
cd IoT
cp include/config.example.h include/config.h
```

Fill in the values at the top of `include/config.h`:

```c
#define GATE_WIFI_SSID     "..."
#define GATE_WIFI_PASSWORD "..."
#define GATE_API_BASE_URL  "https://<gateway host>/api"      // or http://<LAN IP>:5000/api for dev
#define GATE_DEVICE_KEY    "etk_gate_..."
```

`GATE_API_ROOT_CA` is already filled in with **DigiCert Global Root G2**, the root Azure's managed
certificates chain to; leave it (see [HTTPS](#https)). A development build over plain HTTP does not
use it, and an older `config.h` without it still builds for development.

The desktop app's **Kopiraj konfiguraciju za uređaj** fills in the URL and the key; the URL it
copies is the one that desktop app talks to. **A gate registered on the local stack and one
registered in Azure are different devices with different keys**, because they are separate
databases: register the real gate in the desktop app pointed at Azure. `config.h` is gitignored — it
holds a WiFi password and a working credential for a door.

### 4. Build and flash

Requires [PlatformIO](https://platformio.org/install) (the VS Code extension, or `pip install platformio`).

```bash
cd IoT
pio run -e esp32cam -t upload -t monitor       # the real gate
pio run -e esp32cam-dev -t upload -t monitor   # local development
```

A bare `pio run` builds the real gate. In the VS Code extension, pick the environment in the status
bar before uploading.

The ESP32-CAM-MB handles the boot-mode dance itself — no IO0→GND jumper needed. If upload fails,
hold the MB's **RST** button, start the upload, release when it says `Connecting...`.

### 5. Confirm it came up

The serial monitor (115200 baud) should print:

```
[gate] eTicketing — ulazni skener
[gate] Verzija: stvarni uređaj (esp32cam), veza samo HTTPS.
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

If the product and sector names are right, the connection works (including the certificate check,
over HTTPS), the key is accepted and the gate is scoped correctly. Each scan also logs how long the
server took to answer, and whether that scan opened a new connection (`nova TLS veza`, or
`nova HTTP veza` in a plain-HTTP development build) or reused the open one (`postojeća veza`).

## Using it

Hold a ticket QR **8–20 cm** from the lens. Print a ticket PDF (`/api/tickets/{id}/pdf`) or show the
QR on a phone. Every scan is logged to serial with the decoded payload before the HTTP call, so a
decode failure is always distinguishable from an API failure.

| Outcome | LED | Buzzer | Barrier |
|---|---|---|---|
| Valid | green | rising pair — 2000 Hz, then 3000 Hz | up 3 s, then down |
| Rejected (wrong sector, already used, wrong event, forged, expired) | red | three short bursts, 1800 Hz | stays down |
| `409` — another scanner has this ticket right now | green + red | two longer tones, 2400 Hz | stays down |
| Bad or revoked device key | red, slow blink, scanning stops | silent | stays down |
| Network error, timeout, untrusted certificate | red, three fast blinks | two short bursts, 1800 Hz | stays down |

**Pattern** carries the meaning, not pitch: a passive piezo disc is loudest around 2–4 kHz and
nearly inaudible much below that, so every tone stays in that band (`config.example.h`, the tone
section). The tones are generated by bit-banging the piezo pin rather than with `tone()` — every
LEDC channel on this board is spoken for by the camera clock and the servo, and `tone()` would take
the camera's.

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

**Three things to know before you rely on it:**

- **It exists only in the development build** (`esp32cam-dev`, with `GATE_DEBUG_SERVER 1`). The real
  build compiles it out whatever `config.h` says, and the compiler prints a note if `config.h` asks
  for it anyway.
- **It slows scanning down.** The stream and the decoder draw frames from the same camera buffers,
  so every frame sent to your browser is a frame the decoder does not get, and each one is
  JPEG-encoded in software on the ESP32. Close the tab before judging how fast the gate is. Tune with
  `GATE_DEBUG_STREAM_DELAY_MS`.
- **It is unauthenticated.** Anyone on the same network can watch the camera, which is exactly why
  the real build does not contain it.

If you need the decoder library's own frame-by-frame trace (heap, stack, per-frame dimensions,
~10 lines/second), set `GATE_QR_LIBRARY_DEBUG 1` — but the gate's own log already reports
located-but-unreadable codes with quirc's reason, so try that first.

## Troubleshooting

| Symptom | Cause |
|---|---|
| Local: WiFi connects, every request times out, or `server nije dostupan na mreži` | Windows Firewall is blocking inbound port 5000. Allow it, and check `GATE_API_BASE_URL` uses the PC's LAN IP rather than `localhost`. |
| Azure: `server nije dostupan na mreži` | The gate's WiFi has no internet access, or the host name in `GATE_API_BASE_URL` is wrong. |
| `TLS veza odbijena — X509 - Certificate verification failed` | The gateway's certificate does not chain to the root in `GATE_API_ROOT_CA`, or the host in `GATE_API_BASE_URL` is not the one the certificate was issued for. See [HTTPS](#https) for finding the right root. |
| `TLS veza odbijena — ... Memory allocation failed` | Not enough internal RAM for the handshake; the line prints the free heap. In a development build, switch off `GATE_DEBUG_SERVER` first. |
| Build stops: `The real gate (esp32cam) talks HTTPS only` | An `http://` address in the real build. Use the `https://` gateway address, or build `esp32cam-dev` for a local test. |
| Build stops: `Plain http:// is allowed only to a private LAN address` | A development build with `http://` to a host name or a public address. Use the PC's LAN IP. |
| Build stops: `GATE_API_ROOT_CA is missing` | An `https://` address with a `config.h` from before HTTPS. Copy the `GATE_API_ROOT_CA` block from `config.example.h`. |
| `Ključ uređaja je odbijen` right after switching between local and Azure | The key belongs to the other backend. Register the gate in the desktop app pointed at the server in `GATE_API_BASE_URL`. |
| `Ključ uređaja je odbijen` | Key mistyped, rotated, or the device was set inactive in the back-office. |
| QR never decodes | Hold it further away — the OV2640's fixed focus starts around 8–10 cm. Glare on a phone screen is the other usual cause; try a printed ticket, and set `GATE_USE_FLASH 0` (a lit flash reflects straight back off a screen). |
| `ticket.wrong_sector` on a ticket you expect to pass | The device's sector list does not include that ticket's sector. Check the boot banner's `Sektori:` line against the back-office. |
| Board resets when the barrier moves | Servo power. See [docs/wiring.md](docs/wiring.md). |
| Servo spins and never stops | `GATE_SERVO_STOP_US` is not this servo's true neutral. It is a per-unit calibration — retune it. See [docs/wiring.md](docs/wiring.md). |
| Barrier still up after a reboot | No mechanical stop at the closed position for the boot re-datum to drive against. See [docs/wiring.md](docs/wiring.md). |
| Buzzer silent, LEDs fine | The piezo is not on GPIO 14, or it is an active buzzer module. This firmware drives a passive disc only; see `beepAt()` in `src/gate_io.cpp` and [docs/wiring.md](docs/wiring.md). |

## HTTPS

The gate sends its device key with every request, and every validation response carries the ticket
holder's email. Over plain HTTP, anyone on the venue WiFi could read both, and with the key could act
as this door. That is no longer possible:

- **The real gate speaks HTTPS only.** `gate_api.cpp` uses `WiFiClientSecure`, and the real build
  (`esp32cam`) with a `GATE_API_BASE_URL` that is not `https://` fails at compile time. Only the
  development build may use plain HTTP, and only to a private LAN address, because a plain-HTTP gate
  aimed at a real server would send its key in clear before anything could refuse it.
- **It trusts exactly one root certificate**, `GATE_API_ROOT_CA`. The server's certificate is checked
  against it, and against the host name in the URL, before the key is sent. A fake access point
  cannot present a certificate that passes, so the key never reaches it. `setInsecure()` is never
  used: it would encrypt without checking who is on the other end.
- **One connection is kept open between scans** (`GATE_TLS_REUSE_MS`, 60 s by default), so a queue
  pays for the handshake once. After a quiet spell the next scan opens a new one.

**Which root.** Container Apps ingress serves HTTPS with a certificate Azure manages and renews.
The gate pins the **root** of that chain, not the server's own certificate, which changes at every
renewal. `config.example.h` ships **DigiCert Global Root G2** (valid until 2038, SHA-256
`CB:3C:CB:B7:…:5A:B1:CB:5F`), the root Azure's managed certificates chain to. If the gate's boot log
says `TLS veza odbijena — X509 - Certificate verification failed`, your gateway chains elsewhere:

```bash
openssl s_client -connect <gateway host>:443 -servername <gateway host> -showcerts </dev/null
```

The `issuer=` of the last certificate names the root. Download it as PEM from that CA's site and
paste it into `GATE_API_ROOT_CA` in the same quoted-line format. Several certificates may be pasted
one after another if needed.

### Deployed gates

The same firmware reaches the Container Apps gateway with no server change:

1. **Register the gate in Azure's database**, with the desktop app built against the deployed API
   (`--dart-define=API_BASE_URL=https://<gateway host>/api/`). Its **Kopiraj konfiguraciju za
   uređaj** copies the `https://` URL and the Azure key together.
2. **Flash the real build:** `pio run -e esp32cam -t upload -t monitor`. The WiFi at the venue must
   reach the internet.
3. **Keep the backend warm on event days.** If the gateway or Ticketing scale to zero replicas, the
   first request after a quiet spell waits for a cold start, which can exceed the gate's 8 s timeout.
   The gate fails closed and the next scan retries, but the first person waits. Set the minimum
   replicas of `gateway` and `ticketing-service` to 1 (Container App → Scale), or raise
   `GATE_HTTP_TIMEOUT_MS`.

### What HTTPS does not cover

The ESP32's TLS library is built without certificate date checks, so the gate needs no clock (no NTP) but also accepts an expired certificate from the pinned CA. The key
is still compiled into the firmware in clear, so someone holding the board can read it from flash
(ESP32 flash encryption would close that). If a gate is stolen, rotate its key in the back-office.

## File map

| File | Responsibility |
|---|---|
| `platformio.ini` | The two builds: `esp32cam` (the real gate, default) and `esp32cam-dev` |
| `src/build_mode.h` | What each build allows; the one place the development build is detected |
| `src/main.cpp` | State machine and verdict dispatch |
| `src/camera_qr.cpp` | OV2640 + quirc, behind a few functions — the only file to touch if the decoder library changes |
| `src/gate_api.cpp` | `GET /gate/config`, `POST /gate/validate`, over one kept HTTPS connection |
| `src/gate_io.cpp` | LEDs, buzzer, MG90S servo |
| `src/wifi_link.cpp` | Connect and auto-reconnect |
| `src/debug_log.cpp` | Ring buffer of recent log lines, mirrored to Serial |
| `src/debug_server.cpp` | Diagnostics page, MJPEG stream and `/logs`; development build only |
| `include/config.example.h` | Every tunable, with the pin-choice reasoning, and the pinned Azure root |
