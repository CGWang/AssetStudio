# AssetStudio
[![Build status](https://ci.appveyor.com/api/projects/status/rnu7l90422pdewx4?svg=true)](https://ci.appveyor.com/project/Perfare/assetstudio/branch/master/artifacts)

**None of the repo, the tool, nor the repo owner is affiliated with, or sponsored or authorized by, Unity Technologies or its affiliates.**

[中文文档 (Chinese README)](README_zh.md)

AssetStudio is a tool for exploring, extracting and exporting assets and assetbundles.

## Features
* Support version:
  * 3.4 - 2022.2+ (partial Unity 6 support)
* Support asset types:
  * **Texture2D** : convert to png, tga, jpeg, bmp
  * **Sprite** : crop Texture2D to png, tga, jpeg, bmp
  * **AudioClip** : mp3, ogg, wav, m4a, fsb. support convert FSB file to WAV(PCM)
  * **Font** : ttf, otf
  * **Mesh** : obj
  * **TextAsset**
  * **Shader**
  * **MovieTexture**
  * **VideoClip**
  * **MonoBehaviour** : json
  * **Animator** : export to FBX file with bound AnimationClip

## What's New (Fork Enhancements)

### Unity Version Compatibility
* **Shader parsing fixes** for Unity 2021.3.10+, 2022.1.13+, and 2022.2+
  * Added `SerializedPlayerSubProgram` and `m_ParameterBlobIndices` support
  * Added `stageCounts` field handling for 2021.3.12+/2022.1.21+
  * Unity 6 (6000.x) shaders are detected and skipped gracefully
  * Shader parse failures no longer block the UI (downgraded from error to warning)
* **Texture2D parsing fixes** for Unity 2022.2+
  * Added `m_MipmapLimitGroupName` field support using TypeTree-based detection
  * Added safety bounds checking for `m_PlatformBlob` reading
* **Zstandard (Zstd) bundle decompression** for newer Unity versions
  * Added `ZstdSharp.Port` NuGet dependency

### Parallel File Loading
* Asset loading now uses `Parallel.For` across multiple CPU cores
* Thread-safe collections (`ConcurrentDictionary`) for shared state during load
* Wave-based dependency resolution: new files discovered during parallel load are queued for the next wave
* Significant speedup when loading large numbers of asset bundles

### CLI Tool (`AssetStudioCLI`)
A command-line interface for batch asset extraction without the GUI.

```
Usage: AssetStudioCLI <input> <output> [options]

Options:
  -t, --type <types>         Filter by type (comma-separated), e.g. Texture2D,AudioClip
  -m, --mode <mode>          Export mode: convert (default), raw, dump
  -v, --unity-version <ver>  Specify Unity version for stripped assets
```

Examples:
```bash
# Export all assets from a folder
AssetStudioCLI "C:\GameData" "C:\Output"

# Export only textures and audio
AssetStudioCLI "C:\GameData" "C:\Output" -t Texture2D,AudioClip

# Export raw asset data
AssetStudioCLI "C:\GameData" "C:\Output" -m raw
```

### Shader Export Improvements
* **Pretty-printed shader output** with proper indentation (4-space indent per nesting level)
  * All serialized shader structures now output with hierarchical indentation
  * `StringBuilder.Append` extension method for indent-aware string building
* **PlayerSubProgram export** for Unity 2021.3.10+/2022.1.13+ shaders
  * Exports `SerializedPlayerSubProgram` data alongside regular SubPrograms
  * `FlattenPlayerSubPrograms()` flattens 2D PlayerSubProgram arrays for export
  * Generic `ConvertSubPrograms<T>()` handles both SubProgram and PlayerSubProgram types
* **Lazy shader subprogram generation** via `ShaderSubProgramWrap`
  * Defers `ShaderSubProgram` parsing until export time, reducing upfront memory usage
* **`SerializedPropertyType.Int` fix** — integer shader properties now render correctly (rounded to int)

### GUI Command-Line Arguments
* Pass file/folder paths as command-line arguments to auto-load on startup
  * `AssetStudioGUI.exe "C:\GameData\assets"` — loads folder automatically
  * `AssetStudioGUI.exe file1.bundle file2.bundle` — loads multiple files
  * Invalid paths are silently skipped

### ACL Animation Decompression (Infrastructure)
* Added P/Invoke wrapper for `acl.dll` native library
* Added `ACLClip` class for ACL-compressed animation data
* Native `acl.dll` included for x86 and x64
* Note: ACL is used by some game-specific Unity builds (e.g., MiHoYo titles). The infrastructure is in place but not auto-detected for standard Unity assets.

### Improved Diagnostics
* Asset parse errors now include Unity version, byte offset, and byte size
* Parse failures are logged as warnings instead of blocking error dialogs
* `ObjectReader.Read()` includes bounds validation with diagnostic messages

## Requirements

- AssetStudio.net472
   - [.NET Framework 4.7.2](https://dotnet.microsoft.com/download/dotnet-framework/net472)
- AssetStudio.net5
   - [.NET Desktop Runtime 5.0](https://dotnet.microsoft.com/download/dotnet/5.0)
- AssetStudio.net6
   - [.NET Desktop Runtime 6.0](https://dotnet.microsoft.com/download/dotnet/6.0)


## Usage

### Load Assets/AssetBundles

Use **File-Load file** or **File-Load folder**.

When AssetStudio loads AssetBundles, it decompresses and reads it directly in memory, which may cause a large amount of memory to be used. You can use **File-Extract file** or **File-Extract folder** to extract AssetBundles to another folder, and then read.

### Extract/Decompress AssetBundles

Use **File-Extract file** or **File-Extract folder**.

### Export Assets

use **Export** menu.

### Export Model

Export model from "Scene Hierarchy" using the **Model** menu.

Export Animator from "Asset List" using the **Export** menu.

#### With AnimationClip

Select model from "Scene Hierarchy" then select the AnimationClip from "Asset List", using **Model-Export selected objects with AnimationClip** to export.

Export Animator will export bound AnimationClip or use **Ctrl** to select Animator and AnimationClip from "Asset List", using **Export-Export Animator with selected AnimationClip** to export.

### Export MonoBehaviour

When you select an asset of the MonoBehaviour type for the first time, AssetStudio will ask you the directory where the assembly is located, please select the directory where the assembly is located, such as the `Managed` folder.

#### For Il2Cpp

First, use my another program [Il2CppDumper](https://github.com/Perfare/Il2CppDumper) to generate dummy dll, then when using AssetStudio to select the assembly directory, select the dummy dll folder.

## Build

* Visual Studio 2022 or newer
* **AssetStudioFBXNative** uses [FBX SDK 2020.2.1](https://www.autodesk.com/developer-network/platform-technologies/fbx-sdk-2020-2-1), before building, you need to install the FBX SDK and modify the project file, change include directory and library directory to point to the FBX SDK directory

## Open source libraries used

### Texture2DDecoder
* [Ishotihadus/mikunyan](https://github.com/Ishotihadus/mikunyan)
* [BinomialLLC/crunch](https://github.com/BinomialLLC/crunch)
* [Unity-Technologies/crunch](https://github.com/Unity-Technologies/crunch/tree/unity)
