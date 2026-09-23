// Which of the two firmware builds this is, and what each one is allowed to do.
//
// The build is chosen by the PlatformIO environment (platformio.ini), never by config.h: config.h
// is a per-device settings file that gets copied around, and it must not be able to turn a real
// gate into a development one.
//
//   esp32cam      the real gate — HTTPS only, no diagnostic page
//   esp32cam-dev  local development — adds the diagnostic page, and plain HTTP to a LAN address
#pragma once

#include "config.h"

#ifndef GATE_DEV_BUILD
#define GATE_DEV_BUILD 0
#endif

// The diagnostic page publishes an unauthenticated view of the camera to the whole network, so the
// real build never contains it, whatever config.h says.
#define GATE_DIAGNOSTICS_ENABLED (GATE_DEV_BUILD && GATE_DEBUG_SERVER)
