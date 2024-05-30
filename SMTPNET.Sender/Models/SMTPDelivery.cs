using Microsoft.Extensions.Logging;
using SMTPNET.Extensions;
using SMTPNET.Sender.Models.Base;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;

namespace SMTPNET.Sender.Models
{
    public ref struct SMTPDelivery
    {

        public Socket TheSocket { get; set; }

        private readonly ReadOnlySpan<byte> _mailData;
        private readonly ILogger _logger;

        public SMTPDelivery(Socket socket, ReadOnlySpan<MailAddress> addresses, ReadOnlySpan<byte> mailData, ILogger logger)
        {
            _logger = logger;
            _mailData = mailData;
            TheSocket = socket;
            EndSuccess = false;
            CheckDelivery = new EmailSent[addresses.Length];
            for(int i = 0; i < addresses.Length; i++)
            {
                CheckDelivery[i] = new() { Email = addresses[i], Received = false };
            }
        }

        public bool EndSuccess { get; set; }

        public Span<EmailSent> CheckDelivery;

        public bool SendEmail(MailAddress messageFrom, string messageBody)
        {
            ReadOnlySpan<char> expectedWelcome = Read();
            if (expectedWelcome.StartsWith("220"))
            {
                TheSocket.Send($"EHLO {messageFrom.Host} \r\n".ToASCII());
                if (readRespondOk())
                {
                    TheSocket.Send("STARTLS".ToASCII());
                    ReadOnlySpan<char> accept220 = Read();
                    if (accept220.StartsAs("220"))
                    {
                       
                    }
                    TheSocket.Send($"MAIL FROM: <{messageFrom}>".ToASCII());
                    if (readRespondOk())
                    {
                        bool shouldSend = false;
                        for (int i = 0; i < CheckDelivery.Length; i++)
                        {
                            TheSocket.Send($"RCPT TO: <{CheckDelivery[i].Email}>".ToASCII());
                            CheckDelivery[i].Received = readRespondOk();
                            if (shouldSend is false)
                            {
                                if (CheckDelivery[i].Received)
                                { 
                                    shouldSend = true;
                                }
                            }
                        }
                        if (shouldSend)
                        {
                            TheSocket.Send("DATA".ToASCII());
                            ReadOnlySpan<char> expectedStartRead = Read();
                            if (expectedStartRead.StartsWith("354"))
                            {
                                TheSocket.Send(_mailData);
                                EndSuccess = readRespondOk();
                                TheSocket.Send("QUIT".ToASCII());
                                TheSocket.Disconnect(false);
                                return true;
                            }
                        }
                    }
                }
            }

            TheSocket.Send("QUIT"u8);
            TheSocket.Disconnect(false);
            return false;
        }

        internal bool readRespondOk()
        {
            ReadOnlySpan<char> expectedOK = Read();
            return (expectedOK[..3].Equals("250", StringComparison.InvariantCultureIgnoreCase));
        }

        private ReadOnlySpan<char> Read()
        {
            Span<byte> bytes = new byte[64]; //this can be stacalloc
            Span<char> chars = new char[64];
            int count = TheSocket.Receive(bytes);

            if (count < 1) { count = TheSocket.Receive(bytes); }

            Encoding.ASCII.GetChars(bytes, chars);
            _logger.LogInformation($"Read: {chars}");
            return chars;
        }
    }
}
