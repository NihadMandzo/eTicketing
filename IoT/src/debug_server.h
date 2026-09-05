// A testing-only web UI served off the gate itself: a live camera feed next to the decoder log,
// so a ticket that will not scan can be diagnosed by watching what the lens actually sees.
//
// Two HTTP servers on two ports, deliberately. An MJPEG response never ends, so the handler
// serving it occupies its task for as long as the browser is watching; putting the stream on its
// own port keeps the log and stats endpoints answering while the feed runs. This is the same split
// the espressif CameraWebServer example uses, for the same reason.
//
// Compiled out entirely when GATE_DEBUG_SERVER is 0.
#pragma once

#include <Arduino.h>

namespace debug_server {

// Safe to call unconditionally — a no-op unless GATE_DEBUG_SERVER is 1. Call after WiFi is up.
void begin();

bool isRunning();

} // namespace debug_server
