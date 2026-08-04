#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$HOME/.local/bin:$PATH"

git add src infra scripts docs docker-compose.azure.yml README.md

git -c user.name="Alexandro Bolfa" -c user.email="contact@alexandro-bolfa.com" commit -q -m "feat(messaging): split Service Bus credentials by role, verified on real Azure

Applying the Terraform to a real subscription showed the least-privilege auth
rules were decorative. The application used one connection string for
publishing, consuming and dead-lettering, so the send-only rule could not
receive and, because per-entity rules produce entity-scoped connection
strings, could not address the dead-letter queue either. Against the emulator
a single credential did everything, which hid all of it.

The gateway now takes three credentials: send for the topic, listen for the
subscription, dead-letter send for the queue. Terraform grows a listen rule,
since Service Bus grants Listen on the topic rather than the subscription, and
outputs all three connection strings.

With that in place the two features that only existed in HCL were tested
against the live namespace: the broker discarded a replayed MessageId, and the
subscription filter passed schemaVersion 1 while withholding 2. CLI output is
in docs/azure-verification.md. Resources destroyed afterwards.

Also adds docker-compose.azure.yml for pointing the stack at a real namespace,
and scripts/tf.sh, which takes the subscription id from the CLI session so
none is committed."

git push origin main
git log --oneline -1
