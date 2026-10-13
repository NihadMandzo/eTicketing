#!/usr/bin/env bash
# The full measurement suite, in the order the report uses. ~50 minutes on a 16-core machine.
# Start from a fresh stack (see README), then: loadtests/run-all.sh
set -uo pipefail
here="$(dirname "$0")"

"$here/run.sh" seed-run seed.js -q
"$here/run.sh" smoke mixed.js -q -e TARGET_VUS=20 -e RAMP=10s -e HOLD=30s -e BUYER_EVERY=2
"$here/verify-redis-lock.sh" | tee "$here/results/verify-redis-lock.txt"

"$here/run.sh" tier-1k mixed.js -q -e TARGET_VUS=1000
"$here/run.sh" tier-5k mixed.js -q -e TARGET_VUS=5000 -e RAMP=3m -e HOLD=3m
"$here/run.sh" breakpoint breakpoint.js -q -e MAX_RPS=6000

# Each flash sale needs a fresh 500-seat sector.
"$here/run.sh" seed-flash-a seed.js -q
"$here/run.sh" flash-burst flash-sale.js -q -e BUYERS=2000
"$here/run.sh" seed-flash-b seed.js -q
"$here/run.sh" flash-queue flash-sale.js -q -e BUYERS=2000 -e QUEUE_RATE=100

# Runs against the sold-out sector the queued sale just produced.
"$here/verify-recovery.sh" | tee "$here/results/verify-recovery.txt"
