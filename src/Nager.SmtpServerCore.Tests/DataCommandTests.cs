using Nager.SmtpServerCore.ComponentModel;
using Nager.SmtpServerCore.IO;
using Nager.SmtpServerCore.Mail;
using Nager.SmtpServerCore.Protocol;
using Nager.SmtpServerCore.Storage;
using Nager.SmtpServerCore.Tests.Mocks;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nager.SmtpServerCore.Tests
{
    /// <summary>
    /// Verifies how <see cref="DataCommand"/> and <see cref="SmtpSession"/> translate the outcome of
    /// <see cref="IMessageStore.SaveAsync"/> into the SMTP reply the client receives.
    /// </summary>
    public sealed partial class DataCommandTests
    {
        const string Message = "Subject: test\r\n\r\nHello World\r\n";
        const string EndOfData = ".\r\n";

        [Fact]
        public async Task AcceptedMessage_RepliesWith250()
        {
            var smtp = SmtpTestSession.Create(Message + EndOfData, MessageStore.Default);

            var successful = await smtp.ExecuteDataCommandAsync();

            Assert.True(successful);
            Assert.EndsWith("250 Ok\r\n", smtp.ServerOutput);
        }

        [Fact]
        public async Task RejectedMessage_PropagatesTheResponseExceptionToTheSession()
        {
            // DataCommand must not swallow the exception: SmtpSession writes the reply,
            // otherwise the client never gets an answer to its message content.
            var rejection = Reject(SmtpReplyCode.MailboxUnavailable, "rejected by policy");
            var smtp = SmtpTestSession.Create(Message + EndOfData, MessageStoreThatThrows(rejection));

            var exception = await Record.ExceptionAsync(smtp.ExecuteDataCommandAsync);

            Assert.Same(rejection, exception);
        }

        [Fact]
        public async Task UnexpectedStoreFailure_RepliesWith554()
        {
            var storeFailure = new InvalidOperationException("storage is down");
            var smtp = SmtpTestSession.Create(Message + EndOfData, MessageStoreThatThrows(storeFailure));

            var successful = await smtp.ExecuteDataCommandAsync();

            Assert.False(successful);
            Assert.Contains("\r\n554 ", smtp.ServerOutput);
        }

        [Fact]
        public async Task OversizedMessage_InStrictMode_ClosesTheSessionWithAReply()
        {
            var oversizedMessage = "Subject: test\r\n\r\n" + new string('A', 500) + "\r\n";
            var smtp = SmtpTestSession.Create(
                oversizedMessage + EndOfData,
                MessageStore.Default,
                options => options.MaxMessageSize(50, MaxMessageSizeHandling.Strict));

            var exception = await Record.ExceptionAsync(smtp.ExecuteDataCommandAsync);

            var responseException = Assert.IsType<SmtpResponseException>(exception);
            Assert.Same(SmtpResponse.MaxMessageSizeExceeded, responseException.Response);
            Assert.True(responseException.IsQuitRequested, "strict mode must close the session");
        }

        [Fact]
        public async Task RejectedMessage_EndToEnd_ClientReceives550BeforeTheSessionCloses()
        {
            var rejection = Reject(SmtpReplyCode.MailboxUnavailable, "rejected by policy");
            var dialogue = string.Concat(
                "EHLO client.example.com\r\n",
                "MAIL FROM:<sender@example.com>\r\n",
                "RCPT TO:<recipient@example.com>\r\n",
                "DATA\r\n",
                Message, EndOfData,
                "QUIT\r\n");
            var smtp = SmtpTestSession.Create(dialogue, MessageStoreThatThrows(rejection));

            await smtp.RunSessionAsync();

            var output = smtp.ServerOutput;
            var rejectedAt = output.IndexOf("550 rejected by policy", StringComparison.Ordinal);
            var closedAt = output.IndexOf("221 ", StringComparison.Ordinal);

            Assert.True(rejectedAt >= 0, $"550 reply of the message store is missing in the server output:\r\n{output}");
            Assert.True(closedAt > rejectedAt, "the 550 reply must be sent before the 221 for QUIT");
        }

        // ----- test infrastructure -----

        static SmtpResponseException Reject(SmtpReplyCode code, string message)
            => new SmtpResponseException(new SmtpResponse(code, message));

        static IMessageStore MessageStoreThatThrows(Exception exception)
            => new ThrowingMessageStore(exception);

        /// <summary>
        /// A single SMTP session with a scripted client: the client input is fixed up front,
        /// everything the server writes back can be read from <see cref="ServerOutput"/>.
        /// </summary>
        sealed class SmtpTestSession
        {
            readonly SmtpSessionContext _context;
            readonly MockClientStream _client;

            SmtpTestSession(SmtpSessionContext context, MockClientStream client)
            {
                _context = context;
                _client = client;
            }

            public static SmtpTestSession Create(
                string clientInput,
                IMessageStore messageStore,
                Action<SmtpServerOptionsBuilder>? configure = null)
            {
                var client = new MockClientStream(clientInput);

                var serviceProvider = new ServiceProvider();
                serviceProvider.Add(messageStore);

                var options = new SmtpServerOptionsBuilder().ServerName("localhost");
                configure?.Invoke(options);

                var context = new SmtpSessionContext(serviceProvider, options.Build(), new EndpointDefinitionBuilder().Build())
                {
                    Pipe = new SecurableDuplexPipe(client, () => { })
                };

                // DataCommand expects an open transaction, as if MAIL FROM and RCPT TO were already accepted.
                context.Transaction.Reset();
                context.Transaction.From = new Mailbox("sender@example.com");
                context.Transaction.To.Add(new Mailbox("recipient@example.com"));

                return new SmtpTestSession(context, client);
            }

            public string ServerOutput => _client.ServerOutput;

            public Task<bool> ExecuteDataCommandAsync()
                => new DataCommand().ExecuteAsync(_context, CancellationToken.None);

            public Task RunSessionAsync()
                => new SmtpSession(_context).RunAsync(CancellationToken.None);
        }
    }
}
