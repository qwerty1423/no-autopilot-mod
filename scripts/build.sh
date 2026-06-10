#!/bin/bash
# Usage: ./scripts/build.sh "/path/to/Nuclear Option"

set -e

CONTAINER_ENGINE="${CONTAINER_ENGINE:-$(command -v podman || command -v docker)}"

if [ -z "$CONTAINER_ENGINE" ]; then
    echo "Error: Neither podman nor docker found. Please install one or set CONTAINER_ENGINE."
    exit 1
fi

echo "Using $CONTAINER_ENGINE"

GAME_DIR="$1"

if [ -z "$GAME_DIR" ]; then
    echo "Usage: $0 \"/path/to/Nuclear Option\""
    exit 1
fi

echo "Building with game from $GAME_DIR"

mkdir -p build-output

$CONTAINER_ENGINE build -f Dockerfile.build -t noautopilot-build .

$CONTAINER_ENGINE run --rm \
  -v "$(pwd)":/src:ro \
  -v "$(pwd)/build-output":/out \
  -v "$GAME_DIR":/game:ro \
  noautopilot-build \
  bash -lc '
    rsync -a --exclude bin --exclude obj /src/ /tmp/build/ &&
    cd /tmp/build &&
    dotnet build NOAutopilot.csproj -c Release \
      -p:NuclearOptionDir=/game \
      -p:OutputPath=/out/
    '
