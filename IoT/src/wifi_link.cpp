#include "wifi_link.h"

#include <WiFi.h>

#include "config.h"
#include "gate_io.h"

namespace wifi_link {

void begin() {
  WiFi.mode(WIFI_STA);

  // The ESP32's default is to write credentials into flash on every connect and to drop into
  // modem sleep between beacons. Neither helps a mains-powered gate, and modem sleep in
  // particular adds latency to the one HTTP call anyone is waiting on.
  WiFi.persistent(false);
  WiFi.setSleep(false);
  WiFi.setAutoReconnect(true);

  WiFi.begin(GATE_WIFI_SSID, GATE_WIFI_PASSWORD);
}

void waitUntilConnected() {
  if (isConnected()) return;

  Serial.printf("[wifi] Povezivanje na '%s'", GATE_WIFI_SSID);
  uint32_t lastAttempt = millis();

  while (!isConnected()) {
    gate_io::tickStatusHeartbeat(250);

    // A stalled association never recovers on its own; kick it periodically rather than waiting
    // forever with the gate dark.
    if (millis() - lastAttempt > GATE_WIFI_RETRY_MS) {
      Serial.print(".");
      WiFi.disconnect();
      WiFi.begin(GATE_WIFI_SSID, GATE_WIFI_PASSWORD);
      lastAttempt = millis();
    }
    delay(50);
  }

  gate_io::setStatusLed(true);
  Serial.printf("\n[wifi] Povezan. IP: %s, RSSI: %d dBm\n", WiFi.localIP().toString().c_str(), WiFi.RSSI());
}

bool isConnected() { return WiFi.status() == WL_CONNECTED; }

String localIp() { return WiFi.localIP().toString(); }

} // namespace wifi_link
