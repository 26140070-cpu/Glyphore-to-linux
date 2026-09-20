#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
rm -rf ./dist/linux-x64 ./dist/Glyphore-6.0.1-linux-x64.zip
command -v dotnet >/dev/null 2>&1 || { echo 'dotnet 10 SDK is required.' >&2; exit 1; }
dotnet restore Glyphore.csproj
dotnet publish Glyphore.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o ./dist/linux-x64
command -v zip >/dev/null 2>&1 || { echo 'zip is required.' >&2; exit 1; }
( cd ./dist/linux-x64 && zip -q -r ../Glyphore-6.0.1-linux-x64.zip . )
echo "Release: $(realpath ./dist/Glyphore-6.0.1-linux-x64.zip)"
