using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using SMTPNET.Extensions;
using SMTPNET.Listener;
using SMTPNET.Listener.Models;
using SMTPNET.MailDns;
using SMTPNET.Worker.Models.Base;

namespace SMTPNET.Worker.Models
{
    public ref struct SMTPResponse
    {

        private ReadOnlySpan<char> _pathfile;
        public ReadOnlySpan<char> DataResult;
        public ReadOnlySpan<char> Sender;

        private Socket TheSocket { get; set; }
        private StatusEnum status;

        public ReadOnlySpan<char> HostDnsName;

        public bool GracefulFinish;

        private readonly ILogger _logger;

        public SMTPResponse(Socket incomingSocket,  string hostDnsName, ILogger logger)
        {
            _logger = logger;
            HostDnsName = hostDnsName;
            _pathfile = Path.Combine(SMTPListener.EmailsPath, $"{DateTime.Now.ToString("yyyyMMdd.hhmmss.FFFFFFF")}.eml");
            // DataResult = "";
            TheSocket = incomingSocket;
            status = StatusEnum.Connected;
            WriteLine($"220 Welcome from SMTPNET Server"); // Welcomes incoming connection
        }

        internal int DisconnectCounter = 0;

        /// <summary>
        /// Start accepting mail
        /// </summary>
        public bool ReceiveEmail()
        {
            ReadOnlySpan<char> data;
            while (TheSocket.Connected)
            {
                if (DisconnectCounter > 2) { TheSocket.Disconnect(false); return false; }
                 data = Read(); // The Thing

                if (data.Length < 1) { _logger.LogWarning("Received 0"); DisconnectCounter++; break; }
                else
                {
                        switch (data[0..4])
                        {
                            case "QUIT":
                                WriteLine("221 Good bye");
                                TheSocket.Disconnect(false);
                                return false;
                            case "RSET":
                                WriteLine("250 OK");
                                status = StatusEnum.Identified;
                                break;
                            case "HELO":
                            case "EHLO":
                            if (status is StatusEnum.Connected)
                            {
                                if (SMTPEncryptedResponse.TlsEnabled)
                                {
                                    WriteLine($"250-Hello {HostDnsName} \r\n250 STARTTLS"); // Extensions here
                                }
                                else {  WriteLine($"250-Hello {HostDnsName}"); }
                               
                                status = StatusEnum.Identified;
                                break;
                            }
                            else
                            { DisconnectCounter++; WriteCommand(SmtpResponseCode.BadSequenceOfCommands); break; }
                            case "STAR":
                                WriteLine("220 Start TLS");
                            try
                                {
                                using (SMTPEncryptedResponse SMER = new(TheSocket, HostDnsName.ToString(), _logger))
                                    {
                                        if (SMER.Connected) { return SMER.ReceiveEmail(); }
                                        else { WriteCommand(SmtpResponseCode.AuthenticationFailed); }
                                    }
                                } 
                            catch(Exception ex) { _logger.LogError(ex.Message); }
                                  break;
                            case "MAIL":
                                if (status is StatusEnum.Identified)
                                {
                                    status = StatusEnum.Mail;

                                Sender = data.GetFirstTag();
                                _logger.LogInformation($"Receiving email from: {Sender}");
                                MXRecord? mx = new MailDns.MailDns().GetFirstMX(Sender[(Sender.IndexOf('@') + 1)..].ToString());
                                if (mx is not null && mx.mailServer is not null)
                                {
                                        ReadOnlySpan<char> domain = String.Join('.', mx.mailServer.Split(".")[^2..]);
                                        if (HostDnsName.EndsAs(domain))
                                    { WriteLine("250 OK"); }
                                    else
                                    {
                                        WriteCommand(SmtpResponseCode.DnsError);
                                        WriteLine("QUIT");
                                        TheSocket.Disconnect(false);
                                    }
                                }
                                }
                                else { DisconnectCounter++; WriteCommand(SmtpResponseCode.BadSequenceOfCommands);  }
                                break;
                            case "RCPT":
                                if (status is StatusEnum.Recipient || status is StatusEnum.Mail)
                                {
                                    status = StatusEnum.Recipient;
                                    WriteLine("250 OK");
                                } 
                                else { DisconnectCounter++; WriteCommand(SmtpResponseCode.BadSequenceOfCommands); }
                                break;
                            case "DATA":
                                if (status is not StatusEnum.Recipient)
                                { DisconnectCounter++; WriteCommand(SmtpResponseCode.BadSequenceOfCommands); }
                                else
                                {
                                    status = StatusEnum.Data;
                                    WriteLine("354 Start mail input; end with <CRLF>.<CRLF>");
                                    GracefulFinish = SaveData();
                                    WriteLine("250 OK");
                                    WriteLine("QUIT");
                                    TheSocket.Disconnect(false);
                                    return true;
                                }
                                break;
                        default:
                                
                                WriteCommand(SmtpResponseCode.CommandNotImplemented);
                               _logger.LogWarning($"Syntax error on: {data}");
                            _logger.LogError("data");
                                DisconnectCounter++;
                                break;
                        
                    }
                }
            }
            return true;
        }

        private ReadOnlySpan<char> Read()
        {
            Span<byte> bytes = new byte[64]; //this can be stacalloc
            Span<char> chars = new char[64];
            int count = TheSocket.Receive(bytes);

            if (count < 1) {  count = TheSocket.Receive(bytes); }
           
            Encoding.ASCII.GetChars(bytes, chars);
             _logger.LogInformation($"Read: {chars}");
            return chars;
        }


        private bool SaveData()
        {
            Span<byte> bytes = stackalloc byte[2048];
            using (Stream fs = File.OpenWrite(_pathfile.ToString()))
            {
            reread:              
                int count = TheSocket.Receive(bytes);
                if (count == 0) { return false; }
                else
                {
                    ReadOnlySpan<char> theread = Encoding.UTF8.GetString(bytes[0..count]);
                   
                    fs.Write(bytes); 

                    if (theread.EndsAs("\r\n.\r\n")) 
                    {
                        _logger.LogInformation("Received Email = Confirmed!");

                        return true;
                    }
                    else
                    { 
                        goto reread;
                    }
                } 
            }
        }

        private void WriteCommand(SmtpResponseCode smtpResponseCode)
        {
            WriteLine($"{(int)smtpResponseCode} ${smtpResponseCode}");
        }

        private void WriteLine(ReadOnlySpan<char> data)
        {
            TheSocket.Send($"{data}\r\n".ToASCII());
             _logger.LogTrace($"Wrote: {data}");
        }

  
    }
}
