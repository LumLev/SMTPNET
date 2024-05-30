using System.Net;
using System.Net.Mail;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using SMTPNET.Listener;
using SMTPNET.Sender;
using SMTPNET.Sender.Extensions;
using SMTPNET.Sender.Models;
using SMTPNET.Sender.Models.Base;

namespace SmtpWorker
{
    public class Worker : BackgroundService
    {

        private readonly WebApplication _mailWebApp;

        private readonly SMTPListener _smtpListener;

        private readonly CustomLoggerProvider _loggerProvider;

        public Worker(ILogger<Worker> logger, IConfiguration config)
        {
            string ip = config.GetValue<string>("ip") ?? "0.0.0.0";
            int port = config.GetValue<int>("port");
            string certPath = config.GetValue<string>("certPath") ?? "";

            var webBuilder = WebApplication.CreateBuilder();

            string webEndPoint = config.GetValue<string>("webEndPoint") ?? "0.0.0.0:7001";
            

            _loggerProvider = new(logger);
            webBuilder.Logging.ClearProviders();
            webBuilder.Logging.AddProvider(_loggerProvider);

            if (certPath.Length > 0)
            {
                string? certPassword = config.GetValue<string?>("certPassword");
                if (certPassword?.Length == 0)
                {
                    certPassword = null;
                }
                logger.LogInformation($"Added cert: {certPath}");
                logger.LogInformation($"IP: {ip}, Port: {port}");
                X509Certificate2 cert = new(certPath, certPassword);
                _smtpListener = new(ip, cert: cert, logger: logger);
                webBuilder.WebHost.UseKestrel(kestrel =>
                {
                    kestrel.Listen(IPEndPoint.Parse(webEndPoint));
                   // kestrel.ConfigureHttpsDefaults(https => {  https.ServerCertificate = cert;  });
                });
            }
            else
            {
                webBuilder.WebHost.UseKestrel(kestrel => kestrel.Listen(IPEndPoint.Parse(webEndPoint)));
                logger.LogInformation($"IP: {ip}, Port: {port}");
                _smtpListener = new(ip, logger: _loggerProvider.CreateLogger("SMTPListener"));
            }
            
            _mailWebApp = webBuilder.Build();
            _mailWebApp.UseDefaultFiles();
            _mailWebApp.UseStaticFiles();
            _mailWebApp.MapGet("/", () => "Jelo from webmail");

            _mailWebApp.MapGet("/keys", () =>
            {
                return CurrentDkimKeys.GetCurrentOrNewDkimKeys();
            });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //await Parallel.ForEachAsync([_mailWebApp.RunAsync(stoppingToken), _smtpListener.StartAcceptMailAsync(stoppingToken)], async (task, ct) =>
            //{
            //    await task.WaitAsync(ct);
            //});
            await Task.WhenAll([_mailWebApp.RunAsync(stoppingToken), _smtpListener.StartAcceptMailAsync(stoppingToken)]);
        }
    }

    public class CustomLoggerProvider : ILoggerProvider
    {
        private readonly ILogger _logger;

        public CustomLoggerProvider(ILogger logger)
        {
            _logger = logger;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _logger;
        }

       public void Dispose()
        { }
    }
}
