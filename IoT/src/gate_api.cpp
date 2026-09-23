#include "gate_api.h"

#include <ArduinoJson.h>
#include <HTTPClient.h>
#include <WiFi.h>
#include <WiFiClientSecure.h>

#include <initializer_list>

#include "build_mode.h"

namespace gate_api {
namespace {

// ------------------------------------------------------------------------------ build-time guards

// C++11 constexpr (the toolchain builds gnu++11), hence single-expression functions and recursion
// rather than loops.
constexpr bool startsWith(const char *text, const char *prefix) {
  return *prefix == '\0' || (*text == *prefix && startsWith(text + 1, prefix + 1));
}

constexpr bool isDigit(char c) { return c >= '0' && c <= '9'; }

/// <summary>The second octet of a 172.x.x.x address, followed by its dot, is 16 to 31.</summary>
constexpr bool isPrivate172SecondOctet(const char *octet) {
  return (octet[0] == '1' && octet[1] >= '6' && octet[1] <= '9' && octet[2] == '.') ||
         (octet[0] == '2' && isDigit(octet[1]) && octet[2] == '.') ||
         (octet[0] == '3' && (octet[1] == '0' || octet[1] == '1') && octet[2] == '.');
}

/// <summary>http:// to a private IPv4 address (RFC 1918: 10/8, 172.16/12, 192.168/16), judged on
/// the literal. A host NAME is never accepted, because nothing at compile time can say where it
/// resolves.</summary>
constexpr bool isPrivateLanHttpUrl(const char *url) {
  return startsWith(url, "http://10.") || startsWith(url, "http://192.168.") ||
         (startsWith(url, "http://172.") && isPrivate172SecondOctet(url + 11));
}

constexpr bool kPlainHttp = startsWith(GATE_API_BASE_URL, "http://");

// The device key rides in a header on every request, so a plain-HTTP gate hands it to anyone on
// the network. That is not left to a comment in config.h: the real gate does not build with an
// http:// address at all, and the development build accepts one only on the local network. A
// plain-HTTP build aimed at a real server would send the key in clear with its very first request,
// before any server could refuse it, so the build is the only place the rule can be enforced in
// time.
static_assert(kPlainHttp || startsWith(GATE_API_BASE_URL, "https://"),
              "GATE_API_BASE_URL must start with https:// - see config.example.h");
static_assert(!kPlainHttp || GATE_DEV_BUILD,
              "The real gate (esp32cam) talks HTTPS only. Plain http:// is for local development: "
              "pio run -e esp32cam-dev");
static_assert(!kPlainHttp || isPrivateLanHttpUrl(GATE_API_BASE_URL),
              "Plain http:// is allowed only to a private LAN address (10.x, 172.16-31.x, "
              "192.168.x), never to a real server");

// The pinned root is needed only for https://. A plain-HTTP development build never opens a TLS
// connection, so an older config.h without the block still builds for it.
#ifdef GATE_API_ROOT_CA
constexpr bool kRootCaConfigured = true;
#else
constexpr bool kRootCaConfigured = false;
#define GATE_API_ROOT_CA ""
#endif
static_assert(kPlainHttp || kRootCaConfigured,
              "GATE_API_ROOT_CA is missing from config.h - an https:// address needs the root "
              "certificate block from config.example.h");

#ifndef GATE_TLS_REUSE_MS
#define GATE_TLS_REUSE_MS 60000
#endif

// ---------------------------------------------------------------------------------- connection

// One connection to the server, kept open across scans: TLS, except in a development build aimed at
// a LAN address over plain HTTP. A handshake costs far more than a request, and a queue at the door
// should pay for it once, not once per ticket.
//
// Both objects live here rather than on a request's stack for a reason that is easy to undo by
// accident: HTTPClient's destructor stops the client it was handed, so a local HTTPClient would
// close the very connection setReuse() exists to keep.
WiFiClientSecure tlsClient;
#if GATE_DEV_BUILD
// Development build only, and used only when GATE_API_BASE_URL is http:// to a LAN address. The
// real build does not contain a plain-HTTP client at all.
WiFiClient plainClient;
#endif
HTTPClient http;
bool tlsConfigured = false;
uint32_t lastExchangeAt = 0;

WiFiClient &connection() {
#if GATE_DEV_BUILD
  if (kPlainHttp) return plainClient;
#endif
  return tlsClient;
}

void configureTls() {
  if (tlsConfigured) return;

  // Trust this one root and nothing else. Never setInsecure(): it still encrypts, but without
  // checking who is on the other end, so a fake access point at the venue would be handed the
  // device key just the same.
  tlsClient.setCACert(GATE_API_ROOT_CA);

  // The library default is 120 s, for which the scan loop would sit blocked with the door shut.
  tlsClient.setHandshakeTimeout(GATE_HTTP_TIMEOUT_MS / 1000);

  http.setReuse(true);
  tlsConfigured = true;
}

void applyCommonHeaders(HTTPClient &client) {
  client.addHeader("X-Device-Key", GATE_DEVICE_KEY);
  client.addHeader("Accept", "application/json");
  client.setTimeout(GATE_HTTP_TIMEOUT_MS);
  client.setConnectTimeout(GATE_HTTP_TIMEOUT_MS);
  // A gate must never sit blocked on a redirect chain it cannot reason about.
  client.setFollowRedirects(HTTPC_DISABLE_FOLLOW_REDIRECTS);
}

/// <summary>Points the shared client at `path` on the kept connection, or on a new one when there
/// is none worth keeping. `freshConnection` reports which, so the caller can tell a scan that paid
/// for a handshake from one that did not. False only for a URL the client cannot parse.</summary>
bool beginRequest(const char *path, bool &freshConnection) {
  configureTls();

  // A connection idle for longer than GATE_TLS_REUSE_MS may already have been closed by the
  // server. Sending a scan down it would fail that scan, and the person at the door would get a
  // network error for a ticket that was fine — so start that scan on a new connection instead.
  if (connection().connected() && millis() - lastExchangeAt > GATE_TLS_REUSE_MS) {
    connection().stop();
  }
  freshConnection = !connection().connected();

  if (!http.begin(connection(), String(GATE_API_BASE_URL) + path)) return false;
  applyCommonHeaders(http);
  return true;
}

/// <summary>Ends one exchange. The connection stays open when the server allowed keep-alive; a
/// failed exchange leaves it in an unknown state, so it is dropped and the next scan starts
/// clean.</summary>
void endRequest(int status) {
  http.end();
  if (status < 0) {
    connection().stop();
  } else {
    lastExchangeAt = millis();
  }
}

/// <summary>Logs why a connection could not be opened. HTTPClient reports every failed connect as
/// the same HTTPC_ERROR_CONNECTION_REFUSED; the useful detail — a certificate the pinned root did
/// not sign, a host name the certificate was not issued for, no memory for the handshake — is
/// only in mbedTLS's last error. That value holds the socket number after a successful connect and
/// -1 for a TCP-level failure, so only codes below -1 are mbedTLS's own.</summary>
void logConnectFailure(const char *path) {
  char reason[128];
  const int error = kPlainHttp ? 0 : tlsClient.lastError(reason, sizeof(reason));
  if (error < -1) {
    Serial.printf("[api] %s: TLS veza odbijena — %s (-0x%04X). Slobodna interna memorija: %u B, "
                  "najveći blok: %u B.\n",
                  path, reason, -error, ESP.getFreeHeap(), ESP.getMaxAllocHeap());
  } else {
    Serial.printf("[api] %s: server nije dostupan na mreži.\n", path);
  }
}

/// <summary>A JSON string field as a String, with an absent, null or non-string value collapsing
/// to empty. Every field the server may legitimately omit (sectorName and holderEmail are null for
/// a printed ticket, for instance) goes through here.</summary>
String str(JsonVariantConst value) {
  return value.is<const char *>() ? String(value.as<const char *>()) : String();
}

/// <summary>Pull the first of several possible keys out of a response body. The validate endpoint
/// answers with { code, message } on both the success shape and the Result-failure shape, while
/// FluentValidation rejections come back as an RFC7807 ProblemDetails with { title, errors }
/// instead — so the firmware has to tolerate both rather than assume one.</summary>
String firstString(JsonDocument &doc, std::initializer_list<const char *> keys) {
  for (const char *key : keys) {
    const String value = str(doc[key]);
    if (!value.isEmpty()) return value;
  }
  return String();
}

} // namespace

void dropConnection() {
  connection().stop();
}

bool usesPlainHttp() { return kPlainHttp; }

GateConfig fetchConfig() {
  GateConfig config;

  if (WiFi.status() != WL_CONNECTED) return config;

  bool freshConnection = false;
  if (!beginRequest("/gate/config", freshConnection)) {
    Serial.println("[api] Neispravan GATE_API_BASE_URL.");
    return config;
  }

  const int status = http.GET();
  const String body = http.getString();
  endRequest(status);

  if (status == HTTPC_ERROR_CONNECTION_REFUSED) logConnectFailure("/gate/config");

  if (status != 200) {
    Serial.printf("[api] /gate/config -> HTTP %d: %s\n", status, body.c_str());
    return config;
  }

  JsonDocument doc;
  if (deserializeJson(doc, body)) {
    Serial.println("[api] /gate/config vratio neispravan JSON.");
    return config;
  }

  config.ok = true;
  config.deviceName = str(doc["deviceName"]);
  config.productName = str(doc["productName"]);
  config.allSectors = doc["allSectors"] | false;

  for (JsonObjectConst sector : doc["sectors"].as<JsonArrayConst>()) {
    if (config.sectorCount++) config.sectorSummary += ", ";
    config.sectorSummary += str(sector["name"]);
  }

  return config;
}

Verdict validate(const String &scannedCode) {
  Verdict verdict;

  if (WiFi.status() != WL_CONNECTED) {
    verdict.message = "Nema veze sa serverom.";
    return verdict;
  }

  if (!beginRequest("/gate/validate", verdict.freshConnection)) {
    verdict.message = "Neispravan URL servera.";
    return verdict;
  }

  http.addHeader("Content-Type", "application/json");

  // Serialised through ArduinoJson rather than string-concatenated: a scanned payload is
  // attacker-supplied text, and hand-built JSON is exactly how a crafted QR gets to inject fields
  // into the request body.
  JsonDocument requestDoc;
  requestDoc["code"] = scannedCode;
  String requestBody;
  serializeJson(requestDoc, requestBody);

  const uint32_t startedAt = millis();
  verdict.httpStatus = http.POST(requestBody);
  const String body = http.getString();
  verdict.elapsedMs = millis() - startedAt;
  endRequest(verdict.httpStatus);

  JsonDocument doc;
  const bool parsed = deserializeJson(doc, body) == DeserializationError::Ok;

  switch (verdict.httpStatus) {
    case 200:
      if (!parsed) {
        verdict.message = "Neispravan odgovor servera.";
        return verdict;
      }
      // The one field that decides whether the barrier moves. Absent or non-boolean is treated as
      // false — a malformed success body must never be read as permission to open.
      verdict.outcome = (doc["isValid"] | false) ? Outcome::Valid : Outcome::Invalid;
      verdict.code = str(doc["code"]);
      verdict.message = str(doc["message"]);
      verdict.sectorName = str(doc["sectorName"]);
      verdict.ticketType = str(doc["ticketTypeName"]);
      verdict.holderEmail = str(doc["holderEmail"]);
      return verdict;

    case 409:
      verdict.outcome = Outcome::Retry;
      verdict.code = parsed ? firstString(doc, {"code"}) : "ticket.validation_in_progress";
      verdict.message = parsed ? firstString(doc, {"message"}) : "";
      if (verdict.message.isEmpty()) verdict.message = "Ulaznica se provjerava na drugom uređaju.";
      return verdict;

    case 401:
    case 403:
      verdict.outcome = Outcome::Unauthorized;
      verdict.message = "Ključ uređaja nije prihvaćen.";
      return verdict;

    case 400:
      // A 400 here is the request-shape validator, not a ticket verdict — an empty or absurdly
      // long scan. Surfaced as a rejection so the person at the door still gets a red card.
      verdict.outcome = Outcome::Invalid;
      verdict.code = "gate.bad_request";
      verdict.message = parsed ? firstString(doc, {"message", "title"}) : "";
      if (verdict.message.isEmpty()) verdict.message = "Kod nije prihvaćen.";
      return verdict;

    case HTTPC_ERROR_CONNECTION_REFUSED:
      // Refused before a single byte of the request was sent — including a certificate this gate
      // does not trust, which is exactly when the device key must stay on the device.
      logConnectFailure("/gate/validate");
      verdict.message = kPlainHttp ? "Veza sa serverom nije uspostavljena."
                                   : "Sigurna veza sa serverom nije uspostavljena.";
      return verdict;

    default:
      verdict.message = "Greška servera (HTTP " + String(verdict.httpStatus) + ").";
      return verdict;
  }
}

} // namespace gate_api
