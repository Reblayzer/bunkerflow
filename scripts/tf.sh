#!/usr/bin/env bash
# Terraform against the signed-in Azure subscription.
#   scripts/tf.sh plan
#   scripts/tf.sh apply -auto-approve
#   scripts/tf.sh destroy -auto-approve
set -euo pipefail
cd "$(dirname "$0")/../infra/terraform"
export PATH="$HOME/.local/bin:$PATH"

# Taken from the CLI session rather than committed, so no subscription id ends
# up in source control.
ARM_SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
export ARM_SUBSCRIPTION_ID

terraform init -input=false -no-color >/dev/null
terraform "$@" -no-color
