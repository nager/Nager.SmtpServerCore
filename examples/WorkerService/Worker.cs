using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace WorkerService
{
    public sealed class Worker : BackgroundService
    {
        readonly Nager.SmtpServerCore.SmtpServer _smtpServer;

        public Worker(Nager.SmtpServerCore.SmtpServer smtpServer)
        {
            _smtpServer = smtpServer;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return _smtpServer.StartAsync(stoppingToken);
        }
    }
}
