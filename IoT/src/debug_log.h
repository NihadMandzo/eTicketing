// A small in-memory ring of recent log lines, so the same events that scroll past on the serial
// monitor can also be pulled by a browser over WiFi. Everything written here is mirrored to Serial,
// so a tethered board loses nothing by this existing.
//
// Written from the main loop and read from the HTTP server task, hence the mutex. Lines are capped
// and the ring overwrites silently: this is a debugging aid, not an audit trail — the authoritative
// record of a validation is the server-side log in Ticketing.
#pragma once

#include <Arduino.h>

namespace debug_log {

// Severity drives nothing but the colour of the dot in the web UI.
enum class Level { Info, Good, Warn, Bad };

void begin();

// printf-style. Always echoes to Serial, with the same text the web panel shows.
void addf(Level level, const char *fmt, ...);

/// <summary>Lines newer than `since`, as a JSON object: {"next":N,"lines":[{"seq":..,"ms":..,
/// "lvl":"..","msg":".."}]}. The browser passes the previous `next` back so it only ever pulls
/// what it has not seen.</summary>
String toJson(uint32_t since);

} // namespace debug_log
