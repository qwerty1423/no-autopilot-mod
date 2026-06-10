#!/bin/bash
# Usage: ./scripts/build.sh "/path/to/Nuclear Option"

set -e

GAME_DIR="$1"

if [ -z "$GAME_DIR" ]; then
    echo "Usage: $0 \"/path/to/Nuclear Option\""
    exit 1
fi

echo "Building with game from: $GAME_DIR"

podman build -f Dockerfile.build -t noautopilot-build .

podman run --rm \
  -v "$(pwd)":/build \
  -v "$GAME_DIR":/game:ro \
  noautopilot-build \
  dotnet build NOAutopilot.csproj -c Release \
    -p:NuclearOptionDir=/game
