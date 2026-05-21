# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

AssetStudio is a C# tool for exploring, extracting, and exporting Unity engine assets and asset bundles. It supports Unity versions 3.4 through 2022.x, with partial support for Unity 6 (6000.x) — shaders are skipped but other asset types load correctly.

## Build

Requires Visual Studio 2022+ with the C++ desktop workload. The solution targets multiple .NET frameworks simultaneously.

```powershell
# Restore NuGet packages
nuget restore

# Build all managed projects for .NET 4.7.2 (simplest — no publish step)
msbuild /p:Configuration=Release /p:TargetFramework=net472 /verbosity:minimal

# Build for .NET 6 (produces a publish-ready output)
msbuild /t:AssetStudioGUI:publish /p:Configuration=Release /p:TargetFramework=net6.0-windows /p:SelfContained=false /verbosity:minimal
```

**FBX SDK dependency**: `AssetStudioFBXNative` links against [Autodesk FBX SDK 2020.2.1](https://www.autodesk.com/developer-network/platform-technologies/fbx-sdk-2020-2-1). Install the SDK, then update `AssetStudioFBXNative.vcxproj` include/library paths to match. FBX export will still fail gracefully if the native DLL is absent (build uses `ContinueOnError="true"`).

**Native DLLs**: After building the two C++ projects (`AssetStudioFBXNative`, `Texture2DDecoderNative`) for both Win32 and x64, the GUI post-build target copies them into `x86/` and `x64/` subdirectories of the output folder. `DllLoader` in `AssetStudio.PInvoke` picks the correct architecture at runtime.

## Architecture

The solution has 9 projects organized in three tiers:

### Core Library (`AssetStudio/`)

Pure parsing logic with no UI or export dependencies. Key classes:

- **AssetsManager** — entry point for loading. Accepts files/folders, detects formats by magic bytes (`FileReader`), dispatches to `BundleFile`/`WebFile`/`SerializedFile` parsers, then runs a two-phase pipeline: `ReadAssets()` deserializes objects by ClassIDType, `ProcessAssets()` links GameObjects to their components (Transform, MeshRenderer, Animator, etc.). File loading is parallelized with `Parallel.For` and thread-safe collections (`ConcurrentDictionary`).
- **SerializedFile** — parses `.assets` files: header, type tree metadata, object table, and external references.
- **BundleFile** — parses UnityFS/UnityRaw/UnityWeb bundles, decompresses storage blocks (LZMA, LZ4, LZ4HC, Lzham, Zstd), and extracts contained files.
- **ObjectReader** — positioned binary reader for a single serialized object; each asset type class (Texture2D, Mesh, AnimationClip, etc.) reads from it in its constructor with version-specific branching.
- **ResourceReader** — lazy-loads external binary data (textures, audio stored in `.resource`/`.resS` files).

Asset type classes live in `AssetStudio/Classes/`. Each one deserializes itself from `ObjectReader` in its constructor, with extensive version branching for Unity format changes.

### Utility Layer (`AssetStudioUtility/`)

Format conversion and export logic. Depends on core + PInvoke + both wrappers.

- **Texture2DConverter** — decodes ~30 GPU texture formats by delegating to `Texture2DDecoderNative` via `TextureDecoder` wrapper.
- **ModelConverter** — collects mesh, skeleton, material, and animation data from GameObjects/Animators into an `IImported` intermediate representation.
- **ShaderConverter** — decompresses and reconstructs shader source from compiled bytecode blobs.
- **AssemblyLoader** — uses Mono.Cecil to load .NET assemblies for MonoBehaviour type resolution during export.

### CLI (`AssetStudioCLI/`)

Command-line tool for batch asset extraction. References core + utility. No WinForms dependency.

- **Program.cs** — entry point, argument parsing, asset loading, and export logic in a single file. Supports type filtering, export mode selection (convert/raw/dump), and Unity version override.

### GUI (`AssetStudioGUI/`)

Windows Forms application. References core + utility.

- **Studio.cs** — static coordinator holding the global `AssetsManager`, `AssemblyLoader`, and export dispatch logic. Bridges the form to the library.
- **Exporter.cs** — type-specific export methods (Texture2D→image, Mesh→OBJ, AudioClip→WAV, MonoBehaviour→JSON, etc.).
- **AssetStudioGUIForm** — main form with scene tree, asset list, OpenGL mesh preview, texture viewer, and audio player (FMOD).

### Native Interop

Two C++ DLLs with managed wrappers, bridged through `AssetStudio.PInvoke`:

| Native (C++/vcxproj) | Wrapper (C#) | Purpose |
|---|---|---|
| `Texture2DDecoderNative` | `Texture2DDecoderWrapper` | GPU texture format decoders (BCn, ETC, ASTC, PVRTC, Crunch) |
| `AssetStudioFBXNative` | `AssetStudioFBXWrapper` | FBX export via Autodesk FBX SDK (meshes, skins, animations, morph targets) |

`AssetStudio.PInvoke/DllLoader.cs` handles architecture detection (x86/x64) and loads the correct native DLL at runtime.

### Dependency Graph

```
AssetStudioGUI ─┐
AssetStudioCLI ─┤
                ├── AssetStudio (core)
                └── AssetStudioUtility
                      ├── AssetStudio (core)
                      ├── AssetStudio.PInvoke
                      ├── AssetStudioFBXWrapper → AssetStudioFBXNative (C++)
                      └── Texture2DDecoderWrapper → Texture2DDecoderNative (C++)
```

### Asset Loading Pipeline

1. **Detect** — `FileReader` reads magic bytes → classifies as Bundle, WebFile, GZip, Brotli, Zip, or raw SerializedFile
2. **Unpack** — `BundleFile`/`WebFile` decompresses and extracts contained `StreamFile`s
3. **Parse** — `SerializedFile` reads header, type trees, object table, external references
4. **Deserialize** — `ReadAssets()` creates typed objects (Texture2D, Mesh, etc.) from `ObjectReader`
5. **Link** — `ProcessAssets()` wires GameObjects to components and resolves SpriteAtlas references
6. **Export** — converter + exporter pair transforms the in-memory object to an output format

## Key NuGet Dependencies

- **K4os.Compression.LZ4** — LZ4 decompression for bundles and shaders
- **ZstdSharp.Port** — Zstandard decompression for newer Unity bundles
- **Mono.Cecil** — .NET assembly inspection for MonoBehaviour type resolution
- **SixLabors.ImageSharp.Drawing** — sprite cropping and image composition
- **OpenTK** — OpenGL rendering for mesh preview in GUI
- **Newtonsoft.Json** — MonoBehaviour JSON export
- **FMOD** (bundled DLLs in `AssetStudioGUI/Libraries/`) — audio playback

## Version Compatibility Notes

Shader parsing has version-specific branching throughout `Shader.cs`. Key version boundaries:
- **2021.3.10f1+** / **2022.1.13f1+**: `SerializedPlayerSubProgram` and `m_ParameterBlobIndices` added to `SerializedProgram`
- **2021.3.12f1+** / **2022.1.21f1+**: `stageCounts` field added to `Shader`
- **2022.1+**: `m_SerializedKeywordStateMask` in `SerializedProgram`
- **2022.2+**: `m_MipmapLimitGroupName` in `Texture2D`
- **Unity 6 (6000.x)**: Shader parsing is skipped entirely (format too different); other assets use TypeTree-based field detection

If an asset fails to parse, the error is caught and logged as a Warning (not Error), so the GUI doesn't pop up blocking dialogs. The object is still added to the asset list when possible (Shader retains its name from NamedObject base class).

## Debugging Asset Parse Failures

When `ReadAssets()` catches a parse exception, it logs: Unity version, asset file name, path, ClassIDType, PathID, byte offset, and byte size. Check these fields against the version branching in the relevant `Classes/*.cs` file to find missing version checks. Community forks to reference for newer format changes:
- **Razviar/assetstudio** (most current, supports Unity 6, archived but comprehensive)
- **RazTools/Studio** (MiHoYo-focused, archived May 2024)

## CI

GitHub Actions workflow (`.github/workflows/build.yml`) builds for net472, net5.0-windows, and net6.0-windows on `windows-latest`. It downloads and installs the FBX SDK during the build.
