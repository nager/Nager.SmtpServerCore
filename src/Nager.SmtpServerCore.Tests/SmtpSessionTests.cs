using Nager.SmtpServerCore.ComponentModel;
using Nager.SmtpServerCore.IO;
using Nager.SmtpServerCore.Tests.Mocks;
using System;
using System.Linq;
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
            var client = new MockClientStream(clientInput);

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
    }
}
