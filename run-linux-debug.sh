#!/usr/bin/env bash
set -u
cd "$(dirname "${BASH_SOURCE[0]}")"
LOG="$(pwd)/glyphore-diagnostic-$(date +%Y%m%d-%H%M%S).log"
exec > >(tee -a "$LOG") 2>&1
export GLYPHORE_DIAGNOSTIC=1

echo "Glyphoré source diagnostic"
echo "Log: $LOG"

dotnet run --project Glyphore.csproj -- --diagnostic "$@"
STATUS=$?
echo "Exit code: $STATUS"
exit "$STATUS"
