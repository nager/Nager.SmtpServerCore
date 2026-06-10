using Nager.SmtpServerCore.ComponentModel;
using Nager.SmtpServerCore.IO;
using Nager.SmtpServerCore.Protocol;
using Nager.SmtpServerCore.Tests.Mocks;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nager.SmtpServerCore.Tests
{
    public class AuthCommandTests
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public AuthCommandTests()
        {
        }

        private SmtpSessionContext GetSmtpSessionContextWithUserAuthenticator()
        {
            var memoryStream = new MemoryStream();

            var serviceProvider = new ServiceProvider();
            serviceProvider.Add(new MockUserAuthenticator(username: "test", password: "1234"));
            var smtpServerOptions = new SmtpServerOptionsBuilder().Build();
            var endpointDefinitions = new EndpointDefinitionBuilder().Build();
            var smtpSessionContext = new SmtpSessionContext(serviceProvider, smtpServerOptions, endpointDefinitions);
            smtpSessionContext.Pipe = new SecurableDuplexPipe(memoryStream, () => { });

            return smtpSessionContext;
        }

        private SmtpSessionContext GetSmtpSessionContextWithoutUserAuthenticator()
        {
            var memoryStream = new MemoryStream();

            var serviceProvider = new ServiceProvider();
            var smtpServerOptions = new SmtpServerOptionsBuilder().Build();
            var endpointDefinitions = new EndpointDefinitionBuilder().Build();
            var smtpSessionContext = new SmtpSessionContext(serviceProvider, smtpServerOptions, endpointDefinitions);
            smtpSessionContext.Pipe = new SecurableDuplexPipe(memoryStream, () => { });

            return smtpSessionContext;
        }

        [Fact]
        public async Task Test_Login_WithoutUserAuthenticator()
        {
            //TODO: missing logic for password next line

            var smtpSessionContext = this.GetSmtpSessionContextWithoutUserAuthenticator();

            var authCommand = new AuthCommand(AuthenticationMethod.Login, "myuser");
            var successful = await authCommand.ExecuteAsync(smtpSessionContext, CancellationToken.None);

            Assert.False(successful);
        }

        [Fact]
        public async Task Test_Login_WithUserAuthenticator()
        {
            //TODO: missing logic for password next line

            var smtpSessionContext = this.GetSmtpSessionContextWithUserAuthenticator();

            var authCommand = new AuthCommand(AuthenticationMethod.Login, "myuser");
            var successful = await authCommand.ExecuteAsync(smtpSessionContext, CancellationToken.None);

            Assert.False(successful);
        }

        [Fact]
        public async Task Test_Plain_WithUserAuthenticator()
        {
            var smtpSessionContext = this.GetSmtpSessionContextWithUserAuthenticator();

            var authCommand = new AuthCommand(AuthenticationMethod.Plain, "dGVzdAB0ZXN0ADEyMzQ=");
            var successful = await authCommand.ExecuteAsync(smtpSessionContext, CancellationToken.None);

            Assert.True(successful);
        }
    }
}
