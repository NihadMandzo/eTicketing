// Flash sale: many more buyers than seats, all hitting "buy" in the same second — the moment a
// ticketing system actually breaks in production. This is a correctness test as much as a speed
// test: the Redis capacity lock must never let more tickets be sold than the sector holds.
//
//   k6 run -e BUYERS=2000 /scripts/flash-sale.js
//
// Each VU registers its own account at a random moment in the first REGISTER_WINDOW_SECONDS (150 s),
// then every VU sleeps until the same instant (SALE_AT_SECONDS, 180 s) and goes hold ->
// payment-intent -> purchase on the seeded "Flash" sector (seed.json flashCapacity seats, 500 by
// default). Sign-ups are spread out and finish before the sale on purpose: real buyers are already
// logged in when tickets go on sale, and squeezing 2,000 PBKDF2 hashes into the same minute
// measured the sign-up storm instead of the sale.
//
// Pass/fail: purchases_completed must equal the capacity exactly — not one more (oversell), and
// not fewer (seats lost to the lock) — and every refused buyer must get a clean 409, not a 5xx.
//
// QUEUE_RATE=N simulates a virtual waiting room admitting N buyers per second; 0 (default) sends
// everyone at once. Buyers retry a 503 after Retry-After, as a real client should.
import { sleep, check } from 'k6';
import exec from 'k6/execution';
import { Counter, Trend } from 'k6/metrics';
import http from 'k6/http';
import * as api from './lib/api.js';

const seed = JSON.parse(open('/results/seed.json'));
const BUYERS = Number(__ENV.BUYERS || 2000);
const REGISTER_WINDOW_SECONDS = Number(__ENV.REGISTER_WINDOW_SECONDS || 150);
const SALE_AT_SECONDS = Number(__ENV.SALE_AT_SECONDS || 180);
const RUN_ID = __ENV.RUN_ID || `${Math.floor(Date.now() / 1000)}`;
// Virtual waiting room. 0 = everyone hits "buy" in the same second. N > 0 = buyers are admitted
// to the sale N per second, in arrival order, the way a queue in front of the sale page would.
const QUEUE_RATE = Number(__ENV.QUEUE_RATE || 0);
// A well-behaved client retries a 503 after Retry-After; this many times at most.
const MAX_RETRIES = Number(__ENV.MAX_RETRIES || 3);

export const options = {
  scenarios: {
    flash: { executor: 'per-vu-iterations', vus: BUYERS, iterations: 1, maxDuration: '10m' },
  },
  thresholds: {
    // Never more than the seats that exist ...
    purchases_completed: [`count<=${seed.flashCapacity}`, `count>=${seed.flashCapacity}`],
    holds_granted: [`count<=${seed.flashCapacity}`],
    // ... and, with more buyers than seats, every seat actually sold.
    // Sold out must be a clean 409. A 503 is the deliberate load-shedding refusal and is counted
    // separately (shed_503); anything else at 5xx is a real failure.
    server_errors: ['count==0'],
    'http_req_duration{endpoint:POST /sectors/{id}/hold}': ['p(95)<2000'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

const holdsGranted = new Counter('holds_granted');
const soldOut = new Counter('sold_out_409');
const purchasesCompleted = new Counter('purchases_completed');
const serverErrors = new Counter('server_errors');
const shed = new Counter('shed_503');
const gaveUp = new Counter('gave_up_after_503');
const timeToTicket = new Trend('time_to_ticket', true);

function countServerError(res) {
  if ((res.status >= 500 && res.status !== 503) || res.status === 0) serverErrors.add(1);
}

/** Sends, and on 503 waits Retry-After (plus jitter) and tries again, up to MAX_RETRIES times. */
function withRetry(send) {
  let res = send();
  for (let attempt = 0; res.status === 503 && attempt < MAX_RETRIES; attempt++) {
    shed.add(1);
    sleep(Number(res.headers['Retry-After'] || 2) + Math.random());
    res = send();
  }
  if (res.status === 503) { shed.add(1); gaveUp.add(1); }
  countServerError(res);
  return res;
}

export default function () {
  sleep(Math.random() * REGISTER_WINDOW_SECONDS);
  const session = api.register({
    firstName: 'Flash',
    lastName: 'Buyer',
    email: `fs${RUN_ID}.${__VU}@loadtest.local`,
    username: `fs${RUN_ID}_${__VU}`,
    password: 'LoadTest123!',
  });
  if (!session) return;

  // The on-sale moment — the same instant for everyone, or this buyer's turn in the queue.
  const queueDelayMs = QUEUE_RATE > 0 ? ((__VU - 1) / QUEUE_RATE) * 1000 : 0;
  const saleAt = exec.scenario.startTime + SALE_AT_SECONDS * 1000 + queueDelayMs;
  const wait = (saleAt - Date.now()) / 1000;
  if (wait > 0) sleep(wait);

  const started = Date.now();
  const held = withRetry(() => api.hold(session, seed.flashSectorId, 1));
  if (held.status === 409) { soldOut.add(1); return; }
  if (!check(held, { 'hold 200 or 409': (r) => r.status === 200 })) return;
  holdsGranted.add(1);
  const holdId = JSON.parse(held.body).holdId;

  const intent = withRetry(() => api.createPaymentIntent(session, holdId, 1));
  if (!check(intent, { 'payment-intent 201': (r) => r.status === 201 })) return;
  const { orderId, intentId } = JSON.parse(intent.body);

  const bought = withRetry(() => api.purchase(session, holdId, orderId, intentId, 1));
  if (!check(bought, { 'purchase 200': (r) => r.status === 200 || r.status === 201 })) return;
  purchasesCompleted.add(1);
  timeToTicket.add(Date.now() - started);
}

// Ask the system itself what is left, independently of what the VUs counted.
export function teardown() {
  const res = http.get(`${api.BASE_URL}/api/sectors?productId=${seed.productId}&page=0&pageSize=20`);
  const flash = (res.json('items') || []).find((s) => s.id === seed.flashSectorId);
  console.log(`Flash sector capacity=${seed.flashCapacity}, remainingCapacity reported by API=${flash ? flash.remainingCapacity : 'n/a'}`);
}
