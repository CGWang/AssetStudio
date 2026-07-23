using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AssetStudio
{
    public static class ImageExtensions
    {
        public static void WriteToStream(this Image image, Stream stream, ImageFormat imageFormat)
        {
            switch (imageFormat)
            {
                case ImageFormat.Jpeg:
                    image.SaveAsJpeg(stream);
                    break;
                case ImageFormat.Png:
                    image.SaveAsPng(stream);
                    break;
                case ImageFormat.Bmp:
                    image.Save(stream, new BmpEncoder
                    {
                        BitsPerPixel = BmpBitsPerPixel.Pixel32,
                        SupportTransparency = true
                    });
                    break;
                case ImageFormat.Tga:
                    image.Save(stream, new TgaEncoder
                    {
                        BitsPerPixel = TgaBitsPerPixel.Pixel32,
                        Compression = TgaCompression.None
                    });
                    break;
            }
        }

        public static MemoryStream ConvertToStream(this Image image, ImageFormat imageFormat)
        {
            var stream = new MemoryStream();
            image.WriteToStream(stream, imageFormat);
            return stream;
        }

        public static byte[] ConvertToBytes<TPixel>(this Image<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
        {
            // Fast path: pixels stored in a single contiguous block.
            if (image.TryGetSinglePixelSpan(out var pixelSpan))
            {
                return MemoryMarshal.AsBytes(pixelSpan).ToArray();
            }

            // Large images are allocated by ImageSharp in discontiguous memory groups,
            // so TryGetSinglePixelSpan fails. A group boundary only falls between rows,
            // so each row span is still contiguous and can be copied out one at a time.
            if (image.Height == 0 || image.Width == 0)
            {
                return new byte[0];
            }
            var firstRow = MemoryMarshal.AsBytes(image.GetPixelRowSpan(0));
            var stride = firstRow.Length;
            var buffer = new byte[stride * image.Height];
            firstRow.CopyTo(buffer);
            for (int y = 1; y < image.Height; y++)
            {
                MemoryMarshal.AsBytes(image.GetPixelRowSpan(y)).CopyTo(buffer.AsSpan(y * stride));
            }
            return buffer;
        }
    }
}
