#include "camera_qr.h"

#include <ESP32QRCodeReader.h>
#include <esp_camera.h>

#include "config.h"
#include "debug_log.h"

namespace camera_qr {
namespace {

ESP32QRCodeReader reader(CAMERA_MODEL_AI_THINKER);

uint32_t decodedCount = 0;
uint32_t failedCount = 0;

} // namespace

bool begin() {
  // quirc's working buffers plus a VGA grayscale frame are several hundred kilobytes — far more
  // than the ~300KB of internal DRAM left after WiFi. Without PSRAM the decoder allocates nothing
  // and every scan silently fails, so say so loudly here rather than at the door.
  if (!psramFound()) {
    debug_log::addf(debug_log::Level::Bad, "[cam] PSRAM nije pronađen — čitanje QR koda neće raditi.");
    return false;
  }

  debug_log::addf(debug_log::Level::Info, "[cam] Slobodan PSRAM: %u B, heap: %u B",
                  ESP.getFreePsram(), ESP.getFreeHeap());

#if GATE_QR_LIBRARY_DEBUG
  // The library's own frame-by-frame trace. Ours already reports failed decodes with quirc's
  // reason, so this is only worth enabling when the decoder itself is under suspicion.
  reader.setDebug(true);
#endif

  reader.setup();

  // The library hardcodes fb_count = 1, which is correct only while its decoder task is the single
  // consumer of the camera. The debug server's MJPEG handler is a second one, and with one buffer
  // the driver hands the same frame to both: the stream encodes a buffer the sensor's DMA is still
  // filling, so the picture arrives torn -- a clean top band over a black remainder -- and the
  // decoder is fed equally mangled frames, which is why nothing ever resolves while the feed is open.
  //
  // Re-initialising with two buffers gives each consumer a whole frame. This has to happen after
  // setup() (which performs the first esp_camera_init) and before beginOnCore() starts pulling
  // frames -- exactly this window. deinit invalidates the sensor handle, so it is fetched below.
  {
    camera_config_t config = reader.cameraConfig;
    config.frame_size = GATE_FRAME_SIZE;
    config.fb_count = GATE_CAM_FB_COUNT;
    config.fb_location = CAMERA_FB_IN_PSRAM;
    // The library asks for 10MHz; the OV2640 on this board is specified for 20MHz and every
    // Espressif example drives it there. The pixel clock scales with it, so this is a straight
    // doubling of the frame rate available to both the decoder and the debug stream. Drop back to
    // 10000000 if a marginal ribbon connector starts producing striped or rolling frames.
    config.xclk_freq_hz = 20000000;
    // LATEST rather than WHEN_EMPTY: a browser that stalls mid-stream must not pin a buffer and
    // starve the decoder back down to the single-buffer behaviour this is here to fix.
    config.grab_mode = CAMERA_GRAB_LATEST;

    esp_camera_deinit();

    // Walk the buffer count down rather than dropping straight to one: PSRAM is shared with quirc's
    // working set, and two buffers is still a working configuration where three does not fit.
    esp_err_t err = ESP_FAIL;
    for (int count = GATE_CAM_FB_COUNT; count >= 1; count--) {
      config.fb_count = count;
      // A single buffer cannot serve two consumers under LATEST; fall back to the library's mode.
      config.grab_mode = (count > 1) ? CAMERA_GRAB_LATEST : CAMERA_GRAB_WHEN_EMPTY;
      err = esp_camera_init(&config);
      if (err == ESP_OK) break;
      debug_log::addf(debug_log::Level::Warn, "[cam] %d bafera nije moguće (0x%x).", count, err);
    }
    if (err != ESP_OK) {
      debug_log::addf(debug_log::Level::Bad, "[cam] Inicijalizacija kamere nije uspjela (0x%x).", err);
      return false;
    }
    // qrCodeDetectTask copies this struct when it starts, so keep the library's view in sync.
    reader.cameraConfig = config;
    debug_log::addf(debug_log::Level::Info, "[cam] Bafera kadrova: %d, XCLK: %d Hz",
                    config.fb_count, config.xclk_freq_hz);
  }

  // Checked through the driver rather than through setup()'s return value: the library has shipped
  // setup() as both void and as a status enum across versions, and a null sensor handle is the
  // same answer either way — the camera did not come up.
  sensor_t *sensor = esp_camera_sensor_get();
  if (sensor == nullptr) {
    debug_log::addf(debug_log::Level::Bad, "[cam] Inicijalizacija kamere nije uspjela (napajanje ili ribbon kabl?).");
    return false;
  }

  sensor->set_framesize(sensor, GATE_FRAME_SIZE);
  // A printed ticket is black on white and a phone screen is backlit; nudging contrast up and
  // letting AEC/AGC run gives quirc cleaner module edges in both cases.
  sensor->set_contrast(sensor, 2);
  sensor->set_whitebal(sensor, 1);
  sensor->set_gain_ctrl(sensor, 1);
  sensor->set_exposure_ctrl(sensor, 1);
  // The OV2640 on these boards is mounted such that the image comes out mirrored; quirc does not
  // care about handedness, but a corrected image makes the debug stream readable if you add one.
  sensor->set_hmirror(sensor, 1);

  // Core 1: WiFi and the TCP/IP stack live on core 0, and quirc's identify pass is long enough to
  // starve them if the two share a core.
  reader.beginOnCore(1);

  debug_log::addf(debug_log::Level::Good, "[cam] Čitač QR koda je spreman.");
  return true;
}

Scan poll(String &payload, String &error, uint16_t timeoutMs) {
  QRCodeData data;
  if (!reader.receiveQrCode(&data, timeoutMs)) return Scan::None;

  // The library reuses `payload` for quirc's error text when the decode failed, which is why the
  // valid flag has to be read before the buffer is interpreted as a ticket code.
  if (!data.valid) {
    error = String(reinterpret_cast<const char *>(data.payload));
    error.trim();
    if (error.isEmpty()) error = "nepoznata greška";
    failedCount++;
    return Scan::Failed;
  }

  payload = String(reinterpret_cast<const char *>(data.payload));
  payload.trim();
  if (payload.isEmpty()) return Scan::None;

  decodedCount++;
  return Scan::Decoded;
}

Stats stats() { return Stats{decodedCount, failedCount}; }

void flush() {
  QRCodeData discarded;
  // Zero timeout: drain whatever is already queued and return immediately, rather than waiting for
  // a fresh read.
  while (reader.receiveQrCode(&discarded, 0)) {
  }
}

} // namespace camera_qr
