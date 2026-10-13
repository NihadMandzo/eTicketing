#!/usr/bin/env bash
# Live check of two fixes, against the load-test stack, after a flash sale has sold the Flash sector out:
#
#   1. Redis losing every key (FLUSHALL = a restart without persistence) must not let a sold-out
#      sector sell again: the counter is rebuilt from the Tickets table before the next hold.
#   2. Events the broker refuses (no queue bound, e.g. payment.failed while Notifications is down)
#      must not hold back the rest: no ticket.purchased row may be left waiting.
#
# Run from the repo root: loadtests/verify-recovery.sh   (expects "RESULT: N passed, 0 failed")
set -uo pipefail
H=$(mktemp)
B=http://localhost:5100/api
SEED="$(dirname "$0")/results/seed.json"
FLASH=$(grep -o '"flashSectorId": "[^"]*"' "$SEED" | cut -d'"' -f4)
BULK=$(grep -o '"bulkSectorId": "[^"]*"' "$SEED" | cut -d'"' -f4)
CAP=$(grep -o '"flashCapacity": [0-9]*' "$SEED" | grep -o '[0-9]*$')
SAPW=$(grep '^SA_PASSWORD=' "$(dirname "$0")/../.env" | cut -d= -f2- | tr -d '\r"')
R() { docker exec eticketing-lt-redis redis-cli "$@" | tr -d '\r'; }
SQL() { MSYS_NO_PATHCONV=1 docker exec eticketing-lt-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SAPW" -C -h -1 -W -Q "SET NOCOUNT ON; $1" | tr -d '\r' | head -1; }
pass=0; fail=0
expect() { if [ "$2" == "$3" ]; then echo "  PASS  $1 ($2)"; pass=$((pass+1)); else echo "  FAIL  $1: expected $3, got $2"; fail=$((fail+1)); fi; }

u="rc$RANDOM$RANDOM"
curl -s -D "$H" -o /dev/null -H "Content-Type: application/json" \
  -d "{\"firstName\":\"Recovery\",\"lastName\":\"Check\",\"email\":\"$u@loadtest.local\",\"username\":\"$u\",\"password\":\"LoadTest123!\"}" $B/auth/register
AT=$(grep -i "set-cookie: eticketing_at=" "$H" | sed 's/.*eticketing_at=\([^;]*\).*/\1/')
hold() { curl -s -o /dev/null -w "%{http_code}" -H "Cookie: eticketing_at=$AT" -H "Content-Type: application/json" -d '{"quantity":1}' "$B/sectors/$1/hold"; }

echo "1. Redis loses everything after a sold-out sale"
SOLD=$(SQL "SELECT COUNT(*) FROM TicketingDb.dbo.Tickets WHERE SectorId = '$FLASH' AND Status <> 3")
expect "Flash sector sold out in SQL" "$SOLD" "$CAP"
expect "Flash counter in Redis before the flush" "$(R GET "sector:$FLASH:capacity:used")" "$CAP"
R FLUSHALL > /dev/null
expect "Redis is empty" "$(R DBSIZE)" "0"
expect "hold on the sold-out sector after the flush is refused" "$(hold "$FLASH")" "409"
expect "counter rebuilt from SQL, not from zero" "$(R GET "sector:$FLASH:capacity:used")" "$CAP"
LOGGED=$(docker logs --since 2m eticketing-lt-ticketing 2>&1 | grep -c "obnovljen iz baze sa $CAP zauzetih")
expect "Ticketing logged the rebuild" "$([ "$LOGGED" -ge 1 ] && echo yes || echo no)" "yes"

echo "2. A sector with capacity left keeps selling after the flush"
BULK_SOLD=$(SQL "SELECT COUNT(*) FROM TicketingDb.dbo.Tickets WHERE SectorId = '$BULK' AND Status <> 3")
expect "hold on the bulk sector succeeds" "$(hold "$BULK")" "200"
expect "bulk counter = tickets sold in SQL + this hold" "$(R GET "sector:$BULK:capacity:used")" "$((BULK_SOLD + 1))"

echo "3. Refused events do not block the outbox"
sleep 6   # one dispatcher tick
expect "ticket.purchased events waiting in Ticketing's outbox" \
  "$(SQL "SELECT COUNT(*) FROM TicketingDb.dbo.OutboxMessages WHERE RoutingKey = 'ticket.purchased'")" "0"
echo "   refused rows kept for retry (no consumer running for them):"
MSYS_NO_PATHCONV=1 docker exec eticketing-lt-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SAPW" -C -W -s"|" -Q \
  "SET NOCOUNT ON; SELECT 'Ticketing' AS Svc, RoutingKey, COUNT(*) AS Rows, MAX(AttemptCount) AS MaxAttempts FROM TicketingDb.dbo.OutboxMessages GROUP BY RoutingKey UNION ALL SELECT 'Identity', RoutingKey, COUNT(*), MAX(AttemptCount) FROM IdentityDb.dbo.OutboxMessages GROUP BY RoutingKey" \
  | tr -d '\r' | sed 's/^/   /'

echo; echo "RESULT: $pass passed, $fail failed"
