using Nager.SmtpServerCore.ComponentModel;

namespace Nager.SmtpServerCore.Storage
{
    /// <summary>
    /// Message Store Factory Interface
    /// </summary>
    public interface IMessageStoreFactory : ISessionContextInstanceFactory<IMessageStore> { }
}
