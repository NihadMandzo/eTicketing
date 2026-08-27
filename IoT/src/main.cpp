// eTicketing — ESP32-CAM gate scanner
//
// Reads a ticket QR, hands the decoded string to eTicketing over the device API key, and performs
// whatever verdict comes back. Two properties are deliberate and load-bearing:
//
//  1. This firmware decides NOTHING about validity. It does not know the HMAC key, does not know
//     which product or sectors this door serves, and does not cache verdicts. It forwards a string
//     and obeys the answer. Everything that determines whether a ticket is good — signature, event,
//     sector, already-used, valid-today, organization — is checked server-side, so re-flashing this
//     board with different constants cannot widen what the gate admits.
//
//  2. It fails CLOSED. No network, a timeout, an unparseable body, a rejected key: the barrier does
//     not move. The only path that opens it is HTTP 200 with isValid = true.

#include <Arduino.h>

#include "camera_qr.h"
#include "config.h"
#include "gate_api.h"
#include "gate_io.h"
#include "wifi_link.h"

namespace {

// Remembers the last code acted on, so one ticket held in front of the lens is one HTTP call
// rather than a burst. Cleared by time, not by a different code, because presenting the SAME
// ticket twice on purpose (to see "already used") has to keep working.
String lastCode;
uint32_t lastCodeAt = 0;

uint32_t lastConfigFetch = 0;
bool halted = false; // Set when the server rejects our key. Terminal until someone reboots us.

void logConfig(const gate_api::GateConfig &config) {
  if (!config.ok) {
    Serial.println("[gate] Nije moguće preuzeti konfiguraciju uređaja.");
    return;
  }

  Serial.println("------------------------------------------------------------");
  Serial.printf("[gate] Uređaj  : %s\n", config.deviceName.c_str());
  Serial.printf("[gate] Proizvod: %s\n", config.productName.c_str());
  if (config.allSectors) {
    Serial.println("[gate] Sektori : SVI sektori ovog proizvoda");
  } else {
    Serial.printf("[gate] Sektori : %s (%u)\n", config.sectorSummary.c_str(), config.sectorCount);
  }
  Serial.println("------------------------------------------------------------");
}

void refreshConfig(bool force) {
  if (!force && millis() - lastConfigFetch < GATE_CONFIG_REFRESH_MS) return;

  lastConfigFetch = millis();
  const gate_api::GateConfig config = gate_api::fetchConfig();
  if (config.ok || force) logConfig(config);
}

bool isDuplicateScan(const String &code) {
  return code == lastCode && millis() - lastCodeAt < GATE_RESCAN_GUARD_MS;
}

void handle(const gate_api::Verdict &verdict) {
  switch (verdict.outcome) {
    case gate_api::Outcome::Valid:
      Serial.printf("[gate] VALIDNA — %s | sektor: %s | %s\n",
                    verdict.message.c_str(), verdict.sectorName.c_str(), verdict.holderEmail.c_str());
      gate_io::performGranted();
      return;

    case gate_api::Outcome::Invalid:
      Serial.printf("[gate] NIJE VALIDNA (%s) — %s\n", verdict.code.c_str(), verdict.message.c_str());
      gate_io::performDenied();
      return;

    case gate_api::Outcome::Retry:
      Serial.printf("[gate] PONOVITE — %s\n", verdict.message.c_str());
      gate_io::performRetry();
      return;

    case gate_api::Outcome::Unauthorized:
      Serial.println("[gate] Ključ uređaja je odbijen. Provjerite GATE_DEVICE_KEY ili status uređaja");
      Serial.println("[gate] u desktop aplikaciji (Ulazni uređaji). Skeniranje je zaustavljeno.");
      halted = true;
      return;

    case gate_api::Outcome::TransportError:
      Serial.printf("[gate] GREŠKA VEZE (HTTP %d) — %s\n", verdict.httpStatus, verdict.message.c_str());
      gate_io::performNetworkError();
      return;
  }
}

} // namespace

void setup() {
  Serial.begin(115200);
  delay(300);
  Serial.println("\n[gate] eTicketing — ulazni skener");

  // First, and before anything that can fail: drive the barrier closed. A gate that reboots
  // mid-shift must come back down, not sit open.
  gate_io::begin();

  if (!camera_qr::begin()) {
    Serial.println("[gate] Kamera nije dostupna — uređaj se ne može koristiti.");
    while (true) gate_io::showFatal();
  }

  wifi_link::begin();
  wifi_link::waitUntilConnected();

  refreshConfig(true);

#if GATE_USE_FLASH
  gate_io::setFlash(true);
#endif

  Serial.println("[gate] Spreman. Prislonite ulaznicu ispred kamere.");
}

void loop() {
  if (halted) {
    gate_io::showFatal();
    return;
  }

  if (!wifi_link::isConnected()) {
    gate_io::setStatusLed(false);
#if GATE_USE_FLASH
    gate_io::setFlash(false);
#endif
    wifi_link::waitUntilConnected();
    // Scope may have changed while this gate was off the network.
    refreshConfig(true);
#if GATE_USE_FLASH
    gate_io::setFlash(true);
#endif
    camera_qr::flush();
    return;
  }

  // Picks up a sector added or removed in the back-office without a re-flash or a reboot. Only the
  // serial log uses the result — the server enforces the scope on every scan regardless — but it is
  // what lets someone at the venue confirm the change actually landed.
  refreshConfig(false);

  String code;
  if (!camera_qr::poll(code)) return;

  if (isDuplicateScan(code)) return;

  lastCode = code;
  lastCodeAt = millis();

  Serial.printf("[gate] Skenirano: %s\n", code.c_str());

  const gate_api::Verdict verdict = gate_api::validate(code);
  handle(verdict);

  // Whatever was decoded while the barrier was moving is stale by now.
  camera_qr::flush();
  lastCodeAt = millis();
}
