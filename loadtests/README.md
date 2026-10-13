# Load tests (k6)

Load, stress and flash-sale tests for the whole platform, driven through the public Gateway
exactly like a real client. They run against an **isolated** copy of the stack — its own
containers (`eticketing-lt-*`), network and SQL volume — so the tens of thousands of test
accounts and tickets they create never reach the development database.

## What's here

| File | What it does |
|---|---|
| `docker-compose.loadtest.yml` | Overlay on the root compose file: renames containers, moves the Gateway to port **5100**, forces `Payment:Provider=Mock`, lifts the per-IP login limit, adds the `k6` runner |
| `scripts/seed.js` | Creates a published product with an unlimited "Parter" sector and a 500-seat "Flash" sector through the real API, writes their ids to `results/seed.json` |
| `scripts/mixed.js` | The tiered test — `TARGET_VUS` concurrent users; 90% browse anonymously, every 10th registers and buys (hold → payment-intent → purchase) |
| `scripts/flash-sale.js` | `BUYERS` users sign up over 150 s, then all hit "buy" on the 500-seat sector at 180 s — at once, or `QUEUE_RATE` per second through a simulated virtual queue. Retries `503` after `Retry-After`. Fails on oversell, unsold seats, or any 5xx other than 503 |
| `scripts/breakpoint.js` | Open-model ramp of requests/second until errors or latency break — the ceiling of the deployment |
| `verify-redis-lock.sh` | 20 correctness checks of the Redis capacity lock against the real Redis (upgrade, release, purchase, decline, expiry, idempotency, invariant) |
| `verify-recovery.sh` | After a sold-out flash sale: wipes Redis and checks the sector cannot sell again (counter rebuilt from SQL), and that no purchase event is stuck in the outbox |
| `run.sh` | Runs one test and saves everything under `results/` (see "Output") |
| `run-all.sh` | The whole suite in report order, ~50 minutes |
| `report/analyze.py`, `report/build.py`, `report/template.html` | Turn `results/` into the charted report page `results/load-test-report.html` |

## Why some things are switched off

- **Notifications and PdfGeneration are not started.** They send real Brevo email for every
  registration and purchase; a 5,000-user run would mail thousands of fake addresses and burn the
  sending quota. Their events are refused by the broker (no queue bound) and wait in the outbox,
  which since 2026-10-08 no longer blocks every other event behind them.
- **Payment runs on the Mock provider.** Stripe test mode has much lower rate limits than live
  mode, so the test would measure Stripe's throttling instead of this system.
- **The login rate limit is lifted** (`RateLimiting__AuthAttemptsPerMinute`). In production it is
  20/minute per IP, and every virtual user here shares the k6 container's IP. The default is
  unchanged everywhere else.

## Running it (Git Bash, from the repo root)

```bash
# 1. Start a fresh isolated stack (first time builds images; ~1-3 min)
docker compose -p eticketing-loadtest -f docker-compose.yml -f loadtests/docker-compose.loadtest.yml down -v
docker compose -p eticketing-loadtest -f docker-compose.yml -f loadtests/docker-compose.loadtest.yml up -d --build gateway

# 2a. Everything, in order (~50 min), then the report page
loadtests/run-all.sh
python loadtests/report/analyze.py && python loadtests/report/build.py   # -> loadtests/results/load-test-report.html

# 2b. Or one test at a time
loadtests/run.sh seed-run   seed.js -q                     # once per fresh stack, and before each flash sale
loadtests/run.sh tier-1k    mixed.js -q -e TARGET_VUS=1000
loadtests/run.sh tier-5k    mixed.js -q -e TARGET_VUS=5000 -e RAMP=3m -e HOLD=3m
loadtests/run.sh breakpoint breakpoint.js -q -e MAX_RPS=6000
loadtests/run.sh flash-burst flash-sale.js -q -e BUYERS=2000                   # everyone at once
loadtests/run.sh flash-queue flash-sale.js -q -e BUYERS=2000 -e QUEUE_RATE=100  # virtual queue, 100 buyers/s
loadtests/verify-redis-lock.sh
loadtests/verify-recovery.sh                               # right after a flash sale

# 3. Tear down (removes the load-test containers AND their database volume)
docker compose -p eticketing-loadtest -f docker-compose.yml -f loadtests/docker-compose.loadtest.yml down -v
```

Do not label a run `seed`: the summary file would be `seed-summary.json`, which is fine, but older
versions of `run.sh` wrote `seed.json` and overwrote the seed data.

## Output (per run, in `results/`)

| File | Contents |
|---|---|
| `<label>.txt` | k6 terminal output; the end-of-test summary is at the bottom |
| `<label>-summary.json` | machine-readable summary |
| `<label>.csv.gz` | every k6 sample (per request) — what the report's time series and percentiles come from |
| `<label>-dashboard.html` | k6's own HTML report with charts |
| `<label>-stats.txt` | `docker stats` every 10 s: CPU/memory per container |
| `<label>-outbox.csv` | outbox rows waiting/refused in Ticketing and Identity every 10 s, plus tickets sold |

Reading the k6 summary: `http_req_duration` p(95)/p(99) is the latency to quote; `http_req_failed`
counts every non-2xx including deliberate `409` sold-out and `503` overloaded answers;
`✓ / ✗` per threshold is the pass/fail of each script.

## Limits of a single machine

One machine can't generate 100,000 or 1,000,000 concurrent users. Each k6 virtual user costs
memory, and the generator shares the CPU with the system it's testing (k6 alone averaged 7 of 16
cores at 5,000 users). Beyond that, run k6 distributed (k6-operator on Kubernetes, or Grafana Cloud
k6) against a deployment on separate hardware. For on-sale moments above the measured capacity,
the platform's answer is a virtual waiting room in front of the sale.
