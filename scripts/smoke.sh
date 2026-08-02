#!/usr/bin/env bash
# Starts the API, pushes a record through it and prints what came back.
# Runs in loopback mode: no broker, no database.
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh

dotnet run --project src/BunkerFlow.Api --urls http://localhost:8080 >/tmp/bunkerflow-api.log 2>&1 &
api_pid=$!
trap 'kill "$api_pid" 2>/dev/null || true' EXIT

for _ in $(seq 1 40); do
  if curl -sf http://localhost:8080/health/live >/dev/null 2>&1; then
    break
  fi
  sleep 0.5
done

echo "=== health/ready ==="
curl -s http://localhost:8080/health/ready
echo

echo "=== ingest a valid trade ==="
curl -s -o /dev/null -w 'HTTP %{http_code}\n' -X POST http://localhost:8080/ingest \
  -H 'Content-Type: application/json' \
  -d '{"sourceSystem":"trading-desk","sourceRecordId":"SMOKE-1","fields":{"tradeReference":"TR-900001","vesselImo":"9074729","port":"DKFRC","product":"VLSFO","quantityMt":"850,5","priceUsdPerMt":"612.25","counterparty":"Bunker Holding A/S","tradedAtUtc":"2026-08-01T08:30:00Z"}}'

echo "=== replay the same record ==="
curl -s -X POST http://localhost:8080/ingest \
  -H 'Content-Type: application/json' \
  -d '{"sourceSystem":"trading-desk","sourceRecordId":"SMOKE-1","fields":{"tradeReference":"TR-900001","vesselImo":"9074729","port":"DKFRC","product":"VLSFO","quantityMt":"850,5","priceUsdPerMt":"612.25","counterparty":"Bunker Holding A/S","tradedAtUtc":"2026-08-01T08:30:00Z"}}'
echo

echo "=== ingest a trade with a bad IMO check digit ==="
curl -s -X POST http://localhost:8080/ingest \
  -H 'Content-Type: application/json' \
  -d '{"sourceSystem":"erp","sourceRecordId":"SMOKE-2","fields":{"deal_id":"D-1","imo":"9074720","delivery_port":"SGSIN","fuel_grade":"MGO","volume_mt":"1200,00","unit_price":"640,50","supplier":"Nordic Marine Fuels","trade_date":"2026-08-01T09:00:00Z"}}'
echo

echo "=== pull the ERP mock source and ingest it as a batch ==="
curl -s 'http://localhost:8080/mock-sources/erp/trades?count=20' \
  | python3 -c 'import json,sys; print(json.dumps([{"sourceSystem":"erp","sourceRecordId":r["sourceRecordId"],"fields":r} for r in json.load(sys.stdin)]))' \
  | curl -s -X POST http://localhost:8080/ingest/batch -H 'Content-Type: application/json' --data-binary @-
echo

echo "=== events (first 3) ==="
curl -s 'http://localhost:8080/events?limit=3' | python3 -m json.tool | head -40

echo "=== metrics ==="
curl -s http://localhost:8080/metrics | grep -v ' 0$'
