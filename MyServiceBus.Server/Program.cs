using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;

namespace MyServiceBus.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args)
                .Build()
                .Run();
        }

        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            Startup.Settings = SettingsReader.Read();

            var httpPort = Startup.Settings.GetHttpPort();

            var http2Port = Startup.Settings.GetHttp2Port();

            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options =>
                        {

                            options.Listen(IPAddress.Any, httpPort,
                                o => o.Protocols = HttpProtocols.Http1AndHttp2);

                            options.Listen(IPAddress.Any, http2Port,
                                o => o.Protocols = HttpProtocols.Http2);

                        })

                        .UseStartup<Startup>()

                        .ConfigureLogging((context, logging) =>
                        {
                            // clear all previously registered providers
                            // logging.ClearProviders();

                            // now register everything you *really* want
                            // …
                        });
                    ;
                });
        }

    }
}