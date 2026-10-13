#!/usr/bin/env bash
# Real-Redis verification of RedisSectorCapacityLock, through the public API of the load-test stack.
# Run from the repo root after seed.js: loadtests/verify-redis-lock.sh   (expects "RESULT: 20 passed, 0 failed")
# Uses the seeded bulk ("Parter") sector; step 5 edits its Redis keys directly to simulate a hold expiring; step 7 resets Redis command stats.
set -uo pipefail
H=$(mktemp)
B=http://localhost:5100/api
SEED="$(dirname "$0")/results/seed.json"
SECTOR=$(grep -o '"bulkSectorId": "[^"]*"' $SEED | cut -d'"' -f4)
PRODUCT=$(grep -o '"productId": "[^"]*"' $SEED | cut -d'"' -f4)
K="sector:$SECTOR:capacity"
R() { docker exec eticketing-lt-redis redis-cli "$@"; }
pass=0; fail=0
ok()  { echo "  PASS  $1"; pass=$((pass+1)); }
bad() { echo "  FAIL  $1"; fail=$((fail+1)); }
expect() { if [ "$2" == "$3" ]; then ok "$1 ($2)"; else bad "$1: expected $3, got $2"; fi; }

# sum of quantities of every field in the hash (the invariant :used must equal)
hash_sum() { R EVAL "local a=redis.call('HGETALL',KEYS[1]) local u=0 for i=2,#a,2 do local v=a[i] u=u+tonumber(string.sub(v,1,string.find(v,':')-1)) end return u" 1 "$K"; }
live_sum() { R EVAL "local a=redis.call('HGETALL',KEYS[1]) local u=0 for i=2,#a,2 do local v=a[i] local s=string.find(v,':') if tonumber(string.sub(v,s+1))>=tonumber(ARGV[1]) then u=u+tonumber(string.sub(v,1,s-1)) end end return u" 1 "$K" "$(date +%s)"; }
used() { R GET "$K:used"; }
invariant() { expect "invariant :used == sum(hash)" "$(used)" "$(hash_sum)"; }

register() {
  local u="rv$RANDOM$RANDOM"
  curl -s -D "$H" -o /dev/null -H "Content-Type: application/json" \
    -d "{\"firstName\":\"Redis\",\"lastName\":\"Verify\",\"email\":\"$u@loadtest.local\",\"username\":\"$u\",\"password\":\"LoadTest123!\"}" $B/auth/register
  grep -i "set-cookie: eticketing_at=" "$H" | sed 's/.*eticketing_at=\([^;]*\).*/\1/'
}
hold() { curl -s -H "Cookie: eticketing_at=$AT" -H "Content-Type: application/json" -d '{"quantity":1}' $B/sectors/$SECTOR/hold | sed 's/.*"holdId":"\([^"]*\)".*/\1/'; }
buy() { # $1 holdId, $2 last4 -> prints purchase HTTP status
  local i; i=$(curl -s -H "Cookie: eticketing_at=$AT" -H "Content-Type: application/json" -d "{\"holdId\":\"$1\",\"lineItems\":[{\"ticketTypeId\":null,\"quantity\":1}]}" $B/purchases/payment-intent)
  local oid pid; oid=$(echo "$i" | sed 's/.*"orderId":"\([^"]*\)".*/\1/'); pid=$(echo "$i" | sed 's/.*"intentId":"\([^"]*\)".*/\1/')
  curl -s -o /dev/null -w "%{http_code}" -H "Cookie: eticketing_at=$AT" -H "Content-Type: application/json" \
    -d "{\"holdId\":\"$1\",\"lineItems\":[{\"ticketTypeId\":null,\"quantity\":1}],\"orderId\":\"$oid\",\"paymentIntentId\":\"$pid\",\"simulatedLast4\":\"$2\"}" $B/purchases
}
api_remaining() { sleep 6; curl -s "$B/sectors?productId=$PRODUCT&page=0&pageSize=20" | python -c "import sys,json; print([s['remainingCapacity'] for s in json.load(sys.stdin)['items'] if s['id']=='$SECTOR'][0])"; }

AT=$(register)

echo "1. Upgrade of an old-format counter (first touch by the new code)"
echo "   before: hash entries=$(R HLEN $K), :used exists=$(R EXISTS $K:used), key TTL=$(R TTL $K)"
LIVE=$(live_sum)
H1=$(hold)
expect ":used after first hold == live holds before + 1" "$(used)" "$((LIVE+1))"
expect "key TTL removed (sold seats never expire)" "$(R TTL $K)" "-1"
invariant

echo "2. Release a pending hold"
BEFORE=$(used)
curl -s -o /dev/null -X POST -H "Cookie: eticketing_at=$AT" $B/sectors/holds/$H1/release
expect ":used drops by 1" "$(used)" "$((BEFORE-1))"
expect "hold removed from pending set" "$(R ZSCORE $K:exp $H1)" ""
invariant

echo "3. Purchase (Mock, 4242) confirms the hold"
H2=$(hold); BEFORE=$(used)
expect "purchase status" "$(buy $H2 4242)" "201"
expect ":used unchanged by confirm (seat stays taken)" "$(used)" "$BEFORE"
expect "confirmed hold left the pending set" "$(R ZSCORE $K:exp $H2)" ""
expect "confirmed hold stored with the permanent expiry" "$(R HGET $K $H2)" "1:253402300799"
invariant

echo "4. Declined card (0000) gives the seat back"
H3=$(hold); BEFORE=$(used)
expect "declined purchase status" "$(buy $H3 0000)" "400"
expect ":used back down by 1" "$(used)" "$((BEFORE-1))"
invariant

echo "5. Expired hold: read-only remaining ignores it, next hold reclaims it"
H4=$(hold); PAST=$(( $(date +%s) - 60 ))
R ZADD $K:exp XX $PAST $H4 > /dev/null; R HSET $K $H4 "1:$PAST" > /dev/null   # simulate the 5 min passing
USED_WITH_EXPIRED=$(used)
expect "API remaining excludes the expired hold" "$(api_remaining)" "$((100000000 - USED_WITH_EXPIRED + 1))"
expect "read-only remaining did not mutate :used" "$(used)" "$USED_WITH_EXPIRED"
H5=$(hold)
expect ":used after reclaim + new hold (net 0)" "$(used)" "$USED_WITH_EXPIRED"
expect "expired hold removed from the hash" "$(R HEXISTS $K $H4)" "0"
invariant

echo "6. Releasing an unknown / already-released hold is a no-op"
BEFORE=$(used)
curl -s -o /dev/null -X POST -H "Cookie: eticketing_at=$AT" $B/sectors/holds/$H1/release
expect ":used unchanged" "$(used)" "$BEFORE"

echo "7. Cost of one hold no longer grows with sales"
R SLOWLOG RESET > /dev/null; R CONFIG RESETSTAT > /dev/null
for i in $(seq 1 20); do hold > /dev/null; done
echo "   $(R INFO commandstats | grep cmdstat_evalsha | tr -d '\r')"
echo "   hash entries now: $(R HLEN $K)"

echo; echo "RESULT: $pass passed, $fail failed"
