#include "gate_io.h"

#include <ESP32Servo.h>

#include "config.h"

namespace gate_io {
namespace {

Servo barrier;

/// <summary>One tone. Software-timed on purpose: tone() and ledcWriteTone() both go through the
/// LEDC peripheral, and on arduino-esp32 2.x tone() hardcodes channel 0 — the very channel the
/// camera driver assigns to the OV2640's XCLK. Calling it here would reconfigure the sensor's
/// clock mid-run (green frames, then nothing). Bit-banging the pin touches no shared peripheral
/// at all, which is why the gate can have both a camera and a pitched buzzer.</summary>
void beepAt(uint16_t hz, uint16_t ms) {
#if GATE_BUZZER_ACTIVE
  // Active buzzer: pitch is fixed in hardware, so hz is meaningless and only duration carries.
  (void)hz;
  digitalWrite(GATE_PIN_BUZZER, HIGH);
  delay(ms);
  digitalWrite(GATE_PIN_BUZZER, LOW);
#else
  const uint32_t halfPeriodUs = 500000UL / hz;
  const uint32_t until = millis() + ms;
  while (millis() < until) {
    digitalWrite(GATE_PIN_BUZZER, HIGH);
    delayMicroseconds(halfPeriodUs);
    digitalWrite(GATE_PIN_BUZZER, LOW);
    delayMicroseconds(halfPeriodUs);
  }
#endif
}

void beepTwice(uint16_t hz, uint16_t onMs, uint16_t gapMs) {
  beepAt(hz, onMs);
  delay(gapMs);
  beepAt(hz, onMs);
}

/// <summary>Move the barrier for a fixed time, then stop it. The whole vocabulary of a
/// continuous-rotation servo: a pulse width is a speed and a direction, so distance is expressed
/// as duration and every movement MUST be terminated with the neutral pulse or the arm keeps
/// turning.</summary>
void drive(int microseconds, uint32_t travelMs) {
  barrier.writeMicroseconds(microseconds);
  delay(travelMs);
  barrier.writeMicroseconds(GATE_SERVO_STOP_US);
}

void setLeds(bool green, bool red) {
  digitalWrite(GATE_PIN_LED_GREEN, green ? HIGH : LOW);
  digitalWrite(GATE_PIN_LED_RED, red ? HIGH : LOW);
}

} // namespace

void begin() {
  pinMode(GATE_PIN_LED_GREEN, OUTPUT);
  pinMode(GATE_PIN_LED_RED, OUTPUT);
  pinMode(GATE_PIN_BUZZER, OUTPUT);
  pinMode(GATE_PIN_STATUS, OUTPUT);
  pinMode(GATE_PIN_FLASH, OUTPUT);

  setLeds(false, false);
  digitalWrite(GATE_PIN_BUZZER, LOW);
  digitalWrite(GATE_PIN_FLASH, LOW);
  setStatusLed(false);

  // Hand ESP32Servo timers 1-3 and never timer 0: the esp32-camera driver claims LEDC timer 0 /
  // channel 0 for the sensor's XCLK. Letting the servo library allocate greedily means whichever
  // of the two initialises second silently reconfigures the other's clock — the classic symptom
  // being a camera that returns green frames the moment the barrier first moves.
  ESP32PWM::allocateTimer(1);
  ESP32PWM::allocateTimer(2);
  ESP32PWM::allocateTimer(3);

  barrier.setPeriodHertz(50);
  barrier.attach(GATE_PIN_SERVO, GATE_SERVO_MIN_US, GATE_SERVO_MAX_US);

  // Neutral before anything else: attach() starts generating pulses immediately, and on a
  // continuous-rotation servo an undefined pulse means an undefined rotation.
  barrier.writeMicroseconds(GATE_SERVO_STOP_US);
  delay(500);

  // Then re-datum. A reboot mid-shift may have left the arm anywhere, and this servo cannot say
  // where — so drive it into the mechanical closed stop for longer than a normal close takes.
  // From any starting position that ends closed, which is the only safe state to boot into.
  drive(GATE_SERVO_CLOSE_US, GATE_SERVO_REDATUM_MS);
}

void performGranted() {
  setLeds(true, false);

  // Rising two-tone, sounded BEFORE the barrier moves: the holder is still looking at the scanner
  // at this point, and the servo burst is loud enough to mask a beep played under it.
  beepAt(2000, 150);
  delay(100);
  beepAt(3000, 200);

  drive(GATE_SERVO_OPEN_US, GATE_SERVO_OPEN_TRAVEL_MS);
  delay(GATE_OPEN_HOLD_MS);
  // Exact close travel, not the boot overshoot: equal open/close returns the arm to where it
  // started without grinding into the stop on every single admission.
  drive(GATE_SERVO_CLOSE_US, GATE_SERVO_CLOSE_TRAVEL_MS);

  setLeds(false, false);
}

void performDenied() {
  setLeds(false, true);
  beepAt(700, 500);
  delay(GATE_VERDICT_HOLD_MS);
  setLeds(false, false);
}

void performRetry() {
  // Both LEDs, and a mid tone that is neither the rising chime nor the low buzz — this is
  // "try again", not "you are not getting in".
  setLeds(true, true);
  beepTwice(1200, 200, 140);
  delay(GATE_VERDICT_HOLD_MS);
  setLeds(false, false);
}

void performNetworkError() {
  for (uint8_t i = 0; i < 3; i++) {
    setLeds(false, true);
    delay(120);
    setLeds(false, false);
    delay(120);
  }
  // Same low pitch as a rejection but stuttered, so a broken gate is audibly distinct from a
  // stream of bad tickets without needing anyone to read the serial log.
  beepTwice(700, 100, 90);
}

void setStatusLed(bool on) {
  // Onboard LED is wired to 3V3, so it lights when the pin is pulled LOW.
  digitalWrite(GATE_PIN_STATUS, on ? LOW : HIGH);
}

void tickStatusHeartbeat(uint32_t periodMs) {
  setStatusLed((millis() / periodMs) % 2 == 0);
}

void showFatal() {
  setLeds(false, true);
  delay(700);
  setLeds(false, false);
  delay(700);
}

void setFlash(bool on) {
  digitalWrite(GATE_PIN_FLASH, on ? HIGH : LOW);
}

} // namespace gate_io
