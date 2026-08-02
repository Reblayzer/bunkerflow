#!/usr/bin/env bash
# Produces a few trade messages onto the Kafka topic so the streaming path has
# something to consume. Run it after `docker compose up`.
set -euo pipefail
cd "$(dirname "$0")/.."

topic="bunker.trades.raw"

docker compose exec -T redpanda rpk topic create "$topic" --partitions 1 || true

produce() {
  docker compose exec -T redpanda rpk topic produce "$topic" --key "$1" <<< "$2"
}

produce "PT-1" '{"sourceRecordId":"PT-1","tradeReference":"TR-800001","vesselImo":"9074729","port":"NLRTM","product":"VLSFO","quantityMt":"1450.0","priceUsdPerMt":"598.40","counterparty":"Nordic Marine Fuels","tradedAtUtc":"2026-08-01T11:15:00Z"}'
produce "PT-2" '{"sourceRecordId":"PT-2","tradeReference":"TR-800002","vesselImo":"9241061","port":"SGSIN","product":"LSMGO","quantityMt":"620.5","priceUsdPerMt":"721.90","counterparty":"Delta Energy DMCC","tradedAtUtc":"2026-08-01T12:40:00Z"}'
produce "PT-3" '{"sourceRecordId":"PT-3","tradeReference":"TR-800003","vesselImo":"9321483","port":"DKFRC","product":"HSFO","quantityMt":"980.0","priceUsdPerMt":"512.00","counterparty":"Bunker Holding A/S","tradedAtUtc":"2026-08-01T13:05:00Z"}'

# A deliberately broken one, to show it reaching the dead-letter path.
produce "PT-4" '{"sourceRecordId":"PT-4","tradeReference":"TR-800004","vesselImo":"9074720","port":"USHOU","product":"MGO","quantityMt":"400.0","priceUsdPerMt":"680.00","counterparty":"Delta Energy DMCC","tradedAtUtc":"2026-08-01T14:20:00Z"}'

echo "produced 4 messages to $topic"
