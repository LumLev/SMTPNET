using Microsoft.Extensions.Logging;
using MimeKit;
using SMTPNET.Extensions;
using SMTPNET.MailDns;
using SMTPNET.Sender.Extensions;
using SMTPNET.Sender.Models.Base;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;

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
        public async Task<EmailSent[]> SendMessageAsync(MailMessage message, bool dkimSigned = false)
        {
            if (message.From is null)
            {
                if (message.Sender is null) { throw new Exception("Both message From and Sender are null"); }
                else { message.From = message.Sender; }
            }

            byte[] mailData = message.GetMessageDataDkimSigned();
            List<EmailSent> emailsSent = new();
            var mailboxes = message.To;
            foreach(var mailbox in message.CC){ mailboxes.Add(mailbox); }
            foreach(var mailbox in message.Bcc){ mailboxes.Add(mailbox); }
            var groupedMailboxesByHosts = mailboxes.GroupBy(email=> email.Host);
            
            foreach(var groupedMailboxes in groupedMailboxesByHosts)
            {
                MXRecord? record = new MailDns.MailDns().GetFirstMX(groupedMailboxes.Key);
                if (record is not null && record.mailServer is not null)
                {
                    _logger.LogInformation($"Sending message to mail server: {record.mailServer}");
                   EmailSent[] someEmailsSent = await AcceptedMailSenderConnection(record.mailServer, message.From,groupedMailboxes, mailData);
                    emailsSent.AddRange(someEmailsSent);
                }
                else
                {
                   
                    _logger.LogError($"The MX record of the target address:{groupedMailboxes.Key} could not be located:");
                }
            }
            return emailsSent.ToArray();
        }

        public async Task<EmailSent[]> SendMimeMessageAsync(MimeMessage message, Encoding encoder, bool dkimSigned = false)
        {
            string from;
            if (message.From is null)
            {
                throw new Exception("Both message From is null");
                
            }
            else { from = message.From[0].ToString(); }
            

            byte[] mailData = message.GetMessageDataDkimSigned(encoder);
            List<EmailSent> emailsSent = new();
            MailAddressCollection mailboxes = new();
            foreach(var mailbox in message.To) { mailboxes.Add(new(mailbox.ToString())); }
            foreach (var mailbox in message.Cc) { mailboxes.Add(new(mailbox.ToString())); }
            foreach (var mailbox in message.Bcc) {  mailboxes.Add(new(mailbox.ToString()));  }

            var groupedMailboxesByHosts = mailboxes.GroupBy(email => email.Host);

            foreach (var groupedMailboxes in groupedMailboxesByHosts)
            {
                MXRecord? record = new MailDns.MailDns().GetFirstMX(groupedMailboxes.Key);
                if (record is not null && record.mailServer is not null)
                {
                    _logger.LogInformation($"Sending message to mail server: {record.mailServer}");
                    EmailSent[] someEmailsSent = await AcceptedMailSenderConnection(record.mailServer, new(from), groupedMailboxes, mailData);
                    emailsSent.AddRange(someEmailsSent);
                }
                else
                {

                    _logger.LogError($"The MX record of the target address:{groupedMailboxes.Key} could not be located:");
                }
            }
            return emailsSent.ToArray();
        }
        internal async Task<EmailSent[]> AcceptedMailSenderConnection(string mailServerDomain, MailAddress from, IEnumerable<MailAddress> mailboxes, ArraySegment<byte> mailData)
        {
            await _socket.ConnectAsync(mailServerDomain, 25);
            if (_socket.Connected)
            {
                SMTPEncryptedDelivery delivery =new(socket: _socket, mailServerAddress: mailServerDomain,
                                                    addresses: mailboxes.ToArray(),
                                                     logger: _logger);
                delivery.SendEmail(from, mailData);
                EndSuccess = delivery.EndSuccess;
                return delivery.CheckDelivery;
            }
            else
            {
                EndSuccess = false;
                return [];
            }
        }
    }
}