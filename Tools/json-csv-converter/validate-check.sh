#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: ./validate-check.sh <entity>"
  echo "Example: ./validate-check.sh monsters"
  exit 1
fi

ENTITY="$1"
node validateCheckJson.js "${ENTITY}.json" "${ENTITY}.schema.json"
