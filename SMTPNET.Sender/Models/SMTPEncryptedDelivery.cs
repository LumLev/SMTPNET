using Microsoft.Extensions.Logging;
using SMTPNET.Extensions;
using SMTPNET.Sender.Models.Base;
using System.IO;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SMTPNET.Sender.Models
{
    public class SMTPEncryptedDelivery : IDisposable
    {
        private readonly ILogger _logger;
        private readonly NetworkStream _networkStream;
        private readonly string _mailServerAddress;

        public SMTPEncryptedDelivery(Socket socket, string mailServerAddress, ReadOnlySpan<MailAddress> addresses, ILogger logger)
        {
            _mailServerAddress = mailServerAddress;
            _logger = logger;
            _networkStream = new NetworkStream(socket);
            EndSuccess = false;
            CheckDelivery = new EmailSent[addresses.Length];
            for (int i = 0; i < addresses.Length; i++)
            {
                CheckDelivery[i] = new() { Email = addresses[i], Received = false };
            }
        }


        public static SslClientAuthenticationOptions ClientTlsOpts(string host)
        {
            return new SslClientAuthenticationOptions()
            {
                TargetHost = host,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                AllowTlsResume = true,
                AllowRenegotiation = true,
                RemoteCertificateValidationCallback = (a, b, c, d) => { return true; }
            };
        }


        public bool EndSuccess { get; set; }

        public EmailSent[] CheckDelivery { get; init; }

        public bool SendEmail(MailAddress messageFrom, ArraySegment<byte> mailData)
        {
            if (evaluateStart(_networkStream, "220"))
            {
                Write(_networkStream, $"EHLO mail.{messageFrom.Host} \r\n");

                bool hasTLS = ReadEhloForStarTLS(_networkStream);
                
                if (hasTLS)
                {
                    Write(_networkStream, "STARTTLS \r\n");
                    _logger.LogInformation("Requested TLS");
                    if (evaluateStart(_networkStream, "220"))
                    {
                        try
                        {
                            SslStream tlsStream = new(_networkStream, true);
                            {
                                tlsStream.AuthenticateAsClient(ClientTlsOpts(_mailServerAddress));
                                
                                if (tlsStream.IsAuthenticated)
                                {
                                    _logger.LogInformation("TLS Started!");
                                    return EncryptedConversation(tlsStream, messageFrom, mailData);
                                }
                            }
                        }
                        catch (Exception e) { _logger.LogError(e.Message); _logger.LogError(e.Message); }
                    }
                }
                Write(_networkStream, $"MAIL FROM: <{messageFrom}> \r\n");
                    if (evaluateStart(_networkStream, "250"))
                    {
                        bool shouldSend = false;
                        for (int i = 0; i < CheckDelivery.Length; i++)
                        {
                        Write(_networkStream, $"RCPT TO: <{CheckDelivery[i].Email.Address.ToUpperInvariant()}> \r\n");
                            CheckDelivery[i].Received = evaluateStart(_networkStream, "250");
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
                                Write(_networkStream, "DATA \r\n");
                           
                                _networkStream.Write(mailData);
                                EndSuccess = evaluateStart(_networkStream, "250");
                                Write(_networkStream, "QUIT \r\n");
                                _networkStream.Close();
                                _networkStream.Dispose();
                                return true;
                           
                        }
                    }
            }

            Write(_networkStream, "QUIT \r\n");
            _networkStream.Close();
            _networkStream.Dispose();
            return false;
        }


        private bool EncryptedConversation(SslStream stream, MailAddress addressFrom, ReadOnlySpan<byte> mailData)
        {
            Write(stream,$"EHLO {addressFrom.Host} \r\n");
            if (evaluateStart(stream, "250"))
            {
                stream.Flush();
                _logger.LogInformation("Sending Mail");
                Write(stream,$"MAIL FROM: <{addressFrom.Address.ToUpperInvariant()}> \r\n");
                ReadEhloForStarTLS(stream);
                    stream.Flush();
                    bool shouldSend = false;
                    for (int i = 0; i < CheckDelivery.Length; i++)
                    {
                        Write(stream,$"RCPT TO: <{CheckDelivery[i].Email.Address.ToUpperInvariant()}> \r\n");
                        CheckDelivery[i].Received = evaluateStart(stream, "250"); // These have to be falsed if the data command below fails.
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
            redoData:
                           Write(stream,"DATA \r\n");
                           ReadOnlySpan<char> code = First3(stream);
                            switch (code)
                            {
                                case "250":
                            goto redoData;
                                case "354":
                                    _logger.LogInformation($"Sent: {Encoding.ASCII.GetString(mailData)}");
                                    stream.Write(mailData);
                                    stream.Flush();
                                    EndSuccess = evaluateStart(stream, "250");
                                    Write(stream, "QUIT \r\n");
                                    stream.Close();
                                    stream.Dispose();
                                    return true;
                            default:
                            _logger.LogCritical($"Code Received: {code}");
                            break;
                            }
                    }
                    else
                    {
                        _logger.LogCritical("Should Send was False!!!");
                        EndSuccess = false;
                        Write(stream, "QUIT \r\n");
                        stream.Close();
                        stream.Dispose();
                        return false;
                    }
                
            }
            else
            {
                _logger.LogCritical("EncDelivery Failed on EHLO");
            }
            _logger.LogCritical("Ended without success");
            this.EndSuccess = false;
            Write(stream, "QUIT \r\n");
            stream.Close();
            stream.Dispose();
            return false;
        }


        //internal ReadOnlySpan<char> First3(Stream stream)
        //{
        //    Span<byte> bytes = stackalloc byte[64];
        //    int count = stream.Read(bytes);
        //    if (count > 3)
        //    {
        //        Span<char> chars = new char[3];
        //       // _logger.LogInformation($"On first3 Received: {Encoding.ASCII.GetString(bytes)}");
        //        Encoding.ASCII.GetChars(bytes[..3], chars);
        //        _logger.LogInformation($"First 3 Read: {chars}");
        //        return chars;
        //    }
        //    else { stream.Flush(); return []; }
        //}

        internal ReadOnlySpan<char> First3(Stream stream)
        {
            Span<byte> bytes = stackalloc byte[64];
            int count = stream.Read(bytes);
            if (count > 3)
            {
                string received = Encoding.ASCII.GetString(bytes);
                _logger.LogInformation($"First 3 Read: {received}");
                return received.AsSpan(..3);
            }
            else { stream.Flush(); return []; }
        }

        internal bool evaluateStart(Stream stream, ReadOnlySpan<char> start)
        {
            Span<byte> bytes = stackalloc byte[64];
            int count = stream.Read(bytes);
            if (count > start.Length)
            {
                Span<char> chars = stackalloc char[start.Length];
                Encoding.ASCII.GetChars(bytes[..start.Length], chars);
                _logger.LogInformation($"Eval Read: {chars}");
                stream.Flush();
                return chars.SequenceEqual(start);
            }
            else { stream.Flush(); return false; }
        }


        private bool ReadEhloForStarTLS(Stream stream)
        {
            Span<byte> bytes = stackalloc byte[128]; //this can be stacalloc
            bool hasStarTLS = false;  
            int count;
        reread:
            count= stream.Read(bytes);
            if (count < 1)
            {
                return hasStarTLS;
            }
            else
            {
                ReadOnlySpan<string> message = Encoding.ASCII.GetString(bytes).Split("\n", StringSplitOptions.None);
                foreach (string m in message)
                {
                    _logger.LogInformation("Read:" + m);
                    if (m.StartsAs("250") && m.Length > 3)
                    {
                        if (m.Contains("STARTTLS"))
                        {
                            hasStarTLS = true;
                        }
                        switch (m[3])
                        {
                            case ' ':
                                return hasStarTLS;
                            case '-':
                                // this is the case idk
                                break;
                            default:
                                break;
                        }
                    }
                }
               goto reread; 
            }
        }


        private ReadOnlySpan<char> Read(Stream stream, int bufferSize = 64)
        {
            Span<byte> bytes = stackalloc byte[bufferSize]; //this can be stacalloc
            Span<char> chars = new char[bufferSize];
            int count = stream.Read(bytes);

            if (count < 1) { count = stream.Read(bytes); }

            Encoding.ASCII.GetChars(bytes, chars);
            _logger.LogInformation($"Read: {chars}");
            return chars;
        }

        private void Write(Stream stream, string message)
        {
            _logger.LogInformation($"Sent: {message}");
            stream.Write(Encoding.ASCII.GetBytes(message));
        }

        public void Dispose()
        {
            _networkStream?.Dispose();
        }
    }
}
