<p align="center">
  <img src="Assets/Glyphore-Logo.png" alt="Glyphoré — Procedural Character Art Studio" width="920" />
</p>

# Glyphoré 6.0.2 — Linux / Wayland port

**English** | [Español](#español)

Glyphoré is a GPU-accelerated procedural character art studio. This repository is the native Linux port for Arch-based distributions and Wayland desktops, preserving the same scene editor, real-time preview, and export pipeline while replacing the Windows-specific UI/GL stack with a GTK + OpenGL Linux implementation.

The live preview is rendered directly with OpenGL, and exported character output remains real selectable and copyable text.

## Highlights

- C# / .NET 10 + GTKSystem.Windows.Forms on Linux
- Native OpenGL 3.3 through GTK GLArea and libepoxy
- GPU-resident real-time preview
- Effect families and presets preserved from the upstream project
- Spanish and English interface
- Native Linux x64 builds for Arch/Wayland systems
- FIGlet/ASCII title rendering through Figgle 0.6.6
- Linux-friendly resource packaging and desktop launcher integration

Effects include 3D shapes, procedural terrain, SDF scenes, warp fields, fire, lightning, oceans, cellular automata, fractals, orbit systems, boids, particles, clouds, raymarch scenes, masks, and more.

## Linux / Wayland notes

This port is designed for Linux desktop environments using GTK and Wayland, with compatibility assumptions for Arch and Arch-based distributions. It keeps the editor structure and behavior of the Windows project while adapting the platform layer to Linux native APIs and resource handling.

Key notes:

- Native OpenGL context is owned by GTK `GLArea`
- Windows-only WGL and `opengl32.dll` dependencies are not used in the Linux path
- The project is built for `linux-x64` and can be launched from the repository or packaged into a binary distribution
- The Linux app shares the same scene format and visual pipeline as the upstream project
- The UI intentionally avoids unnecessary rounded geometry for a sharper terminal-like appearance

## Real-time editing

Effect parameters can still be modified while the animation is running.

Glyphoré provides:

- effect-specific controls
- live numeric sliders and numeric entry fields
- readable selectors for discrete options
- tooltips and inline descriptions
- live preset switching
- reset controls
- responsive parameter panels

## Scenes and layers

Glyphoré scenes can contain multiple procedural effect layers. You can:

- add, duplicate, remove, and rename layers
- reorder layers and change visibility
- adjust opacity and blend mode
- assign palette and character ramp per layer
- edit masks in layer space
- save and reopen `.glyphore` scenes

Scenes are stored as versioned project files, not only flattened exports.

## Undo and redo

The project keeps a built-in history for scene edits, parameter changes, masks, palette updates, and camera values, with `Ctrl+Z`, `Ctrl+Y`, and `Ctrl+Shift+Z` support.

## ASCII Title Studio

The ASCII Title Studio is preserved as a modeless detached window with live preview, prefab generation, style presets, and scene import. It keeps the same pipeline as the Windows upstream, adapted to Linux UI hosting.

## 3D camera

The camera controls remain available for SDF, terrain, and other 3D-driven effects.

- drag to orbit
- use the mouse wheel to zoom
- adjust numeric yaw/pitch
- reset when needed

## Character and color system

Glyphoré includes a palette and character system for ASCII and Unicode output, including live preview editing and export-friendly reconstruction of text.

## Import and export

Glyphoré can export scenes to:

- PowerShell
- selectable HTML
- JSON
- C# source
- ANSI
- plain TXT

The Linux port retains the same export flow and scene persistence but runs on a native Linux platform stack.

## Build

From the repository root:

```bash
dotnet restore Glyphore.csproj
dotnet build Glyphore.sln -c Release
```

For packaging:

```bash
./build-linux.sh
```

This creates a self-contained Linux release in `dist/linux-x64/` and a ZIP package under `dist/`.

## Requirements

### Linux

- Linux x64
- OpenGL 3.3-capable GPU and driver
- GTK3 runtime and related native dependencies
- .NET 10 SDK

This is the native port, not the Wine compatibility path.

## Licensing

The source code is published under the MIT license. See [LICENSE](LICENSE).

## Español

Glyphoré es un estudio procedural de arte con caracteres basado en GPU. Este repositorio es el port nativo para Linux, especialmente pensado para Arch y distribuciones basadas en Arch con entornos Wayland. Mantiene el mismo flujo de edición, vista previa en tiempo real y exportación que el proyecto original, pero reemplaza la capa de plataforma Windows por una implementación nativa de GTK + OpenGL.

La vista previa en tiempo real se renderiza directamente con OpenGL y la salida exportada sigue siendo texto real seleccionable y copiable.

La interfaz se mantiene funcional y compacta, con una estética más plana y menos redondeada para adaptarse mejor a entornos Linux/terminal-like y a una apariencia más nítida y sobria.

## Licencia

El código fuente de Glyphoré está publicado bajo la [licencia MIT](LICENSE).
