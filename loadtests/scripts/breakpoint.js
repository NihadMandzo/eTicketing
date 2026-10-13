// Breakpoint test: keep raising the request rate until the system stops coping, then stop.
//
//   k6 run -e MAX_RPS=6000 /scripts/breakpoint.js
//
// Open model (ramping-arrival-rate): k6 starts new requests at the target rate whether or not the
// previous ones have finished, which is how real traffic behaves — a slow server does not slow
// the internet down. The rate at which the abort thresholds fire is the ceiling of this
// deployment; divide it by a visitor's request rate to get how many concurrent users it can serve.
//
// Traffic is the anonymous browsing mix (the bulk of any real day): no logins, no purchases.
import http from 'k6/http';
import { check } from 'k6';
import { BASE_URL } from './lib/api.js';

const seed = JSON.parse(open('/results/seed.json'));
const MAX_RPS = Number(__ENV.MAX_RPS || 6000);
const DURATION = __ENV.DURATION || '10m';

export const options = {
  discardResponseBodies: true,
  scenarios: {
    breakpoint: {
      executor: 'ramping-arrival-rate',
      startRate: 50,
      timeUnit: '1s',
      preAllocatedVUs: 500,
      maxVUs: Number(__ENV.MAX_VUS || 3000),
      stages: [{ duration: DURATION, target: MAX_RPS }],
    },
  },
  thresholds: {
    // Abort as soon as either is sustained for 30 s: that rate is the breakpoint.
    http_req_failed: [{ threshold: 'rate<0.05', abortOnFail: true, delayAbortEval: '30s' }],
    http_req_duration: [{ threshold: 'p(95)<2000', abortOnFail: true, delayAbortEval: '30s' }],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

const API = `${BASE_URL}/api`;
const REQUESTS = [
  [`${API}/products?page=0&pageSize=12`, 'GET /products'],
  [`${API}/products/${seed.productId}`, 'GET /products/{id}'],
  [`${API}/sectors?productId=${seed.productId}&page=0&pageSize=20`, 'GET /sectors'],
  [`${API}/recommendations/popular?take=12`, 'GET /recommendations/popular'],
  [`${API}/categories?page=0&pageSize=20`, 'GET /categories'],
];

export default function () {
  const [url, name] = REQUESTS[Math.floor(Math.random() * REQUESTS.length)];
  const res = http.get(url, { tags: { name, endpoint: name } });
  check(res, { 'status 200': (r) => r.status === 200 });
}
