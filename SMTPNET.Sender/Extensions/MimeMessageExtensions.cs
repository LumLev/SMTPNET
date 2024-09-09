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
    public static class MimeMessageExtensions
    {
        public static byte[] GetMessageDataDkimSigned(this MimeMessage message, Encoding encoder, string selector = "dkim1")
        {
            string domain = message.Sender.Domain;
         
            message.Headers.Add(HeaderId.MessageId, $"MAILOUT.{DateTime.Now.ToString("yyyyMMddHHmmssFFFFFFF")}@{domain}");
            message.Headers.Add(HeaderId.MimeVersion, "1.0");
            HeaderId[] headersToSign =  new HeaderId[] { HeaderId.MessageId, HeaderId.MimeVersion, HeaderId.From, HeaderId.To, HeaderId.Subject, HeaderId.Date};
            
            //RSA loadedKey = RSA.Create(2048);
            DkimSigner signer = new DkimSigner("dkim_private.key", message.Sender.Domain, selector, DkimSignatureAlgorithm.RsaSha256) 
            {
                SignatureAlgorithm = DkimSignatureAlgorithm.RsaSha256,
                QueryMethod = "dns/txt",
                AgentOrUserIdentifier = "@" + domain,
                BodyCanonicalizationAlgorithm = DkimCanonicalizationAlgorithm.Relaxed,
                HeaderCanonicalizationAlgorithm = DkimCanonicalizationAlgorithm.Relaxed
            };

            if (encoder == Encoding.ASCII)
            {
                message.Prepare(EncodingConstraint.SevenBit);
            }
            using MemoryStream ss = new MemoryStream();
           
            signer.Sign(FormatOptions.Default,message, headersToSign);
            message.WriteTo(ss);
            ss.Write(encoder.GetBytes("\r\n.\r\n"));
            return ss.GetBuffer();
        }

    }
}