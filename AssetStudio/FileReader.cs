using System;
using System.IO;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    public class FileReader : EndianBinaryReader
    {
        public string FullPath;
        public string FileName;
        public FileType FileType;

        private static readonly byte[] gzipMagic = { 0x1f, 0x8b };
        private static readonly byte[] brotliMagic = { 0x62, 0x72, 0x6F, 0x74, 0x6C, 0x69 };
        private static readonly byte[] zipMagic = { 0x50, 0x4B, 0x03, 0x04 };
        private static readonly byte[] zipSpannedMagic = { 0x50, 0x4B, 0x07, 0x08 };

        private static readonly byte[][] bundleSignatures =
        {
            Encoding.ASCII.GetBytes("UnityFS\0"),
            Encoding.ASCII.GetBytes("UnityRaw\0"),
            Encoding.ASCII.GetBytes("UnityWeb\0"),
            Encoding.ASCII.GetBytes("UnityArchive\0"),
        };

        public FileReader(string path) : this(path, OpenWithOffsetDetection(path)) { }

        public FileReader(string path, Stream stream) : base(stream, EndianType.BigEndian)
        {
            FullPath = Path.GetFullPath(path);
            FileName = Path.GetFileName(path);
            FileType = CheckFileType();
        }

        private static Stream OpenWithOffsetDetection(string path)
        {
            var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long offset = DetectBundleOffset(stream);
            if (offset > 0)
            {
                Logger.Verbose($"Detected bundle offset {offset} (0x{offset:X}) in {Path.GetFileName(path)}");
                return new OffsetStream(stream, offset);
            }
            return stream;
        }

        private static long DetectBundleOffset(Stream stream)
        {
            if (stream.Length < 16) return 0;

            var scanLength = (int)Math.Min(1024, stream.Length);
            var buffer = new byte[scanLength];
            var origPos = stream.Position;
            stream.Position = 0;
            stream.Read(buffer, 0, scanLength);
            stream.Position = origPos;

            // If the file already starts with a known signature, no offset needed
            foreach (var sig in bundleSignatures)
            {
                if (scanLength >= sig.Length && MatchesAt(buffer, 0, sig))
                    return 0;
            }

            // Scan for signatures at non-zero offsets
            foreach (var sig in bundleSignatures)
            {
                for (int i = 1; i <= scanLength - sig.Length; i++)
                {
                    if (MatchesAt(buffer, i, sig))
                        return i;
                }
            }

            return 0;
        }

        private static bool MatchesAt(byte[] buffer, int offset, byte[] signature)
        {
            for (int j = 0; j < signature.Length; j++)
            {
                if (buffer[offset + j] != signature[j])
                    return false;
            }
            return true;
        }

        private FileType CheckFileType()
        {
            var signature = this.ReadStringToNull(20);
            Position = 0;
            switch (signature)
            {
                case "UnityWeb":
                case "UnityRaw":
                case "UnityArchive":
                case "UnityFS":
                    return FileType.BundleFile;
                case "UnityWebData1.0":
                    return FileType.WebFile;
                default:
                    {
                        byte[] magic = ReadBytes(2);
                        Position = 0;
                        if (gzipMagic.SequenceEqual(magic))
                        {
                            return FileType.GZipFile;
                        }
                        Position = 0x20;
                        magic = ReadBytes(6);
                        Position = 0;
                        if (brotliMagic.SequenceEqual(magic))
                        {
                            return FileType.BrotliFile;
                        }
                        if (IsSerializedFile())
                        {
                            return FileType.AssetsFile;
                        }
                        magic = ReadBytes(4);
                        Position = 0;
                        if (zipMagic.SequenceEqual(magic) || zipSpannedMagic.SequenceEqual(magic))
                            return FileType.ZipFile;
                        return FileType.ResourceFile;
                    }
            }
        }

        private bool IsSerializedFile()
        {
            var fileSize = BaseStream.Length;
            if (fileSize < 20)
            {
                return false;
            }
            var m_MetadataSize = ReadUInt32();
            long m_FileSize = ReadUInt32();
            var m_Version = ReadUInt32();
            long m_DataOffset = ReadUInt32();
            var m_Endianess = ReadByte();
            var m_Reserved = ReadBytes(3);
            if (m_Version >= 22)
            {
                if (fileSize < 48)
                {
                    Position = 0;
                    return false;
                }
                m_MetadataSize = ReadUInt32();
                m_FileSize = ReadInt64();
                m_DataOffset = ReadInt64();
            }
            Position = 0;
            if (m_FileSize != fileSize)
            {
                return false;
            }
            if (m_DataOffset > fileSize)
            {
                return false;
            }
            return true;
        }
    }
}
