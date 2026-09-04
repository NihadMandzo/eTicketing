// Thin wrapper over ESP32QRCodeReader (esp32-camera + quirc), isolated behind a few functions so
// swapping the decoder later touches this file and nothing else.
#pragma once

#include <Arduino.h>

namespace camera_qr {

// What one poll of the decoder queue produced.
enum class Scan {
  None,     // nothing in the queue this poll — no QR in front of the lens
  Decoded,  // a QR was read; `payload` holds it
  Failed,   // a QR was LOCATED but could not be decoded; `error` holds quirc's reason
};

// Initialises the OV2640 and starts the decoder task on core 1, leaving core 0 to WiFi/HTTP.
// Returns false if the camera never came up — almost always a power or ribbon-cable fault.
bool begin();

/// <summary>Non-blocking poll of the decoder queue.
///
/// Failed decodes are reported rather than swallowed: quirc distinguishes "no QR in the frame"
/// (Scan::None, silent) from "found a QR, could not read it" (Scan::Failed, with a reason like
/// "ECC failure"), and at a door those two mean completely different things — the first is aim,
/// the second is focus, glare, or damage.</summary>
Scan poll(String &payload, String &error, uint16_t timeoutMs = 60);

// Drop anything decoded while the gate was busy performing a verdict. Without this, a ticket held
// in front of the lens for two seconds queues up a burst of duplicate reads that all fire the
// moment scanning resumes.
void flush();

// Running totals since boot, for the debug page's counters.
struct Stats {
  uint32_t decoded;
  uint32_t failed;
};
Stats stats();

} // namespace camera_qr
