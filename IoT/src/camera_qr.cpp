#include "camera_qr.h"

#include <ESP32QRCodeReader.h>
#include <esp_camera.h>

#include "config.h"

namespace camera_qr {
namespace {

ESP32QRCodeReader reader(CAMERA_MODEL_AI_THINKER);

} // namespace

bool begin() {
  // quirc's working buffers plus a VGA grayscale frame are several hundred kilobytes — far more
  // than the ~300KB of internal DRAM left after WiFi. Without PSRAM the decoder allocates nothing
  // and every scan silently fails, so say so loudly here rather than at the door.
  if (!psramFound()) {
    Serial.println("[cam] PSRAM nije pronađen — čitanje QR koda neće raditi.");
    return false;
  }

  Serial.printf("[cam] Slobodan PSRAM: %u B, heap: %u B\n", ESP.getFreePsram(), ESP.getFreeHeap());

  reader.setup();

  // Checked through the driver rather than through setup()'s return value: the library has shipped
  // setup() as both void and as a status enum across versions, and a null sensor handle is the
  // same answer either way — the camera did not come up.
  sensor_t *sensor = esp_camera_sensor_get();
  if (sensor == nullptr) {
    Serial.println("[cam] Inicijalizacija kamere nije uspjela (napajanje ili ribbon kabl?).");
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

  Serial.println("[cam] Čitač QR koda je spreman.");
  return true;
}

bool poll(String &payload, uint16_t timeoutMs) {
  QRCodeData data;
  if (!reader.receiveQrCode(&data, timeoutMs)) return false;
  if (!data.valid) return false;

  payload = String(reinterpret_cast<const char *>(data.payload));
  payload.trim();
  return !payload.isEmpty();
}

void flush() {
  QRCodeData discarded;
  // Zero timeout: drain whatever is already queued and return immediately, rather than waiting for
  // a fresh read.
  while (reader.receiveQrCode(&discarded, 0)) {
  }
}

} // namespace camera_qr
