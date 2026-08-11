#!/usr/bin/env bash
# The lab keeps one engine checkout while making the product repositories independently versioned.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
exec cargo run --quiet \
  --manifest-path "$SCRIPT_DIR/../../../azimuth-engine/tools/azimuth/Cargo.toml" -- "$@"
