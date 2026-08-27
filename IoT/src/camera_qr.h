// Thin wrapper over ESP32QRCodeReader (esp32-camera + quirc), isolated behind three functions so
// swapping the decoder later touches this file and nothing else.
#pragma once

#include <Arduino.h>

namespace camera_qr {

// Initialises the OV2640 and starts the decoder task on core 1, leaving core 0 to WiFi/HTTP.
// Returns false if the camera never came up — almost always a power or ribbon-cable fault.
bool begin();

// Non-blocking. Returns true and fills `payload` when a QR was decoded this poll.
bool poll(String &payload, uint16_t timeoutMs = 60);

// Drop anything decoded while the gate was busy performing a verdict. Without this, a ticket held
// in front of the lens for two seconds queues up a burst of duplicate reads that all fire the
// moment scanning resumes.
void flush();

} // namespace camera_qr
