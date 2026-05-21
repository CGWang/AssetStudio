using System;
using System.IO;

namespace AssetStudio
{
    internal class OffsetStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _offset;

        public OffsetStream(Stream baseStream, long offset)
        {
            _baseStream = baseStream;
            _offset = offset;
            _baseStream.Position = offset;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _baseStream.Length - _offset;

        public override long Position
        {
            get => _baseStream.Position - _offset;
            set => _baseStream.Position = value + _offset;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _baseStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _baseStream.Position = _offset + offset;
                    break;
                case SeekOrigin.Current:
                    _baseStream.Seek(offset, SeekOrigin.Current);
                    break;
                case SeekOrigin.End:
                    _baseStream.Position = _baseStream.Length + offset;
                    break;
            }
            return Position;
        }

        public override void Flush() => _baseStream.Flush();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _baseStream.Dispose();
            base.Dispose(disposing);
        }
    }
}
