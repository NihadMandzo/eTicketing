// One-off data setup for the load tests. Run once per fresh load-test stack:
//
//   k6 run /scripts/seed.js
//
// Creates, through the real API as a seeded organizer (so every event and read model is fed the
// way production feeds it):
//   - one published SingleOccurrence product ("LoadTest koncert"),
//   - a "Parter" sector with effectively unlimited capacity — the mixed test's buyers never sell
//     it out, so the purchase path is measured rather than the sold-out path,
//   - a "Flash" sector with exactly FLASH_CAPACITY seats — the flash-sale test's oversell target.
// The ids are written to /results/seed.json, which the other scripts read.
import { sleep, fail } from 'k6';
import * as api from './lib/api.js';

export const options = { vus: 1, iterations: 1 };

const ORGANIZER = { email: 'emir@sarajevo-events.ba', password: 'OrgAdmin123!' };
const FLASH_CAPACITY = Number(__ENV.FLASH_CAPACITY || 500);

function created(res, what) {
  if (res.status !== 200 && res.status !== 201) fail(`${what} failed: ${res.status} ${res.body}`);
  return res.json();
}

function waitUntilSellable(productId) {
  // Ticketing learns the product is Published from product.changed over RabbitMQ (ProductSnapshot),
  // and the public sector list refuses a product it has no snapshot for — so poll until it shows.
  for (let i = 0; i < 60; i++) {
    const res = api.listSectors(productId);
    if (res.status === 200 && res.json('items').length > 0) return;
    sleep(1);
  }
  fail('Product never became sellable in Ticketing (ProductSnapshot not projected within 60 s)');
}

export default function () {}

export function setup() {
  const session = api.login(ORGANIZER.email, ORGANIZER.password);
  if (!session) fail('Organizer login failed');

  const date = new Date(Date.now() + 90 * 24 * 3600 * 1000).toISOString();
  const product = created(api.organizerPost(session, '/products', {
    name: `LoadTest koncert ${Date.now()}`,
    description: 'Proizvod kreiran za testiranje opterećenja.',
    date,
    categoryId: 1,
    latitude: 43.8563,
    longitude: 18.4131,
    city: 0,
  }, 'POST /products'), 'Create product');

  created(api.organizerPost(session, `/products/${product.id}/publish`, undefined, 'POST /products/{id}/publish'), 'Publish product');

  const sector = (name, capacity, price) => {
    const s = created(api.organizerPost(session, '/sectors', { productId: product.id, name, capacity, price },
      'POST /sectors'), `Create sector ${name}`);
    created(api.organizerPost(session, `/sectors/${s.id}/publish`, undefined, 'POST /sectors/{id}/publish'), `Publish sector ${name}`);
    return s.id;
  };

  const bulkSectorId = sector('Parter', 100000000, 20);
  const flashSectorId = sector('Flash', FLASH_CAPACITY, 50);

  waitUntilSellable(product.id);

  return { productId: product.id, bulkSectorId, flashSectorId, flashCapacity: FLASH_CAPACITY, seededAt: new Date().toISOString() };
}

export function handleSummary(data) {
  const seed = JSON.stringify(data.setup_data, null, 2);
  return { '/results/seed.json': seed, stdout: `\nSeed data written to /results/seed.json:\n${seed}\n` };
}
