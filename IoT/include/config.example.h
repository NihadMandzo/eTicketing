// Copy this file to include/config.h and fill in the four values at the top.
// config.h is gitignored — it holds a WiFi password and a working gate credential.
#pragma once

// ---------------------------------------------------------------------------- must be filled in

#define GATE_WIFI_SSID "TvojWifi"
#define GATE_WIFI_PASSWORD "TvojaLozinka"

// Base URL of the API gateway, WITHOUT a trailing slash, INCLUDING the /api prefix.
// Use the host machine's LAN address, not localhost — the ESP32 is a separate device.
// Find it with `ipconfig` on Windows; the gateway is published on port 5000 by docker-compose.
#define GATE_API_BASE_URL "http://192.168.1.10:5000/api"

// The API key shown ONCE when this gate was registered in the desktop back-office
// (Ulazni uređaji → Novi uređaj). Rotate it there if it ever leaks; the old one dies instantly.
#define GATE_DEVICE_KEY "etk_gate_xxxxxxxxxxxxxxxxxxxxxxx"

// ------------------------------------------------------------------------------------- hardware
//
// Free GPIO on an AI-Thinker ESP32-CAM is scarce — the camera takes most of it and PSRAM takes
// GPIO16. These defaults assume the microSD slot is UNUSED, which frees GPIO 2/4/12/13/14/15.

#define GATE_PIN_SERVO 12      // MG90S signal. MTDI strapping pin — see docs/wiring.md before rewiring.
#define GATE_PIN_BUZZER 14     // Passive piezo. No boot-time role, unlike 12 and 15 either side of it.
#define GATE_PIN_LED_GREEN 2   // Strapping pin; an LED to GND keeps it low at boot, which is safe.
#define GATE_PIN_LED_RED 15    // MTDO strapping pin. An LED to GND holds it low at boot, which only
                               // silences the ROM bootloader's serial chatter — harmless here.
#define GATE_PIN_STATUS 33     // Onboard red LED, already wired. ACTIVE LOW.
#define GATE_PIN_FLASH 4       // Onboard white flash LED, already wired. Very bright, very thirsty.

// This gate drives a PASSIVE piezo only — a bare disc with no oscillator, which makes sound solely
// from a driven waveform. There is deliberately no switch for an active buzzer: the branch that
// used to exist was selected by any non-zero value of a #if'd macro, and setting it wrong left the
// disc held at a DC level and completely silent. Swapping in an active buzzer now means changing
// beepAt() in gate_io.cpp, which is a visible edit rather than a silent misconfiguration.
//
// Tone frequencies, Hz. A passive piezo disc is a RESONANT transducer: it radiates loudest around
// 2-4kHz and falls off steeply below that, so a lower tone is not merely lower-pitched, it is much
// quieter — the 700Hz a rejection used to sound is close to inaudible across an entrance. Valid and
// invalid are therefore told apart by PATTERN (a rising pair vs three short bursts) while both
// pitches stay inside the band where the disc actually radiates.
#define GATE_TONE_GRANTED_LOW_HZ 2000
#define GATE_TONE_GRANTED_HIGH_HZ 3000
#define GATE_TONE_DENIED_HZ 1800
#define GATE_TONE_RETRY_HZ 2400

// ---- barrier servo: CONTINUOUS ROTATION, not positional ----
//
// This is a 360° MG90S, so a pulse width sets a SPEED AND DIRECTION, not an angle. The gate
// therefore moves in timed bursts — drive for N ms, then return to STOP — and there is no
// position feedback anywhere in the system. Everything below follows from that.
//
// STOP is a per-unit calibration, not a constant: it is whatever pulse leaves YOUR servo
// perfectly still. 1500 is nominal; this unit sits at 1520. If the servo creeps while the gate
// is idle, retune this first — that drift is the arm slowly walking out of alignment.
#define GATE_SERVO_STOP_US 1520
#define GATE_SERVO_OPEN_US 1659
#define GATE_SERVO_CLOSE_US 1367

// Pulse envelope handed to attach(). Wider than the positional default because the calibrated
// values above must sit comfortably inside it.
#define GATE_SERVO_MIN_US 500
#define GATE_SERVO_MAX_US 2500

// How long each burst runs. Keep these two EQUAL: a close that travels exactly as far as the
// open is what returns the arm to where it started, so error does not accumulate over a shift.
#define GATE_SERVO_OPEN_TRAVEL_MS 350
#define GATE_SERVO_CLOSE_TRAVEL_MS 350

// Boot only. A continuous-rotation servo cannot report where the arm is, so a gate that reboots
// mid-shift has no idea whether it is holding the barrier up. This deliberately overshoots the
// normal close travel to drive the arm into its mechanical closed stop, which re-establishes a
// known position from any starting point — and doubles as drift correction.
//
// REQUIRES a physical stop at the closed position. Without one there is nothing to stop the
// overshoot, and the arm simply rotates past closed. See docs/wiring.md.
#define GATE_SERVO_REDATUM_MS 525

// Light the flash LED while scanning. Helpful for printed tickets in a dim entrance, harmful for
// phone screens (it reflects straight back into the lens). Off by default.
#define GATE_USE_FLASH 0

// -------------------------------------------------------------------------------------- timings

#define GATE_OPEN_HOLD_MS 3000        // How long the barrier stays up after a valid ticket.
#define GATE_VERDICT_HOLD_MS 2500     // How long a red/green verdict is shown before scanning again.
#define GATE_RESCAN_GUARD_MS 3000     // Ignore the same payload again within this window.
#define GATE_CONFIG_REFRESH_MS 300000 // Re-fetch /gate/config every 5 min, so a sector change made
                                      // in the back-office reaches this door with no re-flash.
#define GATE_HTTP_TIMEOUT_MS 8000
#define GATE_WIFI_RETRY_MS 5000

// ------------------------------------------------------------------------------ testing / debug
//
// Serves a diagnostic page from the gate itself: live camera feed beside the decoder log, at
// http://<device-ip>/ (the IP is printed at boot). Use it to see what the lens actually sees when
// a ticket refuses to scan.
//
// TURN THIS OFF FOR ANYTHING RESEMBLING PRODUCTION. It publishes an unauthenticated view of the
// camera to everyone on the venue's network, and it slows scanning down (see below).
#define GATE_DEBUG_SERVER 1

// The page is on this port; the MJPEG stream is on this port + 1. Two servers because an MJPEG
// response never finishes, so it would otherwise block the log endpoint behind it.
#define GATE_DEBUG_PORT 80

// Log a per-stage timing breakdown of the MJPEG loop (frame grab / JPEG encode / downscale / socket
// send) into the decoder log every 25 frames. Answers "why is the feed slow" with measurements
// instead of guesses. Off in normal use — it is noise in the log panel.
#define GATE_DEBUG_STREAM_PROFILE 0

// Camera frame buffers. The library hardcodes 1, which tears the moment a second consumer (the
// debug stream) exists. Two stops the tearing; three additionally gives the sensor's DMA a spare to
// fill while the decoder holds one for quirc and the stream holds the other, which is what keeps the
// preview moving instead of stalling behind each decode. VGA grayscale is ~300KB per buffer, out of
// 4MB of PSRAM. camera_qr::begin() walks this down if the allocation fails.
#define GATE_CAM_FB_COUNT 3

// 1..100, HIGHER is better. This is the jpge software encoder's scale — not the sensor's hardware
// `jpeg_quality` field, which is 0..63 and inverted. Conflating the two is why this used to read 12
// and produce a visibly blocky picture. The camera runs in GRAYSCALE for the decoder, so every
// streamed frame is compressed on the CPU and quality does cost real time — though at ~76ms for an
// HVGA frame it is not what limits the feed; the ~195ms sensor grab is.
#define GATE_DEBUG_JPEG_QUALITY 70

// Halve the streamed frame to QVGA before encoding. A quarter of the pixels is roughly a quarter of
// the encode time and a quarter of the bytes on the wire, and this preview only has to be good
// enough to aim the lens — the decoder keeps reading full GATE_FRAME_SIZE frames either way. Set to
// 0 to stream at the sensor's full resolution.
#define GATE_DEBUG_STREAM_HALVE 0

// Pause between streamed frames. The stream and the decoder pull from the same buffer pool, so
// every frame sent to a browser is one the decoder does not get. Raise this to favour scanning,
// lower it for a smoother picture.
#define GATE_DEBUG_STREAM_DELAY_MS 40

// Ask the QR library to dump its own per-frame chatter to serial (very noisy: heap, stack, frame
// dimensions and a line per failed decode, ~10x/second). Off unless you are chasing the decoder
// itself — the gate's own log already reports located-but-unreadable codes with quirc's reason.
#define GATE_QR_LIBRARY_DEBUG 0

// Camera frame size used for decoding — the single biggest control over how responsive this gate
// feels, because quirc needs GRAYSCALE and grayscale frames are uncompressed. Every frame is DMA'd
// whole into PSRAM, so the time to fetch one scales directly with its pixel count: measured at
// ~387ms for VGA, which capped both the decoder and the debug preview at about 2/sec. (The
// Espressif camera examples are fast at this resolution only because PIXFORMAT_JPEG lets the OV2640
// compress in hardware to a few KB before the data ever crosses the bus. quirc cannot read a JPEG,
// so that path is not open to us.)
//
// HVGA (480x320) halves the pixels and so roughly halves the grab to ~195ms, while keeping 1.5x the
// linear resolution of QVGA — enough modules per QR to read a ticket at a normal presenting
// distance. Raise to FRAMESIZE_VGA for maximum read range at ~2 fps; drop to FRAMESIZE_QVGA for
// ~6-7 fps if tickets are always presented close to the lens.
#define GATE_FRAME_SIZE FRAMESIZE_HVGA
