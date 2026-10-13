// Thin wrappers over the public Gateway API, shared by every scenario.
//
// Every request carries a `name` tag so k6 groups /api/products/<guid> under one row instead of
// one row per product id, and an `endpoint` tag the per-endpoint thresholds key on.
import http from 'k6/http';
import { check } from 'k6';

export const BASE_URL = __ENV.BASE_URL || 'http://gateway:8080';
const API = `${BASE_URL}/api`;

const JSON_HEADERS = { 'Content-Type': 'application/json' };

// The services run with ASPNETCORE_ENVIRONMENT=Production inside Docker, so the auth cookies are
// issued Secure (see AuthCookiePolicy). k6's cookie jar follows the spec and never sends a Secure
// cookie over plain http, so the session is carried by hand as an explicit Cookie header instead.
export function sessionFrom(res) {
  const at = res.cookies['eticketing_at'];
  if (!at || at.length === 0) return null;
  return { Cookie: `eticketing_at=${at[0].value}` };
}

function params(endpoint, session, extraHeaders, withBody) {
  const p = {
    headers: Object.assign({}, extraHeaders || {}, session || {}),
    tags: { name: endpoint, endpoint },
  };
  // mixed.js discards bodies by default to save memory per VU; these calls read theirs.
  if (withBody) p.responseType = 'text';
  return p;
}

export function login(emailOrUsername, password) {
  const res = http.post(`${API}/auth/login`, JSON.stringify({ emailOrUsername, password }),
    params('POST /auth/login', null, JSON_HEADERS));
  check(res, { 'login 200': (r) => r.status === 200 });
  return res.status === 200 ? sessionFrom(res) : null;
}

export function register(user) {
  const res = http.post(`${API}/auth/register`, JSON.stringify(user),
    params('POST /auth/register', null, JSON_HEADERS));
  check(res, { 'register 201': (r) => r.status === 201 });
  return res.status === 201 ? sessionFrom(res) : null;
}

export function listProducts(page = 0, pageSize = 12) {
  const res = http.get(`${API}/products?page=${page}&pageSize=${pageSize}`, params('GET /products'));
  check(res, { 'products 200': (r) => r.status === 200 });
  return res;
}

export function getProduct(id) {
  const res = http.get(`${API}/products/${id}`, params('GET /products/{id}'));
  check(res, { 'product 200': (r) => r.status === 200 });
  return res;
}

export function listCategories() {
  const res = http.get(`${API}/categories?page=0&pageSize=20`, params('GET /categories'));
  check(res, { 'categories 200': (r) => r.status === 200 });
  return res;
}

export function popular() {
  const res = http.get(`${API}/recommendations/popular?take=12`, params('GET /recommendations/popular'));
  check(res, { 'popular 200': (r) => r.status === 200 });
  return res;
}

export function listSectors(productId) {
  const res = http.get(`${API}/sectors?productId=${productId}&page=0&pageSize=20`, params('GET /sectors', null, null, true));
  check(res, { 'sectors 200': (r) => r.status === 200 });
  return res;
}

// Returns the raw response: a 409 (sold out) is an expected outcome for the flash-sale scenario,
// so callers decide what counts as success.
export function hold(session, sectorId, quantity = 1) {
  return http.post(`${API}/sectors/${sectorId}/hold`, JSON.stringify({ quantity }),
    params('POST /sectors/{id}/hold', session, JSON_HEADERS, true));
}

export function createPaymentIntent(session, holdId, quantity = 1) {
  return http.post(`${API}/purchases/payment-intent`,
    JSON.stringify({ holdId, lineItems: [{ ticketTypeId: null, quantity }] }),
    params('POST /purchases/payment-intent', session, JSON_HEADERS, true));
}

export function purchase(session, holdId, orderId, paymentIntentId, quantity = 1) {
  // "4242": the Mock provider's approving demo card ("0000" would simulate a decline).
  return http.post(`${API}/purchases`,
    JSON.stringify({ holdId, lineItems: [{ ticketTypeId: null, quantity }], orderId, paymentIntentId, simulatedLast4: '4242' }),
    params('POST /purchases', session, JSON_HEADERS, true));
}

export function myTickets(session) {
  const res = http.get(`${API}/tickets/mine?page=0&pageSize=10`, params('GET /tickets/mine', session));
  check(res, { 'my tickets 200': (r) => r.status === 200 });
  return res;
}

// Organizer-side calls, used only by seed.js.
export function organizerPost(session, path, body, endpoint) {
  return http.post(`${API}${path}`, body === undefined ? null : JSON.stringify(body),
    params(endpoint, session, JSON_HEADERS, true));
}
