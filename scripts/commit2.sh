#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

git add .dockerignore docker-compose.yml README.md infra scripts src

git -c user.name="Alexandro Bolfa" -c user.email="contact@alexandro-bolfa.com" commit -q -m "fix(worker): survive broker outages and expose the worker's own metrics

Running the full compose stack turned up three things:

- A missing Kafka topic at startup threw ConsumeException and took the whole
  worker host down. Recoverable consume errors now log and retry.
- A record that failed to publish still had its offset committed, so the trade
  was lost during exactly the outage the retry policy exists for. The consumer
  now seeks back and leaves the offset uncommitted.
- The worker had no metrics endpoint despite doing most of the ingesting. It
  now runs on the Web SDK and serves /health/live and /metrics on 8081.

Also adds .dockerignore, without which the host's obj/ clobbered the
container's restore, and bakes the Service Bus emulator config into an image
instead of bind-mounting it from the host."

git log --oneline
