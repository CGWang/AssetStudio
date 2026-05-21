using System.Windows.Forms;
using AssetStudio;

namespace AssetStudioGUI
{
    internal class AssetItem : ListViewItem
    {
        public Object Asset;
        public SerializedFile SourceFile;
        public string Container = string.Empty;
        public string TypeString;
        public long m_PathID;
        public long FullSize;
        public ClassIDType Type;
        public string InfoText;
        public string UniqueID;
        public GameObjectTreeNode TreeNode;

        public AssetItem(Object asset)
        {
            Asset = asset;
            SourceFile = asset.assetsFile;
            Type = asset.type;
            TypeString = Type.ToString();
            m_PathID = asset.m_PathID;
            FullSize = asset.byteSize;
        }

        public void SetSubItems()
        {
            SubItems.AddRange(new[]
            {
                Container, //Container
                TypeString, //Type
                m_PathID.ToString(), //PathID
                FullSize.ToString(), //Size
                FormatSize(FullSize), //ReadableSize
            });
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024L)
                return $"{bytes} B";
            if (bytes < 1024L * 1024)
                return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024L * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024):F2} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
