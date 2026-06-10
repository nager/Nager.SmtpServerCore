using Nager.SmtpServerCore.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace Nager.SmtpServerCore.Tests.Mocks
{
    internal class MockUserAuthenticator : IUserAuthenticator
    {
        private readonly string _username;
        private readonly string _password;

        public MockUserAuthenticator(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public Task<bool> AuthenticateAsync(
            ISessionContext context,
            string user,
            string password,
            CancellationToken cancellationToken)
        {
            if (user == this._username && password == this._password)
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }
}
