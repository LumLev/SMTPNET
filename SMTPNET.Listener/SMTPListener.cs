using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using SMTPNET.Worker.Models;
using System.Security.Cryptography.X509Certificates;
using SMTPNET.Listener.Models;
using System.Globalization;

namespace SMTPNET.Listener
{
    public class SMTPListener
    {
        private readonly Socket listener;
        private ILogger _logger;


        public static string EmailsPath { get; private set; } = Path.Combine(AppContext.BaseDirectory, "emails");

        public SMTPListener(string ipAdressOrHost = "0.0.0.0", int port = 25, X509Certificate2? cert = null,
                            (string, string?)? certPathAndPassword = null, ILogger? logger = null)
        {

          X509Certificate2 certificate;
           if (cert is null)
            {
                if (certPathAndPassword.HasValue)
                {
                   certificate = new X509Certificate2(certPathAndPassword.Value.Item1, certPathAndPassword.Value.Item2);
                    
                    SMTPEncryptedResponse.SetServerTlsCertificate(certificate);
                }
            } 
          else 
            { 
                certificate = cert;
                SMTPEncryptedResponse.SetServerTlsCertificate(certificate);
            }


            listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(IPEndPoint.Parse($"{ipAdressOrHost}:{port}"));

            //  _hostName = Dns.GetHostName();
            if (Directory.Exists(EmailsPath) is false)  {  Directory.CreateDirectory(EmailsPath); }
            if (logger is not null) { _logger = logger; }
            else { _logger = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Information)).CreateLogger("Program"); }
            _logger.LogInformation($"Mail Directory: {EmailsPath}");
        }


        #region asynchronous
        // Much simpler to read than synchronous
        public async Task StartAcceptMailAsync(CancellationToken ct,int maxConcurrent = 5)
        {
            listener.Listen(maxConcurrent);
            await AcceptanEmailAsync(ct);
        }

        public async Task AcceptanEmailAsync(CancellationToken ct)
        {
            
            Socket incomingSocket = await listener.AcceptAsync(ct);

            // await Task.WhenAll(Task.Run(() => AcceptIncomingSocket(incomingSocket)),AcceptanEmailAsync(ct));
            await Parallel.ForEachAsync
                ([AcceptanEmailAsync(ct),
                  Task.Run(() => AcceptIncomingSocket(incomingSocket))],
                async (task, ct) => { await task.WaitAsync(ct); }
                );
        }
        #endregion

        #region AcceptingMail
        public void AcceptClient(IAsyncResult client)
        {
            Socket IncomingClient = listener.EndAccept(client);
            AcceptIncomingSocket(IncomingClient);
        }

        public void AcceptIncomingSocket(Socket incomingSocket)
        {
            string? IncomingIP = incomingSocket.RemoteEndPoint?.ToString()?.Split(":")[0];
            if (IncomingIP is not null)
            {
                var clienthost = Dns.GetHostEntry(IncomingIP);
                
                 _logger.LogInformation($"Accepted HostName: {clienthost.HostName}");
                 _logger.LogInformation($"RemoteEndPoint: {incomingSocket.RemoteEndPoint}");

                SMTPResponse sreq = new(incomingSocket, clienthost.HostName, _logger);
                bool x = sreq.ReceiveEmail();
                _logger.LogInformation($"EmailReceived: {x}");
                _logger.LogInformation($" \r\n========= Finished Receiving From: {sreq.Sender} =========");
            }
        }
        #endregion
    }
}

