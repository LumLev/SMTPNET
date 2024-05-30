using Microsoft.Extensions.Logging;
using SMTPNET.Extensions;
using SMTPNET.MailDns;
using SMTPNET.Sender.Extensions;
using SMTPNET.Sender.Models.Base;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Net.Mail;
using System.Net.Sockets;

namespace SMTPNET.Sender.Models
{
    public class SMTPRequest
    {
        private readonly Socket _socket;

        private readonly ILogger _logger;

        public SMTPRequest(Socket? theSocket = null, ILogger? logger = null) 
        {
            if (logger is not null) { _logger = logger; }
            else { _logger = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Information)).CreateLogger("Program"); }
            if (theSocket is not null)
            {
                _socket = theSocket;
            }
            else
            {
                _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
            _logger.LogInformation("Created SMTP Request");
        }

        public bool EndSuccess;
        public async Task<EmailSent[]> SendMessageAsync(MailMessage Message, bool dkimSigned = false)
        {
            List<EmailSent> emailsSent = new();
            var hosts = Message.To.Select(x => x.Host).Order();
            ArraySegment<byte> mailData;
            if (dkimSigned)
            {
                mailData = Message.GetMessageDataDkimSigned();
            }
            else
            {
                mailData = Message.GetMessageDataAsROSpan().ToArray();
            }
            _logger.LogInformation("Host Receivers: " + hosts.Count().ToString());
            foreach (string host in hosts)
            {
                MXRecord? record = new MailDns.MailDns().GetFirstMX(host);
                if (record is not null && record.mailServer is not null)
                {
                    _logger.LogInformation($"Sending message to mail server: {record.mailServer}");
                    Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    EmailSent[] someEmailsSent = await AcceptedMailSenderConnection(socket, host, record.mailServer, Message, mailData);
                    emailsSent.AddRange(someEmailsSent);
                }
                else
                {
                   
                    _logger.LogError($"The MX record of the target address:{host} could not be located:");
                }
            }
            return emailsSent.ToArray();
        }

        internal async Task<EmailSent[]> AcceptedMailSenderConnection(Socket connectionSocket, string hostName, string mailServerDomain, MailMessage Message, ArraySegment<byte> mailData)
        {
           
            if (Message.From is null)
            {
                _logger.LogError("MailMessage FROM(sender) is null");
                return new EmailSent[0];
            }
            await connectionSocket.ConnectAsync(mailServerDomain, 25);
            if (connectionSocket.Connected)
            {
                SMTPEncryptedDelivery delivery =new(socket: connectionSocket, mailServerAddress: mailServerDomain,
                                                    addresses: Message.To.Where(x => x.Host.EqualsAs(hostName)).ToArray(),
                                                     logger: _logger);
                delivery.SendEmail(Message.From, mailData);
                EndSuccess = delivery.EndSuccess;
                return delivery.CheckDelivery;
            }
            else
            {
                EndSuccess = false;
                return new EmailSent[0];
            }
        }

       

    }
}
