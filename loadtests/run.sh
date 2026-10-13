#!/usr/bin/env bash
# Runs one k6 test against the isolated load-test stack and keeps everything it printed.
#
#   loadtests/run.sh <label> <script> [extra k6 args...]
#   loadtests/run.sh tier-1k mixed.js -e TARGET_VUS=1000
#
# Writes, under loadtests/results/:
#   <label>.txt         the k6 terminal output (the end-of-test summary is at the bottom)
#   <label>-summary.json  the machine-readable summary (--summary-export)
#   <label>.csv.gz      every k6 sample (per request), for time-series charts
#   <label>-dashboard.html  k6's own HTML report with charts (web dashboard export)
#   <label>-stats.txt   `docker stats` sampled every 10 s — CPU/memory per container, which is
#                       what tells you *which* service ran out first.
set -euo pipefail

label="$1"; script="$2"; shift 2
root="$(cd "$(dirname "$0")/.." && (pwd -W 2>/dev/null || pwd))"
results="$root/loadtests/results"
mkdir -p "$results"

compose=(docker compose -p eticketing-loadtest -f "$root/docker-compose.yml" -f "$root/loadtests/docker-compose.loadtest.yml" --profile loadtest)

( while true; do
    { echo "=== $(date -u +%H:%M:%S)"; docker stats --no-stream --format "{{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}" | grep -E -- '-lt-|loadtest-k6' || true; } >> "$results/$label-stats.txt"
    sleep 10
  done ) &
sampler=$!

# Outbox backlog every 10 s: rows waiting to be published (AttemptCount = 0) and rows the broker
# refused (AttemptCount > 0), per service, plus tickets sold. Shows whether events keep flowing.
sapw=$(grep '^SA_PASSWORD=' "$root/.env" | cut -d= -f2- | tr -d '\r"')
echo "time,ticketing_pending,ticketing_refused,identity_pending,identity_refused,tickets" > "$results/$label-outbox.csv"
( while true; do
    row=$(MSYS_NO_PATHCONV=1 docker exec eticketing-lt-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$sapw" -C -h -1 -W -s, -Q \
      "SET NOCOUNT ON; SELECT (SELECT COUNT(*) FROM TicketingDb.dbo.OutboxMessages WHERE AttemptCount = 0), (SELECT COUNT(*) FROM TicketingDb.dbo.OutboxMessages WHERE AttemptCount > 0), (SELECT COUNT(*) FROM IdentityDb.dbo.OutboxMessages WHERE AttemptCount = 0), (SELECT COUNT(*) FROM IdentityDb.dbo.OutboxMessages WHERE AttemptCount > 0), (SELECT COUNT(*) FROM TicketingDb.dbo.Tickets)" 2>/dev/null | tr -d '\r' | head -1)
    [ -n "$row" ] && echo "$(date -u +%H:%M:%S),$row" >> "$results/$label-outbox.csv"
    sleep 10
  done ) &
outbox_sampler=$!
trap 'kill $sampler $outbox_sampler 2>/dev/null || true' EXIT
: > "$results/$label-stats.txt"

# MSYS_NO_PATHCONV: stop Git Bash on Windows rewriting /scripts/... into C:/Program Files/Git/...
MSYS_NO_PATHCONV=1 "${compose[@]}" run --rm \
  -e K6_WEB_DASHBOARD=true -e K6_WEB_DASHBOARD_EXPORT="/results/$label-dashboard.html" \
  k6 run \
  --summary-export "/results/$label-summary.json" \
  --out "csv=/results/$label.csv.gz" \
  -e RUN_ID="$(date +%s)" \
  "$@" "/scripts/$script" 2>&1 | tee "$results/$label.txt"
