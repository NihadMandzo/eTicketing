// The main tiered load test: a realistic mix of anonymous browsers and buyers.
//
//   k6 run -e TARGET_VUS=1000 /scripts/mixed.js
//
// Closed model (ramping-vus): TARGET_VUS concurrent users, each looping "visits" with human think
// time between clicks, so request rate follows from user count the way it does in production
// (RPS ≈ users / (think time + response time)).
//
// 90% of VUs are anonymous browsers. Every 10th VU is a buyer: it registers its own account on
// its first visit (so registration + PBKDF2 hashing is load-tested too), then on every visit
// browses and buys one ticket through the full hold -> payment-intent -> purchase path against
// the Mock payment provider.
import { sleep, check } from 'k6';
import { Counter, Trend } from 'k6/metrics';
import * as api from './lib/api.js';

const seed = JSON.parse(open('/results/seed.json'));

const TARGET_VUS = Number(__ENV.TARGET_VUS || 100);
const RAMP = __ENV.RAMP || '2m';
const HOLD = __ENV.HOLD || '3m';
const BUYER_EVERY = Number(__ENV.BUYER_EVERY || 10);
const RUN_ID = __ENV.RUN_ID || `${Math.floor(Date.now() / 1000)}`;
const THINK_MIN = Number(__ENV.THINK_MIN || 1);
const THINK_MAX = Number(__ENV.THINK_MAX || 3);

export const options = {
  scenarios: {
    visitors: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: RAMP, target: TARGET_VUS },
        { duration: HOLD, target: TARGET_VUS },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '30s',
    },
  },
  // Bodies are only needed for a handful of calls, which ask for them explicitly. Discarding the
  // rest is what lets one k6 process hold thousands of VUs in memory.
  discardResponseBodies: true,
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1000', 'p(99)<2500'],
    'http_req_duration{endpoint:GET /products}': ['p(95)<800'],
    'http_req_duration{endpoint:GET /products/{id}}': ['p(95)<800'],
    'http_req_duration{endpoint:GET /sectors}': ['p(95)<800'],
    'http_req_duration{endpoint:GET /recommendations/popular}': ['p(95)<800'],
    'http_req_duration{endpoint:POST /auth/register}': ['p(95)<1500'],
    'http_req_duration{endpoint:POST /sectors/{id}/hold}': ['p(95)<1000'],
    'http_req_duration{endpoint:POST /purchases/payment-intent}': ['p(95)<1500'],
    'http_req_duration{endpoint:POST /purchases}': ['p(95)<2000'],
    purchases_completed: ['count>0'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

const purchasesCompleted = new Counter('purchases_completed');
const purchasesFailed = new Counter('purchases_failed');
const purchaseFlow = new Trend('purchase_flow_duration', true);

let session = null; // per VU: buyers keep the session they registered with

function think() {
  sleep(THINK_MIN + Math.random() * (THINK_MAX - THINK_MIN));
}

function browse() {
  api.listCategories();
  api.listProducts(0, 12);
  think();
  api.popular();
  think();
  api.getProduct(seed.productId);
  api.listSectors(seed.productId);
  think();
}

function buy() {
  if (!session) {
    session = api.register({
      firstName: 'Load',
      lastName: 'Tester',
      email: `lt${RUN_ID}.${__VU}@loadtest.local`,
      username: `lt${RUN_ID}_${__VU}`,
      password: 'LoadTest123!',
    });
    if (!session) { purchasesFailed.add(1, { step: 'register' }); return; }
  }

  const started = Date.now();
  const held = api.hold(session, seed.bulkSectorId, 1);
  if (!check(held, { 'hold 200': (r) => r.status === 200 })) { purchasesFailed.add(1, { step: 'hold' }); return; }
  const holdId = JSON.parse(held.body || '{}').holdId;
  think();

  const intent = api.createPaymentIntent(session, holdId, 1);
  if (!check(intent, { 'payment-intent 201': (r) => r.status === 201 })) { purchasesFailed.add(1, { step: 'intent' }); return; }
  const { orderId, intentId } = JSON.parse(intent.body);
  think();

  const bought = api.purchase(session, holdId, orderId, intentId, 1);
  if (!check(bought, { 'purchase 200': (r) => r.status === 200 || r.status === 201 })) { purchasesFailed.add(1, { step: 'purchase' }); return; }

  purchaseFlow.add(Date.now() - started);
  purchasesCompleted.add(1);
  api.myTickets(session);
}

export default function () {
  browse();
  if (__VU % BUYER_EVERY === 0) buy();
  think();
}
