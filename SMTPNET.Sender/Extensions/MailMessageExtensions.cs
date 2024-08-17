using System.Collections.Immutable;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using MimeKit;
using MimeKit.Cryptography;
using SMTPNET.Extensions;
using SMTPNET.Sender.Models.Base;

namespace SMTPNET.Sender.Extensions
{
    public static class MailMessageExtensions
    {
        public static byte[] GetMessageDataDkimSigned(this MailMessage netMailMessage)
        {
         
           MimeMessage message = MimeMessage.CreateFromMailMessage(netMailMessage);
           if (netMailMessage.HeadersEncoding is null) { netMailMessage.HeadersEncoding = Encoding.ASCII;}
            message.Headers.Add(HeaderId.MessageId, $"MAILOUT.{DateTime.Now.ToString("yyyyMMddHHmmssFFFFFFF")}@{netMailMessage.From?.Host}");
            message.Headers.Add(HeaderId.MimeVersion, "1.0");
            HeaderId[] headersToSign =  new HeaderId[] { HeaderId.MessageId, HeaderId.MimeVersion, HeaderId.From, HeaderId.To, HeaderId.Subject, HeaderId.Date};
            string domain = netMailMessage.From?.Host ?? "";
            string selector = "dkim1";
            //RSA loadedKey = RSA.Create(2048);
            DkimSigner signer = new DkimSigner("dkim_private.key", domain, selector, DkimSignatureAlgorithm.RsaSha256) 
            {
                SignatureAlgorithm = DkimSignatureAlgorithm.RsaSha256,
                QueryMethod = "dns/txt",
                AgentOrUserIdentifier = "@" + netMailMessage.From!.Host,
                BodyCanonicalizationAlgorithm = DkimCanonicalizationAlgorithm.Relaxed,
                HeaderCanonicalizationAlgorithm = DkimCanonicalizationAlgorithm.Relaxed
            };

            using MemoryStream ss = new MemoryStream();
            message.Prepare(EncodingConstraint.SevenBit);
            signer.Sign(FormatOptions.Default,message, headersToSign);
            message.WriteTo(ss);
            ss.Write(netMailMessage.HeadersEncoding.GetBytes("\r\n.\r\n"));
            return ss.GetBuffer();
        }


        public static ReadOnlySpan<byte> GetMessageDataAsROSpan(this MailMessage message)
        {
            ReadOnlySpan<char> boundary = DateTime.Now.ToString("yyyyMMddhhmmss");
            StringBuilder sb = new();
            sb.Append($"Message-ID: {message.Headers["Message-ID"]}\r\n");
            sb.Append($"Received: {message.From!.Host} to {message.To[0].Host}\r\n");
            sb.Append($"MIME-Version: 1.0\r\n");
            sb.Append($"Date: {DateTime.Now.ToLongDateString()}\r\n");
            sb.Append($"From: {message.From.Address.ToUpperInvariant()}\r\n");
            sb.Append($"To: {message.To[0].Address.ToUpperInvariant()}\r\n");
            sb.Append($"Subject: {message.Subject}\r\n");
            sb.Append($"Content-Type: multipart/alternative; boundary=\"{boundary}\"\r\n");
            sb.Append($"\r\n--{boundary}\r\n");
            sb.Append($"Content-Type: text/html; charset=\"UTF-8\"\r\n");
            sb.Append($"\r\n{message.Body}\r\n--{boundary}\r\n\r\n.\r\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }
     
    }
}


