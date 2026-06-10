#!/bin/bash
podman build -f .github/ci/Dockerfile.ci -t noautopilot-ci .
podman run --rm -v "$(pwd)":/build noautopilot-ci \
  dotnet build .github/ci/NOAutopilot.CI.csproj -c Release
