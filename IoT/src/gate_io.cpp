#include "gate_io.h"

#include <ESP32Servo.h>

#include "config.h"

namespace gate_io {
namespace {

Servo barrier;

void beep(uint16_t onMs, uint16_t offMs, uint8_t times) {
  for (uint8_t i = 0; i < times; i++) {
#if GATE_BUZZER_ACTIVE
    digitalWrite(GATE_PIN_BUZZER, HIGH);
    delay(onMs);
    digitalWrite(GATE_PIN_BUZZER, LOW);
#else
    // Passive piezo: it makes no sound from a steady level, so square-wave it by hand at ~2.7kHz.
    // Done in software rather than with tone()/ledcWriteTone() because every LEDC timer on this
    // board is either taken by the camera's XCLK or reserved for the servo below.
    const uint32_t until = millis() + onMs;
    while (millis() < until) {
      digitalWrite(GATE_PIN_BUZZER, HIGH);
      delayMicroseconds(185);
      digitalWrite(GATE_PIN_BUZZER, LOW);
      delayMicroseconds(185);
    }
#endif
    if (i + 1 < times) delay(offMs);
  }
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

  // Closed before anything else can happen. A gate that reboots must come back down.
  barrier.write(GATE_SERVO_CLOSED_DEG);
  delay(400);
}

void performGranted() {
  setLeds(true, false);
  beep(400, 0, 1);

  barrier.write(GATE_SERVO_OPEN_DEG);
  delay(GATE_SERVO_OPEN_MS);
  barrier.write(GATE_SERVO_CLOSED_DEG);
  delay(400);

  setLeds(false, false);
}

void performDenied() {
  setLeds(false, true);
  beep(120, 90, 3);
  delay(GATE_VERDICT_HOLD_MS);
  setLeds(false, false);
}

void performRetry() {
  // Both LEDs together — visibly not the red-only rejection.
  setLeds(true, true);
  beep(220, 140, 2);
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
  beep(120, 90, 2);
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
