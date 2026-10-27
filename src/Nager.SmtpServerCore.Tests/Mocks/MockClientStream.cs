using System;
using System.IO;
using System.Text;

namespace Nager.SmtpServerCore.Tests.Mocks
{
    internal sealed class MockClientStream : Stream
    {
        readonly MemoryStream _clientInput;
        readonly MemoryStream _serverOutput = new MemoryStream();

        public MockClientStream(string clientInput)
        {
            _clientInput = new MemoryStream(Encoding.ASCII.GetBytes(clientInput));
        }

        public string ServerOutput => Encoding.ASCII.GetString(_serverOutput.ToArray());

        public override int Read(byte[] buffer, int offset, int count) => _clientInput.Read(buffer, offset, count);
        public override void Write(byte[] buffer, int offset, int count) => _serverOutput.Write(buffer, offset, count);
        public override void Flush() { }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
