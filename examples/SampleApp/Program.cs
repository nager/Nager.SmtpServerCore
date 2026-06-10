using SampleApp.Examples;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace SampleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            ServicePointManager.ServerCertificateValidationCallback = IgnoreCertificateValidationFailureForTestingOnly;

            //SimpleExample.Run();
            //SimpleServerExample.Run();
            //CustomEndpointListenerExample.Run();
            //ServerCancellingExample.Run();
            SessionTracingExample.Run();
            //DependencyInjectionExample.Run();
            //SecureServerExample.Run();
        }

        static bool IgnoreCertificateValidationFailureForTestingOnly(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}