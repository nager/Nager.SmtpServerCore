using Nager.SmtpServerCore.Protocol;
using Nager.SmtpServerCore.Storage;
using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace Nager.SmtpServerCore.Tests.Mocks
{
    internal sealed class ThrowingMessageStore : MessageStore
    {
        readonly Exception _exception;

        public ThrowingMessageStore(Exception exception)
        {
            _exception = exception;
        }

        public override Task<SmtpResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
            => throw _exception;
    }
}
