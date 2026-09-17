#!/usr/bin/env bash
set -euo pipefail
root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
publish_dir="$root_dir/publish/linux-x64"
app_dir="$root_dir/dist/Glyphore.AppDir"
dotnet publish "$root_dir/Glyphore/Glyphore.csproj" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$publish_dir"
if [[ "${1:-}" != "--appimage" ]]; then
  exit 0
fi
command -v appimagetool >/dev/null || { printf '%s\n' 'appimagetool no está instalado'; exit 1; }
rm -rf "$app_dir"
install -d "$app_dir/usr/bin" "$app_dir/usr/share/applications" "$app_dir/usr/share/icons/hicolor/1024x1024/apps"
install -m 755 "$publish_dir/Glyphore" "$app_dir/usr/bin/Glyphore"
install -m 644 "$root_dir/linux/io.github.saturnus25.Glyphore.desktop" "$app_dir/io.github.saturnus25.Glyphore.desktop"
install -m 644 "$root_dir/Glyphore/Assets/Glyphore-Icon-1024.png" "$app_dir/io.github.saturnus25.Glyphore.png"
install -m 644 "$root_dir/Glyphore/Assets/Glyphore-Icon-1024.png" "$app_dir/usr/share/icons/hicolor/1024x1024/apps/io.github.saturnus25.Glyphore.png"
install -m 644 "$root_dir/linux/io.github.saturnus25.Glyphore.desktop" "$app_dir/usr/share/applications/io.github.saturnus25.Glyphore.desktop"
install -m 755 "$root_dir/linux/AppRun" "$app_dir/AppRun"
appimagetool "$app_dir" "$root_dir/dist/Glyphore-6.0.0-linux-x86_64.AppImage"
