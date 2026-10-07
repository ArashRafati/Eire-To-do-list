#!/usr/bin/env bash
set -euo pipefail
cd /workspace/Eire-To-do-list
export DOTNET_ROOT=/workspace/.tools/dotnet
export DOTNET_CLI_HOME=/workspace/.tools/dotnet-home
export NUGET_PACKAGES=/workspace/.tools/nuget
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
sdk_version=10.0.401
sdk_url=https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-linux-x64.tar.gz
sdk_sha512=51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b
if [ "$(uname -m)" != x86_64 ]; then
  echo 'This setup script requires Linux x86_64.' >&2
  exit 1
fi
if [ ! -x "$DOTNET_ROOT/dotnet" ] || [ "$("$DOTNET_ROOT/dotnet" --version)" != "$sdk_version" ]; then
  sdk_archive=$(mktemp /tmp/eire-sdk-XXXXXX.tar.gz)
  trap 'rm -f "$sdk_archive"' EXIT
  curl --fail --show-error --location --proto '=https' --tlsv1.2 "$sdk_url" -o "$sdk_archive"
  printf '%s  %s\n' "$sdk_sha512" "$sdk_archive" | sha512sum --check --status
  mkdir -p "$DOTNET_ROOT"
  tar -xzf "$sdk_archive" -C "$DOTNET_ROOT"
fi
export PATH="$DOTNET_ROOT:$PATH"
mkdir -p "$NUGET_PACKAGES" "$DOTNET_CLI_HOME"
dotnet restore tests/EireTodo.Checks/EireTodo.Checks.csproj --locked-mode
dotnet run --project tests/EireTodo.Checks/EireTodo.Checks.csproj -c Release --no-restore -- --exports-dir artifacts/export-checks
dotnet restore src/EireTodo.Windows/EireTodo.Windows.csproj --locked-mode
dotnet publish src/EireTodo.Windows/EireTodo.Windows.csproj -c Release --no-restore -o artifacts/windows-x64
