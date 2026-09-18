#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

rm -rf ./dist/linux-x64 ./dist/Glyphore-6.0.1-linux-x64.zip
dotnet restore Glyphore.csproj
dotnet publish Glyphore.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o ./dist/linux-x64

command -v zip >/dev/null 2>&1 || { echo "zip is required to create the release archive." >&2; exit 1; }
( cd ./dist/linux-x64 && zip -q -r ../Glyphore-6.0.1-linux-x64.zip . )
echo "Release: $(realpath ./dist/Glyphore-6.0.1-linux-x64.zip)"

# --- AppImage ---
APPIMAGETOOL="./dist/appimagetool-x86_64.AppImage"
if ! command -v appimagetool >/dev/null 2>&1 && [ ! -x "$APPIMAGETOOL" ]; then
    echo "Descargando appimagetool..."
    curl -sL -o "$APPIMAGETOOL" \
        https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
    chmod +x "$APPIMAGETOOL"
fi
command -v appimagetool >/dev/null 2>&1 && APPIMAGETOOL="appimagetool"

rm -rf ./dist/AppDir
mkdir -p ./dist/AppDir/usr/bin ./dist/AppDir/usr/share/applications ./dist/AppDir/usr/share/icons/hicolor/1024x1024/apps

cp -r ./dist/linux-x64/. ./dist/AppDir/usr/bin/
cp glyphore.desktop ./dist/AppDir/usr/share/applications/
cp glyphore.desktop ./dist/AppDir/glyphore.desktop
cp Assets/Glyphore-Icon-1024.png ./dist/AppDir/usr/share/icons/hicolor/1024x1024/apps/glyphore.png
cp Assets/Glyphore-Icon-1024.png ./dist/AppDir/glyphore.png

cat > ./dist/AppDir/AppRun <<'EOF'
#!/usr/bin/env bash
HERE="$(dirname "$(readlink -f "${0}")")"
exec "$HERE/usr/bin/Glyphore" "$@"
EOF
chmod +x ./dist/AppDir/AppRun

ARCH=x86_64 "$APPIMAGETOOL" ./dist/AppDir ./dist/Glyphore-6.0.1-linux-x64.AppImage
echo "AppImage: $(realpath ./dist/Glyphore-6.0.1-linux-x64.AppImage)"

if [ "${1:-}" = "--run" ]; then
    exec ./dist/Glyphore-6.0.1-linux-x64.AppImage
fi
