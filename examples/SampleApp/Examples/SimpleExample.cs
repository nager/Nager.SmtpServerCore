using Nager.SmtpServerCore;
using Nager.SmtpServerCore.ComponentModel;
using System;
using System.Threading;

namespace SampleApp.Examples
{
    public static class SimpleExample
    {
        public static void Run()
        {
            var cancellationTokenSource = new CancellationTokenSource();

            var options = new SmtpServerOptionsBuilder()
                .ServerName("SmtpServer SampleApp")
                .Port(9025)
                .Build();

            var serviceProvider = new ServiceProvider();
            serviceProvider.Add(new SampleMessageStore(Console.Out));
            
            var server = new SmtpServer(options, serviceProvider);
            var serverTask = server.StartAsync(cancellationTokenSource.Token);

            SampleMailClient.Send(); 

            cancellationTokenSource.Cancel();
            serverTask.WaitWithoutException();
        }
    }
}