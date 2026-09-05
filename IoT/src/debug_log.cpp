#include "debug_log.h"

#include <freertos/FreeRTOS.h>
#include <freertos/semphr.h>

namespace debug_log {
namespace {

constexpr size_t kCapacity = 80;   // ~4 minutes of a busy gate; the web panel keeps its own history
constexpr size_t kMaxLine = 200;

struct Entry {
  uint32_t seq;
  uint32_t ms;
  Level level;
  char msg[kMaxLine];
};

Entry entries[kCapacity];
size_t writeIndex = 0;
uint32_t nextSeq = 1;
SemaphoreHandle_t mutex = nullptr;

const char *levelName(Level level) {
  switch (level) {
    case Level::Good: return "good";
    case Level::Warn: return "warn";
    case Level::Bad:  return "bad";
    default:          return "info";
  }
}

/// <summary>JSON string escaping. A scanned payload is attacker-supplied text that lands in this
/// ring and then in a browser, so quotes, backslashes and control characters have to be neutralised
/// here rather than trusted to be absent.</summary>
void appendEscaped(String &out, const char *text) {
  for (const char *p = text; *p; p++) {
    const unsigned char c = static_cast<unsigned char>(*p);
    switch (c) {
      case '"':  out += "\\\""; break;
      case '\\': out += "\\\\"; break;
      case '\n': out += "\\n";  break;
      case '\r': out += "\\r";  break;
      case '\t': out += "\\t";  break;
      default:
        if (c < 0x20) {
          char buf[7];
          snprintf(buf, sizeof(buf), "\\u%04x", c);
          out += buf;
        } else {
          out += static_cast<char>(c);
        }
    }
  }
}

} // namespace

void begin() {
  if (mutex == nullptr) mutex = xSemaphoreCreateMutex();
}

void addf(Level level, const char *fmt, ...) {
  char line[kMaxLine];
  va_list args;
  va_start(args, fmt);
  vsnprintf(line, sizeof(line), fmt, args);
  va_end(args);

  Serial.println(line);

  if (mutex == nullptr) return;
  if (xSemaphoreTake(mutex, pdMS_TO_TICKS(50)) != pdTRUE) return;

  Entry &entry = entries[writeIndex];
  entry.seq = nextSeq++;
  entry.ms = millis();
  entry.level = level;
  strncpy(entry.msg, line, kMaxLine - 1);
  entry.msg[kMaxLine - 1] = '\0';
  writeIndex = (writeIndex + 1) % kCapacity;

  xSemaphoreGive(mutex);
}

String toJson(uint32_t since) {
  String out;
  out.reserve(2048);
  out += "{\"next\":";

  if (mutex == nullptr || xSemaphoreTake(mutex, pdMS_TO_TICKS(200)) != pdTRUE) {
    out += String(since) + ",\"lines\":[]}";
    return out;
  }

  out += String(nextSeq);
  out += ",\"lines\":[";

  bool first = true;
  // Oldest-first from the write cursor, so the browser can append without sorting.
  for (size_t i = 0; i < kCapacity; i++) {
    const Entry &entry = entries[(writeIndex + i) % kCapacity];
    if (entry.seq == 0 || entry.seq < since) continue;

    if (!first) out += ',';
    first = false;

    out += "{\"seq\":";
    out += String(entry.seq);
    out += ",\"ms\":";
    out += String(entry.ms);
    out += ",\"lvl\":\"";
    out += levelName(entry.level);
    out += "\",\"msg\":\"";
    appendEscaped(out, entry.msg);
    out += "\"}";
  }

  out += "]}";
  xSemaphoreGive(mutex);
  return out;
}

} // namespace debug_log
