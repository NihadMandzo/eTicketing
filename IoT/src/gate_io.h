// LEDs, buzzer and the MG90S barrier servo — everything the gate does that a person can see or
// hear. Nothing in here decides anything; main.cpp tells it which verdict to perform.
#pragma once

#include <Arduino.h>

namespace gate_io {

// Call once, first thing in setup(), before the camera. The barrier is driven to CLOSED here, so a
// gate that reboots mid-shift comes back down rather than sitting open.
void begin();

// --- verdict performances (blocking, each returns with the barrier closed and the LEDs off) ---

// Valid ticket: green, one long beep, barrier up for GATE_SERVO_OPEN_MS, barrier down.
void performGranted();

// Rejected ticket: red, three short beeps. The barrier never moves.
void performDenied();

// 409 — another scanner is mid-validation on this same ticket. Amber (both LEDs) and two medium
// beeps: this is "try again", not "you are not getting in", and the gate staff should read it that
// way rather than turning the holder away.
void performRetry();

// Transport failure — no network, timeout, unparseable response. Deliberately looks different from
// a rejection so nobody mistakes a broken gate for a stream of bad tickets.
void performNetworkError();

// --- status signalling ---

void setStatusLed(bool on);

// Called from the WiFi/config wait loops; a slow blink means "working on it".
void tickStatusHeartbeat(uint32_t periodMs);

// Terminal state: the device key was rejected. Scanning stops and this keeps running so the
// problem is obvious from across a room.
void showFatal();

void setFlash(bool on);

} // namespace gate_io
