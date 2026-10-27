using Nager.SmtpServerCore.ComponentModel;
using Nager.SmtpServerCore.IO;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Nager.SmtpServerCore.Tests
{
    /// <summary>
    /// Verifies the command loop of <see cref="SmtpSession"/> with a scripted client.
    /// </summary>
    public sealed class SmtpSessionTests
    {
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        static async Task<string[]> RunSessionAsync(string clientInput, int maxRetryCount)
        {
            var client = new FakeClientStream(clientInput);

            var options = new SmtpServerOptionsBuilder().ServerName("localhost").MaxRetryCount(maxRetryCount).Build();

            var context = new SmtpSessionContext(new ServiceProvider(), options, new EndpointDefinitionBuilder().Build())
            {
                Pipe = new SecurableDuplexPipe(client, () => { })
            };

            await new SmtpSession(context).RunAsync(TestContext.Current.CancellationToken).WaitAsync(Timeout, TestContext.Current.CancellationToken);

            // the first line is the greeting
            return client.ServerOutput.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
        }

        [Fact]
        public async Task ConsecutiveErrors_EndTheSessionAfterMaxRetryCount()
        {
            var replies = await RunSessionAsync("EHLO client\r\nFOO\r\nFOO\r\nFOO\r\nNOOP\r\n", maxRetryCount: 3);

            var errors = replies.Where(reply => reply.StartsWith('5')).ToArray();

            Assert.Equal(3, errors.Length);
            Assert.EndsWith("2 retry(ies) remaining.", errors[0]);
            Assert.EndsWith("1 retry(ies) remaining.", errors[1]);
            Assert.EndsWith("0 retry(ies) remaining.", errors[2]);
            Assert.Equal(errors[2], replies.Last());
        }

        [Fact]
        public async Task SuccessfulCommand_ResetsTheRetryCount()
        {
            var replies = await RunSessionAsync("EHLO client\r\nFOO\r\nFOO\r\nNOOP\r\nFOO\r\nFOO\r\nQUIT\r\n", maxRetryCount: 3);

            Assert.Equal(4, replies.Count(reply => reply.StartsWith('5')));
            Assert.DoesNotContain(replies, reply => reply.EndsWith("0 retry(ies) remaining."));
            Assert.StartsWith("221 ", replies.Last());
        }

        [Fact]
        public async Task CommandOutOfSequence_CountsAsError()
        {
            var replies = await RunSessionAsync("MAIL FROM:<sender@example.com>\r\nRCPT TO:<recipient@example.com>\r\nNOOP\r\n", maxRetryCount: 2);

            Assert.Equal(2, replies.Length);
            Assert.All(replies, reply => Assert.StartsWith("501 ", reply));
            Assert.EndsWith("0 retry(ies) remaining.", replies.Last());
        }

        /// <summary>
        /// Stands in for the network connection: reads return the scripted client input,
        /// writes are recorded as the server output.
        /// </summary>
        sealed class FakeClientStream : Stream
        {
            readonly MemoryStream _clientInput;
            readonly MemoryStream _serverOutput = new MemoryStream();

            public FakeClientStream(string clientInput)
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
}
