using SMTPNET.Listener;
namespace SMTPNET.WorkerReceiver
{
    public class Worker : BackgroundService
    {

        private readonly SMTPListener _smtpListener;

        public Worker(ILogger<Worker> logger, IConfiguration config)
        {
            string ip = config.GetValue<string>("ip") ?? "0.0.0.0";
            int port = config.GetValue<int>("port");
            string certPath = config.GetValue<string>("certPath") ?? "";
            

            if (certPath.Length > 0)
            {
                string? certPassword = config.GetValue<string?>("certPassword");
                if (certPassword?.Length == 0)
                {
                    certPassword = null;
                }
                logger.LogInformation($"Added cert: {certPath}");
                logger.LogInformation($"IP: {ip}, Port: {port}");
                _smtpListener = new(ip, certPathAndPassword: (certPath, certPassword), logger: logger);
            }
            else
            {
                logger.LogInformation($"IP: {ip}, Port: {port}");
                _smtpListener = new(ip, logger: logger);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
           await _smtpListener.StartAcceptMailAsync(stoppingToken);
        }
    }
}
