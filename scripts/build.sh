#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"

CONTAINER_MANAGER="${CONTAINER_MANAGER:-$(command -v podman || command -v docker || true)}"
if [[ -z "${CONTAINER_MANAGER}" ]]; then
  echo "Error: neither podman nor docker found."
  exit 1
fi

MANAGED_DIR=""
OUTPUT_DIR="$REPO_ROOT/build-output"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --managed-dir)
      MANAGED_DIR="$2"
      shift 2
      ;;
    --output-dir)
      OUTPUT_DIR="$2"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1"
      exit 1
      ;;
  esac
done

if [[ -z "$MANAGED_DIR" ]]; then
  echo "Usage: $0 --managed-dir /path/to/Managed [--output-dir /path/to/out]"
  exit 1
fi

if [[ ! -f "$MANAGED_DIR/Assembly-CSharp.dll" ]]; then
  echo "Error: invalid Managed dir: $MANAGED_DIR"
  exit 1
fi

mkdir -p "$OUTPUT_DIR"

"$CONTAINER_MANAGER" build \
  -f "$REPO_ROOT/Dockerfile.build" \
  -t noautopilot-build \
  "$REPO_ROOT"

"$CONTAINER_MANAGER" run --rm \
  -v "$REPO_ROOT":/src:ro \
  -v "$OUTPUT_DIR":/out \
  -v "$MANAGED_DIR":/managed:ro \
  noautopilot-build \
  bash -lc '
    set -euo pipefail
    rsync -a --exclude bin --exclude obj /src/ /tmp/build/
    cd /tmp/build
    dotnet restore NOAutopilot.csproj --locked-mode
    dotnet build NOAutopilot.csproj \
      -c Release \
      --no-restore \
      -p:ManagedDir=/managed \
      -p:OutputPath=/out/
  '
