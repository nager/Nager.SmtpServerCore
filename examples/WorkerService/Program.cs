using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nager.SmtpServerCore;
using Nager.SmtpServerCore.Authentication;
using Nager.SmtpServerCore.Storage;

namespace WorkerService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices(
                    (hostContext, services) =>
                    {
                        services.AddTransient<IMessageStore, ConsoleMessageStore>();

                        services.AddSingleton(
                            provider =>
                            {
                                var options = new SmtpServerOptionsBuilder()
                                    .ServerName("SMTP Server")
                                    .Port(9025)
                                    .Build();

                                return new Nager.SmtpServerCore.SmtpServer(options, provider.GetRequiredService<IServiceProvider>());
                            });

                        services.AddHostedService<Worker>();
                    });
    }
}
