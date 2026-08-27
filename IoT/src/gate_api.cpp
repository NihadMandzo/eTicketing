#include "gate_api.h"

#include <ArduinoJson.h>
#include <HTTPClient.h>
#include <WiFi.h>

#include <initializer_list>

#include "config.h"

namespace gate_api {
namespace {

void applyCommonHeaders(HTTPClient &http) {
  http.addHeader("X-Device-Key", GATE_DEVICE_KEY);
  http.addHeader("Accept", "application/json");
  http.setTimeout(GATE_HTTP_TIMEOUT_MS);
  http.setConnectTimeout(GATE_HTTP_TIMEOUT_MS);
  // A gate must never sit blocked on a redirect chain it cannot reason about.
  http.setFollowRedirects(HTTPC_DISABLE_FOLLOW_REDIRECTS);
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

GateConfig fetchConfig() {
  GateConfig config;

  if (WiFi.status() != WL_CONNECTED) return config;

  WiFiClient client;
  HTTPClient http;

  const String url = String(GATE_API_BASE_URL) + "/gate/config";
  if (!http.begin(client, url)) {
    Serial.println("[api] Neispravan GATE_API_BASE_URL.");
    return config;
  }

  applyCommonHeaders(http);
  const int status = http.GET();
  const String body = http.getString();
  http.end();

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

  WiFiClient client;
  HTTPClient http;

  const String url = String(GATE_API_BASE_URL) + "/gate/validate";
  if (!http.begin(client, url)) {
    verdict.message = "Neispravan URL servera.";
    return verdict;
  }

  applyCommonHeaders(http);
  http.addHeader("Content-Type", "application/json");

  // Serialised through ArduinoJson rather than string-concatenated: a scanned payload is
  // attacker-supplied text, and hand-built JSON is exactly how a crafted QR gets to inject fields
  // into the request body.
  JsonDocument requestDoc;
  requestDoc["code"] = scannedCode;
  String requestBody;
  serializeJson(requestDoc, requestBody);

  verdict.httpStatus = http.POST(requestBody);
  const String body = http.getString();
  http.end();

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

    default:
      verdict.message = "Greška servera (HTTP " + String(verdict.httpStatus) + ").";
      return verdict;
  }
}

} // namespace gate_api
