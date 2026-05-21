# AssetStudio
[![Build status](https://ci.appveyor.com/api/projects/status/rnu7l90422pdewx4?svg=true)](https://ci.appveyor.com/project/Perfare/assetstudio/branch/master/artifacts)

**本仓库、工具及仓库所有者与 Unity Technologies 及其关联公司无任何关联、赞助或授权关系。**

AssetStudio 是一个用于浏览、提取和导出 Unity 资源（assets）和资源包（assetbundles）的工具。

## 功能特性
* 支持版本：
  * 3.4 - 2022.2+（部分支持 Unity 6）
* 支持的资源类型：
  * **Texture2D**：转换为 png、tga、jpeg、bmp
  * **Sprite**：裁剪 Texture2D 为 png、tga、jpeg、bmp
  * **AudioClip**：mp3、ogg、wav、m4a、fsb，支持将 FSB 文件转换为 WAV(PCM)
  * **Font**：ttf、otf
  * **Mesh**：obj
  * **TextAsset**
  * **Shader**
  * **MovieTexture**
  * **VideoClip**
  * **MonoBehaviour**：json
  * **Animator**：导出为带绑定 AnimationClip 的 FBX 文件

## Fork 新增特性

### Unity 版本兼容性
* **Shader 解析修复**，支持 Unity 2021.3.10+、2022.1.13+ 和 2022.2+
  * 新增 `SerializedPlayerSubProgram` 和 `m_ParameterBlobIndices` 支持
  * 新增 2021.3.12+/2022.1.21+ 的 `stageCounts` 字段处理
  * Unity 6 (6000.x) 的 Shader 被检测并优雅跳过
  * Shader 解析失败不再阻塞 UI（从错误降级为警告）
* **Texture2D 解析修复**，支持 Unity 2022.2+
  * 新增基于 TypeTree 检测的 `m_MipmapLimitGroupName` 字段支持
  * 新增 `m_PlatformBlob` 读取的安全边界检查
* **Zstandard (Zstd) 资源包解压**，支持较新的 Unity 版本
  * 新增 `ZstdSharp.Port` NuGet 依赖

### 并行文件加载
* 资源加载现使用 `Parallel.For` 跨多 CPU 核心并行处理
* 使用线程安全集合（`ConcurrentDictionary`）管理加载期间的共享状态
* 波次式依赖解析：并行加载中发现的新文件排入下一波次处理
* 加载大量资源包时有显著的速度提升

### 命令行工具（`AssetStudioCLI`）
无需 GUI 的批量资源提取命令行工具。

```
用法: AssetStudioCLI <输入路径> <输出路径> [选项]

选项:
  -t, --type <类型>          按类型过滤（逗号分隔），例如 Texture2D,AudioClip
  -m, --mode <模式>          导出模式: convert（默认）、raw、dump
  -v, --unity-version <版本> 为精简资源指定 Unity 版本
```

示例：
```bash
# 导出文件夹中的所有资源
AssetStudioCLI "C:\GameData" "C:\Output"

# 仅导出贴图和音频
AssetStudioCLI "C:\GameData" "C:\Output" -t Texture2D,AudioClip

# 导出原始资源数据
AssetStudioCLI "C:\GameData" "C:\Output" -m raw
```

### Shader 导出改进
* **美化的 Shader 输出**，带正确的缩进（每层嵌套 4 空格缩进）
  * 所有序列化 Shader 结构现在以层次缩进方式输出
  * `StringBuilder.Append` 扩展方法支持缩进感知的字符串构建
* **PlayerSubProgram 导出**，支持 Unity 2021.3.10+/2022.1.13+ 的 Shader
  * 在常规 SubPrograms 之外额外导出 `SerializedPlayerSubProgram` 数据
  * `FlattenPlayerSubPrograms()` 展平二维 PlayerSubProgram 数组用于导出
  * 泛型 `ConvertSubPrograms<T>()` 同时处理 SubProgram 和 PlayerSubProgram 类型
* **延迟 Shader 子程序生成**，通过 `ShaderSubProgramWrap` 实现
  * 将 `ShaderSubProgram` 解析推迟到导出时，减少前期内存占用
* **`SerializedPropertyType.Int` 修复** — 整数类型的 Shader 属性现在正确渲染（四舍五入为整数）

### GUI 命令行参数
* 通过命令行参数传入文件/文件夹路径，启动时自动加载
  * `AssetStudioGUI.exe "C:\GameData\assets"` — 自动加载文件夹
  * `AssetStudioGUI.exe file1.bundle file2.bundle` — 加载多个文件
  * 无效路径会被静默跳过

### ACL 动画解压（基础设施）
* 新增 `acl.dll` 原生库的 P/Invoke 封装
* 新增 `ACLClip` 类，用于 ACL 压缩的动画数据
* 包含 x86 和 x64 的原生 `acl.dll`
* 注：ACL 用于某些游戏特定的 Unity 构建（如米哈游系列）。基础设施已就绪，但不会自动检测标准 Unity 资源。

### 改进的诊断信息
* 资源解析错误现在包含 Unity 版本、字节偏移和字节大小
* 解析失败记录为警告而非弹出阻塞式错误对话框
* `ObjectReader.Read()` 包含带诊断信息的边界验证

## 系统要求

- AssetStudio.net472
   - [.NET Framework 4.7.2](https://dotnet.microsoft.com/download/dotnet-framework/net472)
- AssetStudio.net5
   - [.NET Desktop Runtime 5.0](https://dotnet.microsoft.com/download/dotnet/5.0)
- AssetStudio.net6
   - [.NET Desktop Runtime 6.0](https://dotnet.microsoft.com/download/dotnet/6.0)


## 使用方法

### 加载资源/资源包

使用 **File-Load file** 或 **File-Load folder**。

AssetStudio 加载资源包时会直接在内存中解压和读取，可能导致大量内存占用。你可以使用 **File-Extract file** 或 **File-Extract folder** 将资源包提取到另一个文件夹，然后再读取。

### 提取/解压资源包

使用 **File-Extract file** 或 **File-Extract folder**。

### 导出资源

使用 **Export** 菜单。

### 导出模型

从 "Scene Hierarchy" 使用 **Model** 菜单导出模型。

从 "Asset List" 使用 **Export** 菜单导出 Animator。

#### 带 AnimationClip 导出

从 "Scene Hierarchy" 选择模型，然后从 "Asset List" 选择 AnimationClip，使用 **Model-Export selected objects with AnimationClip** 导出。

导出 Animator 会导出绑定的 AnimationClip，或者使用 **Ctrl** 从 "Asset List" 中同时选择 Animator 和 AnimationClip，使用 **Export-Export Animator with selected AnimationClip** 导出。

### 导出 MonoBehaviour

首次选择 MonoBehaviour 类型的资源时，AssetStudio 会询问程序集所在的目录，请选择程序集所在目录，例如 `Managed` 文件夹。

#### 对于 Il2Cpp

首先使用 [Il2CppDumper](https://github.com/Perfare/Il2CppDumper) 生成 dummy dll，然后在 AssetStudio 中选择程序集目录时，选择 dummy dll 文件夹。

## 构建

* Visual Studio 2022 或更新版本
* **AssetStudioFBXNative** 使用 [FBX SDK 2020.2.1](https://www.autodesk.com/developer-network/platform-technologies/fbx-sdk-2020-2-1)，构建前需要安装 FBX SDK 并修改项目文件，将 include 目录和 library 目录指向 FBX SDK 目录

## 使用的开源库

### Texture2DDecoder
* [Ishotihadus/mikunyan](https://github.com/Ishotihadus/mikunyan)
* [BinomialLLC/crunch](https://github.com/BinomialLLC/crunch)
* [Unity-Technologies/crunch](https://github.com/Unity-Technologies/crunch/tree/unity)
