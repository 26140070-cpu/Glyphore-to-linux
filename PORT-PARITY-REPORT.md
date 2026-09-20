# Glyphoré 6.0.1 — Native Linux Port Parity Report

Reference: Windows package `Glyphore-main(4)` (6.0.1), compared with the Linux working tree after the UI/OpenGL parity migration.

## Result

The Linux project uses a GTK-backed WinForms-compatible UI (`GTKSystem.Windows.Forms`) so the same `Form`/`Panel`/`Button`/`ComboBox`/`RichTextBox` architecture as Windows can be retained. The live preview uses native GTK3 `Gtk.GLArea` with an OpenGL 3.3 context. The Linux project has no legacy XAML UI or WGL dependency.

- Windows reference files missing from the Linux source: 0, excluding Windows-only WGL/bootstrap files intentionally replaced by the GTK backend.
- Main Windows UI families restored to the Linux build: `MainForm*.cs`, `Controls/*.cs`, `AboutForm`, `FullLicenseForm`, `ThirdPartyLicensesForm`, `ExportProgressWindow`, `LicenseTextViewer`, `DetachedWindowManager`, and `Theme`.
- Windows ASCII Title Studio structure restored as a separate modeless `GlyphoreWindow`, including text editor, prefab/style/font/palette/charset selectors, live OpenGL preview, background/framing/zoom controls, replay, detach/dock, mask editing, and Apply/Add scene actions.
- Linux-only OpenGL bridge: `Gtk.GLArea` + libepoxy procedure lookup.
- Windows-only rendering/platform APIs removed from active Linux source: WGL, `user32.dll`, `gdi32.dll`, and `opengl32.dll`; the UI is no longer based on the legacy XAML layer.

## Preview correction

The duplicate white ASCII overlay was removed. The preview viewport no longer contains a UI text overlay; OpenGL is the sole visual renderer of the ASCII frame. `RenderText()` remains available for copy/export workflows.

The OpenGL preview now also honors the editor preview background color/mode and uses the same editor mask overlay uniform path as the Windows renderer.

## UI parity

The Linux application no longer uses XAML. The main window and detached windows are built from the same WinForms-shaped source files used by the Windows project. Native GTK3 is only the platform implementation underneath the compatibility layer.

The application icon resources are shared with Windows (`Assets/Glyphore.ico`, `Assets/Glyphore-Icon-1024.png`), and a Linux GTK icon bridge applies the PNG to top-level GTK windows while the ICO remains embedded for Windows/resource parity.

## ASCII Title Studio

The Title Studio was not recreated as a reduced Linux-only panel. The existing Windows-style implementation is included in the Linux build and opens as its own modeless window. It retains the same control hierarchy and title pipeline: multi-line FIGlet/prefab generation, visual presets, base font, palette, character set, typography options, animated title effects, live OpenGL preview, zoom/framing, background modes, detachable preview, mask editing, replay, and Apply/Add-to-scene operations.

Font discovery was changed from `System.Drawing.FontFamily.Families` to SkiaSharp's cross-platform font manager so Linux font enumeration does not depend on Windows GDI+.

## Linux OpenGL architecture

- `Gtk.GLArea` owns the Linux OpenGL context and framebuffer.
- OpenGL entry points are resolved through libepoxy while the GLArea context is current.
- The existing scene/effect/shader/glyph atlas/render/capture logic is retained.
- The framebuffer binding is queried inside the GLArea render callback.
- No WGL calls, `opengl32.dll`, or Windows device-context management remain in the Linux renderer.

## Validation performed on the source tree

- Balanced braces in edited C# files: passed.
- No legacy XAML UI package/reference in the project file: passed.
- No active `user32.dll`, `gdi32.dll`, `opengl32.dll`, or WGL reference in the project source: passed.
- No legacy XAML UI files remain in the project: passed.
- ASCII Title Studio entry point is present in `MainForm.Layout.cs`: passed.
- Windows-style Title Studio source files are included by the Linux `.csproj`: passed.
- Windows reference portable/UI file comparison: only intentional platform backend exclusions/replacements are absent.

## Build limitation

This execution environment does not contain the .NET SDK/compiler, and network package restore was unavailable here. A real `dotnet restore/build/publish` and physical Linux GPU test could therefore not be executed in this session. The delivered source archive is prepared for a Linux machine with .NET 10, GTK3, libepoxy and an OpenGL 3.3-capable driver.

## Build

```bash
./build-linux.sh
```

The script publishes a self-contained `linux-x64` single-file build to `dist/linux-x64/` and creates `dist/Glyphore-6.0.1-linux-x64.zip`. GTK3/libepoxy are native runtime dependencies of the UI/OpenGL layer.
## Build compatibility fix applied

The GTKSystem.Windows.Forms 1.3.24.89 target does not expose every protected WinForms lifecycle/input override used by the Windows implementation. The Linux port therefore maps those hooks to the corresponding public control events (`KeyDown`, `LostFocus`, `CheckedChanged`, `EnabledChanged`, `MouseCaptureChanged`, `Shown`, `HandleCreated`, `HandleDestroyed`) while keeping the Windows-style control hierarchy. GTK/GDK event argument types are explicitly qualified where they overlap with WinForms types. Generated source now declares `#nullable enable`.

