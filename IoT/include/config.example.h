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
//
// GPIO 12 is deliberately left alone: it is the MTDI strapping pin, and anything holding it high
// at boot tells the chip to run its flash at 1.8V, which stops the module booting at all.

#define GATE_PIN_SERVO 13   // MG90S signal. Non-strapping, PWM-capable.
#define GATE_PIN_BUZZER 14  // Active buzzer (the kind that tones on its own from a DC level).
#define GATE_PIN_LED_GREEN 15
#define GATE_PIN_LED_RED 2  // Strapping pin; an LED to GND keeps it low at boot, which is safe.
#define GATE_PIN_STATUS 33  // Onboard red LED, already wired. ACTIVE LOW.
#define GATE_PIN_FLASH 4    // Onboard white flash LED, already wired. Very bright, very thirsty.

// Set to 0 if your buzzer is a passive one (a bare piezo disc that needs a driven frequency).
// The firmware then pulses the pin in software instead of just holding it high.
#define GATE_BUZZER_ACTIVE 1

// MG90S travel. 0 = barrier down, 90 = barrier up. Swap if your horn is mounted mirrored.
#define GATE_SERVO_CLOSED_DEG 0
#define GATE_SERVO_OPEN_DEG 90

// Standard hobby-servo pulse envelope, in microseconds. Widen if your MG90S does not reach a
// full 90°; narrow it if it buzzes and strains at the ends of travel.
#define GATE_SERVO_MIN_US 500
#define GATE_SERVO_MAX_US 2400

// Light the flash LED while scanning. Helpful for printed tickets in a dim entrance, harmful for
// phone screens (it reflects straight back into the lens). Off by default.
#define GATE_USE_FLASH 0

// -------------------------------------------------------------------------------------- timings

#define GATE_SERVO_OPEN_MS 3000       // How long the barrier stays up after a valid ticket.
#define GATE_VERDICT_HOLD_MS 2000     // How long a red/green verdict is shown before scanning again.
#define GATE_RESCAN_GUARD_MS 3000     // Ignore the same payload again within this window.
#define GATE_CONFIG_REFRESH_MS 300000 // Re-fetch /gate/config every 5 min, so a sector change made
                                      // in the back-office reaches this door with no re-flash.
#define GATE_HTTP_TIMEOUT_MS 8000
#define GATE_WIFI_RETRY_MS 5000

// Camera frame size used for decoding. VGA (640x480) gives quirc roughly twice the pixels per QR
// module that QVGA does, which is what makes a ticket readable at arm's length instead of having
// to fill the frame. Drop to FRAMESIZE_QVGA only if you are chasing latency.
#define GATE_FRAME_SIZE FRAMESIZE_VGA
