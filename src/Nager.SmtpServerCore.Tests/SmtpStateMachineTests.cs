using Nager.SmtpServerCore.ComponentModel;
using Nager.SmtpServerCore.IO;
using Nager.SmtpServerCore.Mail;
using Nager.SmtpServerCore.Protocol;
using Nager.SmtpServerCore.StateMachine;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nager.SmtpServerCore.Tests
{
    /// <summary>
    /// Verifies which commands the <see cref="SmtpStateMachine"/> accepts in which state of the session.
    /// </summary>
    public sealed class SmtpStateMachineTests
    {
        static readonly IMailbox Mailbox = new Mailbox("user@example.com");
        static readonly IReadOnlyDictionary<string, string> NoParameters = new Dictionary<string, string>();

        static SmtpCommand Ehlo => new EhloCommand("client");
        static SmtpCommand Helo => new HeloCommand("client");
        static SmtpCommand Mail => new MailCommand(Mailbox, NoParameters);
        static SmtpCommand Rcpt => new RcptCommand(Mailbox);
        static SmtpCommand Data => new DataCommand();
        static SmtpCommand Rset => new RsetCommand();
        static SmtpCommand Noop => new NoopCommand();
        static SmtpCommand Quit => new QuitCommand();
        static SmtpCommand StartTls => new StartTlsCommand();
        static SmtpCommand Auth => new AuthCommand(AuthenticationMethod.Plain, null);

        static (SmtpStateMachine StateMachine, SmtpSessionContext Context) Create(bool isSecure = false, bool allowUnsecureAuthentication = false, bool withCertificate = false)
        {
            var endpoint = new EndpointDefinitionBuilder();

            if (allowUnsecureAuthentication)
            {
                endpoint.AllowUnsecureAuthentication();
            }

            if (withCertificate)
            {
                endpoint.Certificate(new NullCertificateFactory());
            }

            var context = new SmtpSessionContext(new ServiceProvider(), new SmtpServerOptionsBuilder().Build(), endpoint.Build())
            {
                Pipe = new FakePipe(isSecure)
            };

            return (new SmtpStateMachine(context), context);
        }

        /// <summary>
        /// Accepts the commands one after another as the session does when each command executes successfully.
        /// </summary>
        static void Accept(SmtpStateMachine stateMachine, SmtpSessionContext context, params SmtpCommand[] commands)
        {
            foreach (var command in commands)
            {
                Assert.True(stateMachine.TryAccept(command, out var errorResponse), $"{command.Name} was not accepted: {errorResponse?.Message}");

                stateMachine.Transition(context);
            }
        }

        static void AssertRejected(SmtpStateMachine stateMachine, SmtpCommand command)
        {
            Assert.False(stateMachine.TryAccept(command, out var errorResponse), $"{command.Name} must not be accepted");
            Assert.Equal(SmtpReplyCode.SyntaxError, errorResponse.ReplyCode);
        }

        [Fact]
        public void Initialized_AcceptsOnlyTheGreetingAndSessionCommands()
        {
            foreach (var command in new[] { Ehlo, Helo, Noop, Rset, Quit })
            {
                var (stateMachine, _) = Create();

                Assert.True(stateMachine.TryAccept(command, out _), $"{command.Name} must be accepted");
            }

            foreach (var command in new[] { Mail, Rcpt, Data, StartTls, Auth })
            {
                var (stateMachine, _) = Create(allowUnsecureAuthentication: true, withCertificate: true);

                AssertRejected(stateMachine, command);
            }
        }

        [Fact]
        public void Initialized_RejectionListsTheExpectedCommands()
        {
            var (stateMachine, _) = Create();

            stateMachine.TryAccept(Mail, out var errorResponse);

            Assert.Contains("EHLO", errorResponse.Message);
            Assert.Contains("HELO", errorResponse.Message);
        }

        [Fact]
        public void RcptAndData_RequireAPrecedingMail()
        {
            var (stateMachine, context) = Create();

            Accept(stateMachine, context, Ehlo);

            AssertRejected(stateMachine, Rcpt);
            AssertRejected(stateMachine, Data);
        }

        [Fact]
        public void Data_RequiresARecipient()
        {
            var (stateMachine, context) = Create();

            Accept(stateMachine, context, Ehlo, Mail);

            AssertRejected(stateMachine, Data);
            AssertRejected(stateMachine, Mail);
        }

        [Fact]
        public void CompleteTransaction_CanBeFollowedByTheNextTransaction()
        {
            var (stateMachine, context) = Create();

            Accept(stateMachine, context, Ehlo, Mail, Rcpt, Rcpt, Data, Mail, Rcpt, Data);
        }

        [Fact]
        public void Rset_EndsTheTransaction()
        {
            var (stateMachine, context) = Create();

            Accept(stateMachine, context, Ehlo, Mail, Rcpt, Rset);

            AssertRejected(stateMachine, Rcpt);
            AssertRejected(stateMachine, Data);
            Accept(stateMachine, context, Mail);
        }

        [Fact]
        public void WithinTransaction_AuthAndStartTlsAreRejected()
        {
            var (stateMachine, context) = Create(allowUnsecureAuthentication: true, withCertificate: true);

            Accept(stateMachine, context, Ehlo, Mail);

            AssertRejected(stateMachine, Auth);
            AssertRejected(stateMachine, StartTls);
            AssertRejected(stateMachine, Ehlo);
        }

        [Fact]
        public void Auth_OverAnUnsecureConnection_IsRejected()
        {
            var (stateMachine, context) = Create();

            Accept(stateMachine, context, Ehlo);

            AssertRejected(stateMachine, Auth);
        }

        [Fact]
        public void Auth_OverAnUnsecureConnection_IsAcceptedWhenAllowed()
        {
            var (stateMachine, context) = Create(allowUnsecureAuthentication: true);

            Accept(stateMachine, context, Ehlo, Auth);
        }

        [Fact]
        public void Auth_OverASecureConnection_IsAccepted()
        {
            var (stateMachine, context) = Create(isSecure: true);

            Accept(stateMachine, context, Ehlo, Auth);
        }

        [Fact]
        public void Auth_WhenAlreadyAuthenticated_IsRejected()
        {
            var (stateMachine, context) = Create(isSecure: true);

            Accept(stateMachine, context, Ehlo);
            context.Authentication = new AuthenticationContext("user");

            AssertRejected(stateMachine, Auth);
        }

        [Fact]
        public void StartTls_WithoutCertificate_IsRejected()
        {
            var (stateMachine, context) = Create();

            Accept(stateMachine, context, Ehlo);

            AssertRejected(stateMachine, StartTls);
        }

        [Fact]
        public void StartTls_WithCertificate_IsAccepted()
        {
            var (stateMachine, context) = Create(withCertificate: true);

            Accept(stateMachine, context, Ehlo);

            Assert.True(stateMachine.TryAccept(StartTls, out _));
        }

        [Fact]
        public void StartTls_OverASecureConnection_IsRejected()
        {
            var (stateMachine, context) = Create(isSecure: true, withCertificate: true);

            Accept(stateMachine, context, Ehlo);

            AssertRejected(stateMachine, StartTls);
        }

        sealed class NullCertificateFactory : ICertificateFactory
        {
            public X509Certificate GetServerCertificate(ISessionContext sessionContext) => null!;
        }

        sealed class FakePipe : ISecurableDuplexPipe
        {
            public FakePipe(bool isSecure)
            {
                IsSecure = isSecure;
            }

            public PipeReader Input { get; } = PipeReader.Create(new MemoryStream());

            public PipeWriter Output { get; } = PipeWriter.Create(new MemoryStream());

            public bool IsSecure { get; }

            public SslProtocols SslProtocol => SslProtocols.None;

            public Task UpgradeAsync(X509Certificate certificate, SslProtocols protocols, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public void Dispose() { }
        }
    }
}
