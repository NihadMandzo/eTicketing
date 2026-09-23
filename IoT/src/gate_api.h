// The two calls this device makes to eTicketing, both over HTTPS and both authenticated with the
// X-Device-Key header. The server's certificate is checked against the one root in config.h before
// the key is sent; see gate_api.cpp for why a plain-HTTP build is refused outright.
//
// Note what is NOT sent: no product id, no sector ids. The server reads this gate's scope from the
// GateDevice row the key resolves to. That is the point — re-flashing this board with different
// constants cannot make it admit tickets for another event or another sector.
#pragma once

#include <Arduino.h>

namespace gate_api {

// What this gate is guarding, as told by the server. Refreshed periodically, so a scope change
// made in the back-office lands here without anyone touching the hardware.
struct GateConfig {
  bool ok = false;
  String deviceName;
  String productName;
  bool allSectors = false;
  String sectorSummary; // "VIP, Loža" — for the serial log; the server does the real checking.
  uint8_t sectorCount = 0;
};

// How a scan came back. The transport outcome and the ticket verdict are separate on purpose: an
// invalid ticket is a perfectly successful HTTP 200, and only `transportOk == false` means the
// gate could not get an answer at all.
enum class Outcome {
  Valid,          // 200, isValid = true
  Invalid,        // 200, isValid = false — a real answer, show the reason
  Retry,          // 409 — another scanner holds this ticket right now
  Unauthorized,   // 401/403 — the device key is wrong, revoked, or the device was deactivated
  TransportError, // no network, timeout, 5xx, unparseable body
};

struct Verdict {
  Outcome outcome = Outcome::TransportError;
  String code;       // e.g. "ticket.wrong_sector"
  String message;    // Bosnian, ready to show verbatim
  String sectorName;
  String ticketType;
  String holderEmail;
  int httpStatus = 0;        // negative = HTTPClient's own error code, no response at all
  uint32_t elapsedMs = 0;    // request sent to response read, including any TLS handshake
  bool freshConnection = false; // true when this scan opened a new connection (a TLS handshake)
};

GateConfig fetchConfig();

// Closes the kept connection. Called when WiFi drops: a socket from before the outage can look open
// afterwards, and the first scan sent down it would fail for no reason the holder could fix.
void dropConnection();

// True only in a development build (esp32cam-dev) whose GATE_API_BASE_URL is http:// to a LAN
// address. Always false in the real build, which does not compile with an http:// address.
bool usesPlainHttp();

Verdict validate(const String &scannedCode);

} // namespace gate_api
