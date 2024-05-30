using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;
using SMTPNET.Extensions;
using SMTPNET.MailDns;
using SMTPNET.Worker.Models.Base;

namespace SMTPNET.Listener.Models
{
    public class SMTPEncryptedResponse:IDisposable
    {

          private readonly SslStream _stream;
        private readonly NetworkStream _netstream;
        private readonly ILogger _logger;
        private readonly string _filepath;

        public string HostDnsName { get; set; }
        public bool GracefulFinish { get; set; }
        public string Sender { get; set; }
        public StatusEnum status { get; set; } = StatusEnum.Connected;
        public bool Connected { get; set; }

        public SMTPEncryptedResponse(Socket incomingSocket, string hostdns,ILogger logger)
        {
            _filepath = Path.Combine(SMTPListener.EmailsPath, $"{DateTime.Now.ToString("yyyyMMdd.hhmmss.FFFFFFF")}.encrypted.eml");
            _logger = logger;
            DisconnectCounter = 0;
            _netstream = new NetworkStream(incomingSocket);
            _stream = new SslStream(_netstream, false, CertCallback, null);
            try
            {
                _stream.AuthenticateAsServer(serveropts);
                Connected = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                Connected = false;
            }
            Sender = "";
            HostDnsName = hostdns;
        }

        #region TlsDefaults
        internal static bool CertCallback(object o, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors) => true;

        internal protected static X509Certificate2? ServerCertificate { get; set; }
        internal protected static SslServerAuthenticationOptions serveropts { get; internal set; } = new SslServerAuthenticationOptions()
        {
            AllowTlsResume = true,
            ServerCertificate = ServerCertificate,
            ClientCertificateRequired = false,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
            AllowRenegotiation = true,
        };

        public static bool TlsEnabled { get => (ServerCertificate is not null); }

        public static void SetServerTlsCertificate(X509Certificate2 serverCertificate) 
        {
            ServerCertificate = serverCertificate;
            serveropts.ServerCertificate = ServerCertificate;
            serveropts.ServerCertificateContext = SslStreamCertificateContext.Create(ServerCertificate, null);
        }
        #endregion


        public int DisconnectCounter { get; set; }
        /// <summary>
        /// Start accepting mail
        /// </summary>
        public bool ReceiveEmail()
        {
            status = StatusEnum.Connected;
            ReadOnlySpan<char> data;
            while (Connected)
            {
                if (DisconnectCounter > 2) { _stream.Close(); _stream.Dispose(); Connected = false; return false; }
                data = Read(); // The Thing

                if (data.Length < 4)
                {
                    _logger.LogWarning("Received less than 4 chars");
                    DisconnectCounter++;
                }
                else
                {
                    switch (data[0..4])
                    {
                        case "QUIT":
                            WriteLine("221 Good bye");
                            _stream.Close(); _stream.Dispose(); Connected = false;
                            return false;
                        case "RSET":
                            WriteLine("250 OK");
                            status = StatusEnum.Identified;
                            break;
                        case "HELO":
                        case "EHLO":
                            if (status is StatusEnum.Connected)
                            {
                                WriteLine($"250 Hello"); // Extensions here
                                status = StatusEnum.Identified;
                                break;
                            }
                            else
                            { DisconnectCounter++; WriteCommand(SmtpResponseCode.BadSequenceOfCommands); break; }

                        case "MAIL":
                            if (status is StatusEnum.Identified)
                            {
                                status = StatusEnum.Mail;

                                Sender = data.GetFirstTag().ToString();
                                _logger.LogInformation($"Receiving email from: {Sender}");
                                MXRecord? mx = new MailDns.MailDns().GetFirstMX(Sender[(Sender.IndexOf('@') + 1)..].ToString());
                                if (mx is not null && mx.mailServer is not null)
                                {
                                    if (HostDnsName.EndsWith(string.Join('.', mx.mailServer.Split(".")[^2..]),
                                        StringComparison.InvariantCultureIgnoreCase))
                                    { WriteLine("250 OK"); }
                                    else
                                    {
                                        WriteCommand(SmtpResponseCode.DnsError);
                                        WriteLine("QUIT");
                                        _stream.Close(); _stream.Dispose(); Connected = false;
                                        return false;
                                    }
                                }
                            }
                            else { DisconnectCounter++; WriteCommand(SmtpResponseCode.BadSequenceOfCommands); }
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
                                _stream.Close();
                                _stream.Dispose(); Connected = false;
                                return true;
                            }
                            break;
                        default:
                            WriteCommand(SmtpResponseCode.CommandNotImplemented);
                            DisconnectCounter++;
                            break;
                    }
                }
            }
            return true;
        }

        private ReadOnlySpan<char> Read()
        {
            Span<byte> bytes = stackalloc byte[64];
            Span<char> chars = new char[64];
            int count = _stream.Read(bytes);
            if (count == 0) { count = _stream.Read(bytes); }
            Encoding.ASCII.GetChars(bytes, chars);
            _logger.LogInformation($"Read: {chars}");
            return chars;
        }


        private bool SaveData()
        {
            Span<byte> bytes = stackalloc byte[2048];

            using (Stream fs = File.OpenWrite(_filepath))
            {
            reread:
                int count = _stream.Read(bytes);
                if (count == 0) { return false; }
                else
                {
                    ReadOnlySpan<char> theread = Encoding.UTF8.GetString(bytes[0..count]);
                    _logger.LogInformation("Writing email data..");
                    fs.Write(bytes);

                    if (theread.EndsAs("\r\n.\r\n"))
                    {
                        _logger.LogInformation("Received Encrypted Email = Confirmed!");
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
            _stream.Write($"{data}\r\n".ToASCII());
            _logger.LogInformation($"Wrote: {data}");
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _netstream?.Dispose();
        }
    }
}
