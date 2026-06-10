using Nager.SmtpServerCore.ComponentModel;

namespace Nager.SmtpServerCore.Authentication
{
    /// <summary>
    /// User Authenticator Factory Interface
    /// </summary>
    public interface IUserAuthenticatorFactory : ISessionContextInstanceFactory<IUserAuthenticator> { }
}
