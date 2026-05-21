using AssetStudio;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Object = AssetStudio.Object;

namespace AssetStudioCLI
{
    internal class AssetItem
    {
        public Object Asset;
        public SerializedFile SourceFile;
        public string Container = string.Empty;
        public string TypeString;
        public string Text;
        public long m_PathID;
        public long FullSize;
        public ClassIDType Type;
        public string UniqueID;

        public AssetItem(Object asset)
        {
            Asset = asset;
            SourceFile = asset.assetsFile;
            Type = asset.type;
            TypeString = Type.ToString();
            m_PathID = asset.m_PathID;
            FullSize = asset.byteSize;
        }
    }

    internal class CLILogger : ILogger
    {
        public void Log(LoggerEvent loggerEvent, string message)
        {
            switch (loggerEvent)
            {
                case LoggerEvent.Error:
                    Console.Error.WriteLine($"[ERROR] {message}");
                    break;
                case LoggerEvent.Warning:
                    Console.Error.WriteLine($"[WARN] {message}");
                    break;
                case LoggerEvent.Info:
                    Console.WriteLine($"[INFO] {message}");
                    break;
                case LoggerEvent.Verbose:
                    break;
            }
        }
    }

    internal static class Program
    {
        private static AssetsManager assetsManager = new AssetsManager();
        private static List<AssetItem> exportableAssets = new List<AssetItem>();

        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                PrintUsage();
                return 1;
            }

            Logger.Default = new CLILogger();
            Progress.Default = new CLIProgress() as IProgress<int>;

            string inputPath = args[0];
            string outputPath = args[1];

            string typeFilter = null;
            string exportMode = "convert";
            string unityVersion = null;

            for (int i = 2; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-t":
                    case "--type":
                        if (i + 1 < args.Length) typeFilter = args[++i];
                        break;
                    case "-m":
                    case "--mode":
                        if (i + 1 < args.Length) exportMode = args[++i].ToLower();
                        break;
                    case "-v":
                    case "--unity-version":
                        if (i + 1 < args.Length) unityVersion = args[++i];
                        break;
                }
            }

            if (!string.IsNullOrEmpty(unityVersion))
            {
                assetsManager.SpecifyUnityVersion = unityVersion;
            }

            try
            {
                Console.WriteLine($"Loading assets from: {inputPath}");
                if (Directory.Exists(inputPath))
                {
                    assetsManager.LoadFolder(inputPath);
                }
                else if (File.Exists(inputPath))
                {
                    assetsManager.LoadFiles(inputPath);
                }
                else
                {
                    Console.Error.WriteLine($"Input path not found: {inputPath}");
                    return 1;
                }

                BuildAssetData();

                var assetsToExport = exportableAssets;
                if (!string.IsNullOrEmpty(typeFilter))
                {
                    var types = typeFilter.Split(',');
                    assetsToExport = assetsToExport.Where(a =>
                        types.Any(t => a.TypeString.Equals(t.Trim(), StringComparison.OrdinalIgnoreCase))
                    ).ToList();
                }

                Console.WriteLine($"Found {assetsToExport.Count} exportable assets" +
                    (typeFilter != null ? $" (filtered by: {typeFilter})" : ""));

                if (assetsToExport.Count == 0)
                {
                    Console.WriteLine("No assets to export.");
                    return 0;
                }

                Directory.CreateDirectory(outputPath);
                ExportAssets(assetsToExport, outputPath, exportMode);

                Console.WriteLine("Done.");
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Fatal error: {e}");
                return 1;
            }
            finally
            {
                assetsManager.Clear();
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("AssetStudio CLI - Unity Asset Extractor");
            Console.WriteLine();
            Console.WriteLine("Usage: AssetStudioCLI <input> <output> [options]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  input              File or folder to load");
            Console.WriteLine("  output             Output directory for exported assets");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -t, --type <types>         Filter by type (comma-separated)");
            Console.WriteLine("                             e.g. Texture2D,AudioClip,Mesh");
            Console.WriteLine("  -m, --mode <mode>          Export mode: convert (default), raw, dump");
            Console.WriteLine("  -v, --unity-version <ver>  Specify Unity version for stripped assets");
            Console.WriteLine();
            Console.WriteLine("Supported types: Texture2D, Sprite, AudioClip, Font, Mesh, TextAsset,");
            Console.WriteLine("  Shader, MovieTexture, VideoClip, MonoBehaviour, Animator, AnimationClip");
        }

        private static void BuildAssetData()
        {
            int i = 0;
            var containers = new List<(PPtr<Object>, string)>();

            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                foreach (var asset in assetsFile.Objects)
                {
                    var assetItem = new AssetItem(asset);
                    assetItem.UniqueID = "#" + i;
                    var exportable = false;

                    switch (asset)
                    {
                        case Texture2D m_Texture2D:
                            if (!string.IsNullOrEmpty(m_Texture2D.m_StreamData?.path))
                                assetItem.FullSize = asset.byteSize + m_Texture2D.m_StreamData.size;
                            assetItem.Text = m_Texture2D.m_Name;
                            exportable = true;
                            break;
                        case AudioClip m_AudioClip:
                            if (!string.IsNullOrEmpty(m_AudioClip.m_Source))
                                assetItem.FullSize = asset.byteSize + m_AudioClip.m_Size;
                            assetItem.Text = m_AudioClip.m_Name;
                            exportable = true;
                            break;
                        case VideoClip m_VideoClip:
                            if (!string.IsNullOrEmpty(m_VideoClip.m_OriginalPath))
                                assetItem.FullSize = asset.byteSize + (long)m_VideoClip.m_ExternalResources.m_Size;
                            assetItem.Text = m_VideoClip.m_Name;
                            exportable = true;
                            break;
                        case Shader m_Shader:
                            assetItem.Text = m_Shader.m_ParsedForm?.m_Name ?? m_Shader.m_Name;
                            exportable = true;
                            break;
                        case Mesh _:
                        case TextAsset _:
                        case AnimationClip _:
                        case Font _:
                        case MovieTexture _:
                        case Sprite _:
                            assetItem.Text = ((NamedObject)asset).m_Name;
                            exportable = true;
                            break;
                        case Animator m_Animator:
                            if (m_Animator.m_GameObject.TryGet(out var gameObject))
                            {
                                assetItem.Text = gameObject.m_Name;
                            }
                            exportable = true;
                            break;
                        case MonoBehaviour m_MonoBehaviour:
                            if (m_MonoBehaviour.m_Name == "" && m_MonoBehaviour.m_Script.TryGet(out var m_Script))
                            {
                                assetItem.Text = m_Script.m_ClassName;
                            }
                            else
                            {
                                assetItem.Text = m_MonoBehaviour.m_Name;
                            }
                            exportable = true;
                            break;
                        case AssetBundle m_AssetBundle:
                            foreach (var m_Container in m_AssetBundle.m_Container)
                            {
                                var preloadIndex = m_Container.Value.preloadIndex;
                                var preloadSize = m_Container.Value.preloadSize;
                                var preloadEnd = preloadIndex + preloadSize;
                                for (int k = preloadIndex; k < preloadEnd; k++)
                                {
                                    containers.Add((m_AssetBundle.m_PreloadTable[k], m_Container.Key));
                                }
                            }
                            assetItem.Text = m_AssetBundle.m_Name;
                            break;
                        case ResourceManager m_ResourceManager:
                            foreach (var m_Container in m_ResourceManager.m_Container)
                            {
                                containers.Add((m_Container.Value, m_Container.Key));
                            }
                            break;
                        default:
                            assetItem.Text = string.Empty;
                            break;
                    }

                    if (assetItem.Text == "")
                        assetItem.Text = assetItem.TypeString + assetItem.UniqueID;

                    if (exportable)
                    {
                        exportableAssets.Add(assetItem);
                    }

                    i++;
                }
            }

            foreach (var (pptr, container) in containers)
            {
                if (pptr.TryGet(out var obj))
                {
                    var item = exportableAssets.FirstOrDefault(a => a.Asset == obj);
                    if (item != null)
                    {
                        item.Container = container;
                    }
                }
            }

            containers.Clear();
        }

        private static void ExportAssets(List<AssetItem> assets, string outputPath, string mode)
        {
            int exported = 0;
            int failed = 0;

            for (int i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                var typePath = Path.Combine(outputPath, asset.TypeString);

                try
                {
                    bool success;
                    switch (mode)
                    {
                        case "raw":
                            success = ExportRawFile(asset, typePath);
                            break;
                        case "dump":
                            success = ExportDumpFile(asset, typePath);
                            break;
                        default:
                            success = ExportConvertFile(asset, typePath);
                            break;
                    }

                    if (success) exported++;
                    else failed++;
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"[ERROR] Failed to export {asset.Text}: {e.Message}");
                    failed++;
                }

                if ((i + 1) % 100 == 0 || i == assets.Count - 1)
                {
                    Console.WriteLine($"Progress: {i + 1}/{assets.Count} (exported: {exported}, failed: {failed})");
                }
            }

            Console.WriteLine($"Export complete: {exported} exported, {failed} failed");
        }

        private static string FixFileName(string str)
        {
            if (str.Length >= 260) return Path.GetRandomFileName();
            return Path.GetInvalidFileNameChars().Aggregate(str, (current, c) => current.Replace(c, '_'));
        }

        private static bool TryExportFile(string dir, AssetItem item, string extension, out string fullPath)
        {
            var fileName = FixFileName(item.Text);
            fullPath = Path.Combine(dir, fileName + extension);
            if (!File.Exists(fullPath))
            {
                Directory.CreateDirectory(dir);
                return true;
            }
            fullPath = Path.Combine(dir, fileName + item.UniqueID + extension);
            if (!File.Exists(fullPath))
            {
                Directory.CreateDirectory(dir);
                return true;
            }
            return false;
        }

        private static bool ExportConvertFile(AssetItem item, string exportPath)
        {
            switch (item.Type)
            {
                case ClassIDType.Texture2D:
                    return ExportTexture2D(item, exportPath);
                case ClassIDType.AudioClip:
                    return ExportAudioClip(item, exportPath);
                case ClassIDType.Shader:
                    return ExportShader(item, exportPath);
                case ClassIDType.TextAsset:
                    return ExportTextAsset(item, exportPath);
                case ClassIDType.MonoBehaviour:
                    return ExportMonoBehaviour(item, exportPath);
                case ClassIDType.Font:
                    return ExportFont(item, exportPath);
                case ClassIDType.Mesh:
                    return ExportMesh(item, exportPath);
                case ClassIDType.VideoClip:
                    return ExportVideoClip(item, exportPath);
                case ClassIDType.MovieTexture:
                    return ExportMovieTexture(item, exportPath);
                case ClassIDType.Sprite:
                    return ExportSprite(item, exportPath);
                default:
                    return ExportRawFile(item, exportPath);
            }
        }

        private static bool ExportTexture2D(AssetItem item, string exportPath)
        {
            var m_Texture2D = (Texture2D)item.Asset;
            if (!TryExportFile(exportPath, item, ".png", out var exportFullPath))
                return false;
            var image = m_Texture2D.ConvertToImage(true);
            if (image == null) return false;
            using (image)
            {
                using (var file = File.OpenWrite(exportFullPath))
                {
                    image.WriteToStream(file, ImageFormat.Png);
                }
                return true;
            }
        }

        private static bool ExportAudioClip(AssetItem item, string exportPath)
        {
            var m_AudioClip = (AudioClip)item.Asset;
            var m_AudioData = m_AudioClip.m_AudioData.GetData();
            if (m_AudioData == null || m_AudioData.Length == 0)
                return false;
            var converter = new AudioClipConverter(m_AudioClip);
            if (converter.IsSupport)
            {
                if (!TryExportFile(exportPath, item, ".wav", out var exportFullPath))
                    return false;
                var buffer = converter.ConvertToWav();
                if (buffer == null) return false;
                File.WriteAllBytes(exportFullPath, buffer);
            }
            else
            {
                if (!TryExportFile(exportPath, item, converter.GetExtensionName(), out var exportFullPath))
                    return false;
                File.WriteAllBytes(exportFullPath, m_AudioData);
            }
            return true;
        }

        private static bool ExportShader(AssetItem item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".shader", out var exportFullPath))
                return false;
            var m_Shader = (Shader)item.Asset;
            var str = m_Shader.Convert();
            File.WriteAllText(exportFullPath, str);
            return true;
        }

        private static bool ExportTextAsset(AssetItem item, string exportPath)
        {
            var m_TextAsset = (TextAsset)item.Asset;
            var extension = ".txt";
            if (!string.IsNullOrEmpty(item.Container))
            {
                extension = Path.GetExtension(item.Container);
                if (string.IsNullOrEmpty(extension)) extension = ".txt";
            }
            if (!TryExportFile(exportPath, item, extension, out var exportFullPath))
                return false;
            File.WriteAllBytes(exportFullPath, m_TextAsset.m_Script);
            return true;
        }

        private static bool ExportMonoBehaviour(AssetItem item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".json", out var exportFullPath))
                return false;
            var m_MonoBehaviour = (MonoBehaviour)item.Asset;
            var type = m_MonoBehaviour.ToType();
            if (type == null)
            {
                var m_Type = MonoBehaviourToTypeTree(m_MonoBehaviour);
                type = m_MonoBehaviour.ToType(m_Type);
            }
            var str = JsonConvert.SerializeObject(type, Formatting.Indented);
            File.WriteAllText(exportFullPath, str);
            return true;
        }

        private static TypeTree MonoBehaviourToTypeTree(MonoBehaviour m_MonoBehaviour)
        {
            if (!assemblyLoader.Loaded)
            {
                var openFolderDialog = new System.IO.DirectoryInfo(
                    Path.GetDirectoryName(m_MonoBehaviour.assetsFile.originalPath ?? m_MonoBehaviour.assetsFile.fullName));
                var managedPath = Path.Combine(openFolderDialog.FullName, "Managed");
                if (Directory.Exists(managedPath))
                {
                    assemblyLoader.Load(managedPath);
                }
            }
            return m_MonoBehaviour.ConvertToTypeTree(assemblyLoader);
        }

        private static AssemblyLoader assemblyLoader = new AssemblyLoader();

        private static bool ExportFont(AssetItem item, string exportPath)
        {
            var m_Font = (Font)item.Asset;
            if (m_Font.m_FontData == null) return false;
            var extension = ".ttf";
            if (m_Font.m_FontData.Length > 3 && m_Font.m_FontData[0] == 79 && m_Font.m_FontData[1] == 84 &&
                m_Font.m_FontData[2] == 84 && m_Font.m_FontData[3] == 79)
            {
                extension = ".otf";
            }
            if (!TryExportFile(exportPath, item, extension, out var exportFullPath))
                return false;
            File.WriteAllBytes(exportFullPath, m_Font.m_FontData);
            return true;
        }

        private static bool ExportMesh(AssetItem item, string exportPath)
        {
            var m_Mesh = (Mesh)item.Asset;
            if (m_Mesh.m_VertexCount <= 0) return false;
            if (!TryExportFile(exportPath, item, ".obj", out var exportFullPath))
                return false;
            var sb = new StringBuilder();
            sb.AppendLine("g " + m_Mesh.m_Name);
            if (m_Mesh.m_Vertices == null || m_Mesh.m_Vertices.Length == 0) return false;
            int c = 3;
            if (m_Mesh.m_Vertices.Length == m_Mesh.m_VertexCount * 4) c = 4;
            for (int v = 0; v < m_Mesh.m_VertexCount; v++)
            {
                sb.AppendFormat("v {0} {1} {2}\r\n", -m_Mesh.m_Vertices[v * c], m_Mesh.m_Vertices[v * c + 1], m_Mesh.m_Vertices[v * c + 2]);
            }
            if (m_Mesh.m_UV0?.Length > 0)
            {
                c = 4;
                if (m_Mesh.m_UV0.Length == m_Mesh.m_VertexCount * 2) c = 2;
                else if (m_Mesh.m_UV0.Length == m_Mesh.m_VertexCount * 3) c = 3;
                for (int v = 0; v < m_Mesh.m_VertexCount; v++)
                {
                    sb.AppendFormat("vt {0} {1}\r\n", m_Mesh.m_UV0[v * c], m_Mesh.m_UV0[v * c + 1]);
                }
            }
            if (m_Mesh.m_Normals?.Length > 0)
            {
                if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 3) c = 3;
                else if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 4) c = 4;
                for (int v = 0; v < m_Mesh.m_VertexCount; v++)
                {
                    sb.AppendFormat("vn {0} {1} {2}\r\n", -m_Mesh.m_Normals[v * c], m_Mesh.m_Normals[v * c + 1], m_Mesh.m_Normals[v * c + 2]);
                }
            }
            int sum = 0;
            for (var si = 0; si < m_Mesh.m_SubMeshes.Length; si++)
            {
                sb.AppendLine($"g {m_Mesh.m_Name}_{si}");
                int indexCount = (int)m_Mesh.m_SubMeshes[si].indexCount;
                var end = sum + indexCount / 3;
                for (int f = sum; f < end; f++)
                {
                    sb.AppendFormat("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\r\n",
                        m_Mesh.m_Indices[f * 3 + 2] + 1, m_Mesh.m_Indices[f * 3 + 1] + 1, m_Mesh.m_Indices[f * 3] + 1);
                }
                sum = end;
            }
            sb.Replace("NaN", "0");
            File.WriteAllText(exportFullPath, sb.ToString());
            return true;
        }

        private static bool ExportVideoClip(AssetItem item, string exportPath)
        {
            var m_VideoClip = (VideoClip)item.Asset;
            if (m_VideoClip.m_ExternalResources.m_Size <= 0) return false;
            if (!TryExportFile(exportPath, item, Path.GetExtension(m_VideoClip.m_OriginalPath), out var exportFullPath))
                return false;
            m_VideoClip.m_VideoData.WriteData(exportFullPath);
            return true;
        }

        private static bool ExportMovieTexture(AssetItem item, string exportPath)
        {
            var m_MovieTexture = (MovieTexture)item.Asset;
            if (!TryExportFile(exportPath, item, ".ogv", out var exportFullPath))
                return false;
            File.WriteAllBytes(exportFullPath, m_MovieTexture.m_MovieData);
            return true;
        }

        private static bool ExportSprite(AssetItem item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".png", out var exportFullPath))
                return false;
            var image = ((Sprite)item.Asset).GetImage();
            if (image == null) return false;
            using (image)
            {
                using (var file = File.OpenWrite(exportFullPath))
                {
                    image.WriteToStream(file, ImageFormat.Png);
                }
                return true;
            }
        }

        private static bool ExportRawFile(AssetItem item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".dat", out var exportFullPath))
                return false;
            File.WriteAllBytes(exportFullPath, item.Asset.GetRawData());
            return true;
        }

        private static bool ExportDumpFile(AssetItem item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".txt", out var exportFullPath))
                return false;
            var str = item.Asset.Dump();
            if (str != null)
            {
                File.WriteAllText(exportFullPath, str);
                return true;
            }
            return false;
        }
    }

    internal class CLIProgress : IProgress<int>
    {
        private int lastPercent = -1;

        public void Report(int value)
        {
            if (value != lastPercent && value % 10 == 0)
            {
                lastPercent = value;
                Console.Write($"\r  {value}%");
                if (value >= 100) Console.WriteLine();
            }
        }
    }
}
