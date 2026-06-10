using Nager.SmtpServerCore.ComponentModel;

namespace Nager.SmtpServerCore.Storage
{
    /// <summary>
    /// Mailbox Filter Factory Interface
    /// </summary>
    public interface IMailboxFilterFactory : ISessionContextInstanceFactory<IMailboxFilter> { }
}
