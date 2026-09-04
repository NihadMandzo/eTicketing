#include "debug_server.h"

#include "config.h"

#if GATE_DEBUG_SERVER

#include <WiFi.h>
#include <esp_camera.h>
#include <esp_http_server.h>
#include <img_converters.h>

#include "camera_qr.h"
#include "debug_log.h"

namespace debug_server {
namespace {

httpd_handle_t controlServer = nullptr;
httpd_handle_t streamServer = nullptr;

// A macro rather than a constant so it can be concatenated into the literals below at compile time.
#define GATE_BOUNDARY "gateframe"

constexpr char kStreamContentType[] = "multipart/x-mixed-replace;boundary=" GATE_BOUNDARY;

// The page is deliberately one self-contained string with no external assets: the gate is on a
// venue LAN with no route to a CDN, so anything not inlined here simply would not load.
const char kIndexHtml[] PROGMEM = R"HTML(<!doctype html>
<html lang="bs"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>eTicketing — dijagnostika ulaza</title>
<style>
:root{color-scheme:dark;--bg:#12151c;--panel:#1a1f29;--line:#2b3240;--tx:#e6e9ef;--dim:#98a2b3;
--good:#4ade80;--warn:#fbbf24;--bad:#f87171;--info:#60a5fa}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--tx);font:14px/1.5 system-ui,Segoe UI,Roboto,sans-serif}
header{padding:12px 16px;border-bottom:1px solid var(--line);display:flex;gap:12px;align-items:center;flex-wrap:wrap}
h1{font-size:15px;margin:0;font-weight:650}
.pill{font-size:12px;color:var(--dim);border:1px solid var(--line);border-radius:999px;padding:2px 10px}
main{display:grid;grid-template-columns:minmax(320px,1fr) minmax(320px,1fr);gap:16px;padding:16px;align-items:start}
@media(max-width:860px){main{grid-template-columns:1fr}}
.card{background:var(--panel);border:1px solid var(--line);border-radius:12px;overflow:hidden}
.card h2{font-size:12px;text-transform:uppercase;letter-spacing:.06em;color:var(--dim);
margin:0;padding:10px 14px;border-bottom:1px solid var(--line);font-weight:600}
img{display:block;width:100%;background:#000;min-height:200px}
#log{height:60vh;overflow-y:auto;margin:0;padding:8px 0;font:12px/1.55 ui-monospace,Consolas,monospace}
#log li{list-style:none;padding:3px 14px 3px 26px;position:relative;white-space:pre-wrap;word-break:break-word;border-bottom:1px solid #20252f}
#log li:before{content:"";position:absolute;left:12px;top:9px;width:6px;height:6px;border-radius:50%}
li.info:before{background:var(--info)} li.good:before{background:var(--good)}
li.warn:before{background:var(--warn)} li.bad:before{background:var(--bad)}
li .t{color:var(--dim);margin-right:8px}
.stats{display:flex;gap:18px;padding:10px 14px;border-top:1px solid var(--line);font-size:12px;color:var(--dim)}
.stats b{color:var(--tx);font-variant-numeric:tabular-nums}
.hint{padding:10px 14px;font-size:12px;color:var(--dim);border-top:1px solid var(--line)}
</style></head><body>
<header><h1>eTicketing — dijagnostika ulaza</h1>
<span class="pill" id="host"></span><span class="pill" id="conn">povezivanje…</span></header>
<main>
<section class="card"><h2>Kamera uživo</h2>
<img id="cam" alt="Live feed">
<div class="hint">Ovo je isti senzor koji dekoder koristi, pa dijele kadrove — dok je feed otvoren
dekodiranje je sporije. Zatvorite ovu karticu prije mjerenja brzine skeniranja.</div></section>
<section class="card"><h2>Dnevnik dekodera</h2>
<ul id="log"></ul>
<div class="stats"><span>Dekodirano: <b id="ok">0</b></span>
<span>Neuspjelo: <b id="fail">0</b></span><span>RAM: <b id="heap">–</b></span></div></section>
</main>
<script>
const host=location.hostname;
document.getElementById('host').textContent=host;
document.getElementById('cam').src='http://'+host+':'+STREAM_PORT+'/stream';
let since=0;const log=document.getElementById('log');
function row(e){const li=document.createElement('li');li.className=e.lvl;
const t=document.createElement('span');t.className='t';
t.textContent=new Date(e.ms).toISOString().substr(14,9);
li.appendChild(t);li.appendChild(document.createTextNode(e.msg));return li}
async function tick(){
 try{const r=await fetch('/logs?since='+since);const d=await r.json();
  since=d.next;
  const atBottom=log.scrollHeight-log.scrollTop-log.clientHeight<40;
  for(const e of d.lines)log.appendChild(row(e));
  while(log.childElementCount>400)log.removeChild(log.firstChild);
  if(atBottom)log.scrollTop=log.scrollHeight;
  document.getElementById('ok').textContent=d.decoded;
  document.getElementById('fail').textContent=d.failed;
  document.getElementById('heap').textContent=Math.round(d.heap/1024)+' kB';
  document.getElementById('conn').textContent='povezano';
 }catch(err){document.getElementById('conn').textContent='nema veze s uređajem'}
 setTimeout(tick,1000)}
tick();
</script></body></html>)HTML";

esp_err_t indexHandler(httpd_req_t *req) {
  httpd_resp_set_type(req, "text/html; charset=utf-8");

  // The stream lives on another port, and the page needs to know which — patched in here rather
  // than hardcoded in the HTML so the two stay in step with config.h.
  String page(reinterpret_cast<const __FlashStringHelper *>(kIndexHtml));
  page.replace("STREAM_PORT", String(GATE_DEBUG_PORT + 1));

  return httpd_resp_send(req, page.c_str(), page.length());
}

esp_err_t logsHandler(httpd_req_t *req) {
  uint32_t since = 0;
  char query[48];
  if (httpd_req_get_url_query_str(req, query, sizeof(query)) == ESP_OK) {
    char value[16];
    if (httpd_query_key_value(query, "since", value, sizeof(value)) == ESP_OK) {
      since = strtoul(value, nullptr, 10);
    }
  }

  const camera_qr::Stats stats = camera_qr::stats();

  String body = debug_log::toJson(since);
  // Splice the counters into the object the ring buffer produced, rather than making the log module
  // aware of the decoder.
  body.remove(body.length() - 1);
  body += ",\"decoded\":" + String(stats.decoded);
  body += ",\"failed\":" + String(stats.failed);
  body += ",\"heap\":" + String(ESP.getFreeHeap());
  body += "}";

  httpd_resp_set_type(req, "application/json");
  httpd_resp_set_hdr(req, "Cache-Control", "no-store");
  return httpd_resp_send(req, body.c_str(), body.length());
}

/// <summary>Drop a grayscale frame to half width and half height by point-sampling every second
/// pixel of every second row. One byte per pixel makes this a plain copy loop with no arithmetic,
/// which matters because the point is to spend less time here than the JPEG encoder would spend on
/// the four times as many pixels. Averaging would be prettier and is not worth it: this image is
/// for aiming the lens, and the decoder never sees it.</summary>
void halveGrayscale(const uint8_t *src, int width, int height, uint8_t *dst) {
  const int dw = width / 2;
  const int dh = height / 2;
  for (int y = 0; y < dh; y++) {
    const uint8_t *row = src + static_cast<size_t>(y) * 2 * width;
    uint8_t *out = dst + static_cast<size_t>(y) * dw;
    for (int x = 0; x < dw; x++) out[x] = row[x * 2];
  }
}

/// <summary>MJPEG. The sensor is configured for GRAYSCALE so quirc can read it, which means there is
/// no hardware JPEG path — every streamed frame is compressed in software on the CPU. That encode is
/// the single most expensive thing this server does, and at VGA it is what makes the feed lag.
///
/// So the frame is halved to QVGA first (a quarter of the pixels, hence roughly a quarter of the
/// encode time) and the camera buffer is handed back BEFORE encoding rather than after — the
/// decoder gets it back in microseconds instead of waiting out the compression. The decoder itself
/// keeps reading full VGA frames; only this preview is downscaled.
/// </summary>
esp_err_t streamHandler(httpd_req_t *req) {
  char partHeader[80];
  uint8_t *scaled = nullptr;
  size_t scaledCap = 0;

  esp_err_t res = httpd_resp_set_type(req, kStreamContentType);
  if (res != ESP_OK) return res;

  httpd_resp_set_hdr(req, "Access-Control-Allow-Origin", "*");
  httpd_resp_set_hdr(req, "Cache-Control", "no-store");

#if GATE_DEBUG_STREAM_PROFILE
  // Where the per-frame milliseconds actually go. Averaged over a window rather than logged per
  // frame, because logging every frame would itself dominate the measurement.
  uint32_t profFrames = 0, profGet = 0, profScale = 0, profEnc = 0, profSend = 0, profBytes = 0;
  uint32_t profWindowStart = millis();
#endif

  while (true) {
#if GATE_DEBUG_STREAM_PROFILE
    const uint32_t tGet0 = micros();
#endif
    camera_fb_t *fb = esp_camera_fb_get();
    if (fb == nullptr) {
      res = ESP_FAIL;
      break;
    }
#if GATE_DEBUG_STREAM_PROFILE
    const uint32_t tGet1 = micros();
    uint32_t tScaleUs = 0, tEncUs = 0;
#endif

    uint8_t *jpg = nullptr;
    size_t jpgLen = 0;
    bool converted = false;

#if GATE_DEBUG_STREAM_HALVE
    if (fb->format == PIXFORMAT_GRAYSCALE && fb->width >= 2 && fb->height >= 2) {
      const int dw = fb->width / 2;
      const int dh = fb->height / 2;
      const size_t need = static_cast<size_t>(dw) * dh;

      // Grown once per connection and kept for its lifetime; PSRAM because internal DRAM is spoken
      // for by WiFi and quirc.
      if (scaled == nullptr || scaledCap < need) {
        free(scaled);
        scaled = static_cast<uint8_t *>(ps_malloc(need));
        scaledCap = (scaled != nullptr) ? need : 0;
      }

      if (scaled != nullptr) {
        const uint32_t tScale0 = micros();
        halveGrayscale(fb->buf, fb->width, fb->height, scaled);
        esp_camera_fb_return(fb);
        fb = nullptr;
        const uint32_t tScale1 = micros();
        converted = fmt2jpg(scaled, need, dw, dh, PIXFORMAT_GRAYSCALE, GATE_DEBUG_JPEG_QUALITY,
                            &jpg, &jpgLen);
#if GATE_DEBUG_STREAM_PROFILE
        tScaleUs = tScale1 - tScale0;
        tEncUs = micros() - tScale1;
#else
        (void)tScale0;
        (void)tScale1;
#endif
      }
    }
#endif

    // Either downscaling is off, the format was not grayscale, or PSRAM was exhausted. Note this
    // path holds the camera buffer across the encode, where the branch above hands it back first.
    if (fb != nullptr) {
      const uint32_t tEnc0 = micros();
      converted = frame2jpg(fb, GATE_DEBUG_JPEG_QUALITY, &jpg, &jpgLen);
      esp_camera_fb_return(fb);
#if GATE_DEBUG_STREAM_PROFILE
      tEncUs = micros() - tEnc0;
#else
      (void)tEnc0;
#endif
    }

    if (!converted) {
      res = ESP_FAIL;
      break;
    }

    const int headerLen = snprintf(partHeader, sizeof(partHeader),
                                   "\r\n--" GATE_BOUNDARY "\r\nContent-Type: image/jpeg\r\n"
                                   "Content-Length: %u\r\n\r\n",
                                   static_cast<unsigned>(jpgLen));

#if GATE_DEBUG_STREAM_PROFILE
    const uint32_t tSend0 = micros();
#endif

    res = httpd_resp_send_chunk(req, partHeader, headerLen);
    if (res == ESP_OK) {
      res = httpd_resp_send_chunk(req, reinterpret_cast<const char *>(jpg), jpgLen);
    }

#if GATE_DEBUG_STREAM_PROFILE
    profSend += micros() - tSend0;
    profGet += tGet1 - tGet0;
    profScale += tScaleUs;
    profEnc += tEncUs;
    profBytes += jpgLen;
    if (++profFrames >= 25) {
      const uint32_t elapsed = millis() - profWindowStart;
      debug_log::addf(debug_log::Level::Info,
                      "[prof] %u kadrova/%ums | get %ums enc %ums skal %ums slanje %ums | %uB/kadar",
                      profFrames, elapsed, profGet / 1000 / profFrames, profEnc / 1000 / profFrames,
                      profScale / 1000 / profFrames, profSend / 1000 / profFrames,
                      profBytes / profFrames);
      profFrames = profGet = profScale = profEnc = profSend = profBytes = 0;
      profWindowStart = millis();
    }
#endif

    free(jpg);

    // Browser closed the tab: stop, and give the frames back to the decoder.
    if (res != ESP_OK) break;

    // Yield so the decoder task is not starved of frames any harder than it already is.
    vTaskDelay(pdMS_TO_TICKS(GATE_DEBUG_STREAM_DELAY_MS));
  }

  free(scaled);
  return res;
}

} // namespace

void begin() {
  httpd_config_t config = HTTPD_DEFAULT_CONFIG();
  config.server_port = GATE_DEBUG_PORT;
  config.ctrl_port = GATE_DEBUG_PORT;      // must differ between the two instances
  config.stack_size = 8192;                // the index page is assembled as a String on this stack
  config.lru_purge_enable = true;

  httpd_uri_t index = {"/", HTTP_GET, indexHandler, nullptr};
  httpd_uri_t logs = {"/logs", HTTP_GET, logsHandler, nullptr};

  if (httpd_start(&controlServer, &config) == ESP_OK) {
    httpd_register_uri_handler(controlServer, &index);
    httpd_register_uri_handler(controlServer, &logs);
  } else {
    controlServer = nullptr;
    debug_log::addf(debug_log::Level::Warn, "[dbg] Web server se nije pokrenuo (port %d).", GATE_DEBUG_PORT);
    return;
  }

  config.server_port = GATE_DEBUG_PORT + 1;
  config.ctrl_port = GATE_DEBUG_PORT + 1;

  httpd_uri_t stream = {"/stream", HTTP_GET, streamHandler, nullptr};
  if (httpd_start(&streamServer, &config) == ESP_OK) {
    httpd_register_uri_handler(streamServer, &stream);
  } else {
    streamServer = nullptr;
    debug_log::addf(debug_log::Level::Warn, "[dbg] Stream server se nije pokrenuo (port %d).",
                    GATE_DEBUG_PORT + 1);
  }

  debug_log::addf(debug_log::Level::Info, "[dbg] Dijagnostika: http://%s:%d/",
                  WiFi.localIP().toString().c_str(), GATE_DEBUG_PORT);
}

bool isRunning() { return controlServer != nullptr; }

} // namespace debug_server

#else // GATE_DEBUG_SERVER

namespace debug_server {
void begin() {}
bool isRunning() { return false; }
} // namespace debug_server

#endif
