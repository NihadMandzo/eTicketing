#pragma once

#include <Arduino.h>

namespace wifi_link {

void begin();

// Blocks until associated, blinking the status LED. Called at boot and again whenever the link
// drops mid-shift.
void waitUntilConnected();

bool isConnected();

String localIp();

} // namespace wifi_link
