using Nager.SmtpServerCore.IO;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Nager.SmtpServerCore.Protocol
{
    /// <summary>
    /// Quit Command
    /// </summary>
    public sealed class QuitCommand : SmtpCommand
    {
        /// <summary>
        /// Smtp Quit Command
        /// </summary>
        public const string Command = "QUIT";

        /// <summary>
        /// Constructor.
        /// </summary>
        public QuitCommand() : base(Command) { }

        /// <summary>
        /// Execute the command.
        /// </summary>
        /// <param name="context">The execution context to operate on.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns true if the command executed successfully such that the transition to the next state should occurr, false 
        /// if the current state is to be maintained.</returns>
        internal override async Task<bool> ExecuteAsync(SmtpSessionContext context, CancellationToken cancellationToken)
        {
            context.IsQuitRequested = true;

            try
            {
                await context.Pipe.Output.WriteReplyAsync(SmtpResponse.ServiceClosingTransmissionChannel, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ioException)
            {
                if (ioException.GetBaseException() is SocketException socketException)
                {
                    // Some mail servers will send the QUIT command and then disconnect before
                    // waiting for the 221 response from the server. This doesnt follow the spec but
                    // we can gracefully handle this situation as in theory everything should be fine
                    if (socketException.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        return true;
                    }
                }

                throw;
            }

            return true;
        }
    }
}
