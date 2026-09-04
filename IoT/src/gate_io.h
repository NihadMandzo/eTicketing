// LEDs, buzzer and the MG90S barrier servo — everything the gate does that a person can see or
// hear. Nothing in here decides anything; main.cpp tells it which verdict to perform.
//
// The servo is a CONTINUOUS-ROTATION unit, so nothing here can ask it for an angle or read one
// back: a pulse width is a speed, movement is expressed as a burst duration, and the firmware
// never truly knows where the arm is. begin()'s re-datum is what substitutes for that missing
// feedback.
#pragma once

#include <Arduino.h>

namespace gate_io {

// Call once, first thing in setup(), before the camera. Holds the servo neutral, then drives it
// into its mechanical closed stop for longer than a normal close takes — so a gate that reboots
// mid-shift ends up closed no matter where the reboot found the arm.
//
// This assumes a physical stop exists at the closed position (see docs/wiring.md). Without one
// there is nothing to arrest the overshoot and the arm rotates past closed.
void begin();

// --- verdict performances (blocking, each returns with the barrier closed and the LEDs off) ---

// Valid ticket: green, a rising two-tone chime, barrier up for GATE_OPEN_HOLD_MS, barrier down.
void performGranted();

// Rejected ticket: red, one long low tone. The barrier never moves.
void performDenied();

// 409 — another scanner is mid-validation on this same ticket. Amber (both LEDs) and two mid
// tones: this is "try again", not "you are not getting in", and the gate staff should read it that
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
