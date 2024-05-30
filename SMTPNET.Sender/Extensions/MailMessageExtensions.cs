using System.Collections.Immutable;
using System.Net.Mail;
using System.Text;
using SMTPNET.Sender.Models.Base;

namespace SMTPNET.Sender.Extensions
{
    public static class MailMessageExtensions
    {
        public static ArraySegment<byte> GetMessageData(this MailMessage message)
        {
            ReadOnlySpan<char> boundary = DateTime.Now.ToString("yyyyMMddHHmmss");
            StringBuilder sb = new StringBuilder();
            string fromHost = message.From?.Host ?? "localhost";
            string toHost = message.To[0]?.Host ?? "localhost";
            sb.Append($"Received: from {fromHost} by {toHost}; {DateTime.Now.ToString("R")}\r\n");

            sb.Append($"MIME-Version: 1.0\r\n");
            sb.Append($"Date: {DateTime.Now.ToString("R")}\r\n");
            string fromAddress = message.From?.Address.ToUpperInvariant() ?? "localhost";
            string toAddress = message.To[0]?.Address.ToUpperInvariant() ?? "localhost";
            sb.Append($"From: <{fromAddress}>\r\n");
            sb.Append($"To: <{toAddress}>\r\n");
            sb.Append($"Subject: {message.Subject}\r\n");

            if (message.Headers.AllKeys.Contains("Message-ID"))
            {
                sb.Append($"Message-ID: {message.Headers["Message-ID"]}\r\n");
            }

            sb.Append($"Content-Type: multipart/alternative; boundary=\"{boundary}\"\r\n");
            sb.Append($"\r\n--{boundary}\r\n");

            // Add plain text part (if available)
            if (!string.IsNullOrEmpty(message.AlternateViews.FirstOrDefault()?.ContentStream.ToString()))
            {
                sb.Append($"Content-Type: text/plain; charset=\"UTF-8\"\r\n");
                sb.Append($"Content-Transfer-Encoding: quoted-printable\r\n");
                sb.Append($"\r\n{message.AlternateViews.FirstOrDefault()?.ContentStream.ToString()}\r\n--{boundary}\r\n");
            }

            // Add HTML part
            sb.Append($"Content-Type: text/html; charset=\"UTF-8\"\r\n");
            sb.Append($"Content-Transfer-Encoding: quoted-printable\r\n");
            sb.Append($"\r\n{message.Body}\r\n--{boundary}--\r\n\r\n.\r\n");

            return new ArraySegment<byte>(Encoding.ASCII.GetBytes(sb.ToString()));
        }


        public static ArraySegment<byte> GetMessageDataDkimSigned(this MailMessage message)
        {
            ReadOnlySpan<char> boundary = DateTime.Now.ToString("yyyyMMddHHmmss");
            StringBuilder sb = new StringBuilder();
            string fromHost = message.From?.Host ?? "localhost";
            string toHost = message.To[0]?.Host ?? "localhost";
            sb.Append($"DKIM-Signature: {message.GenerateDkimHeader(fromHost)}\r\n");
            sb.Append($"Received: from {fromHost} by {toHost}; {DateTime.Now.ToString("R")}\r\n");

            sb.Append($"MIME-Version: 1.0\r\n");
            sb.Append($"Date: {DateTime.Now.ToString("R")}\r\n");
            string fromAddress = message.From?.Address.ToUpperInvariant() ?? "localhost";
            string toAddress = message.To[0]?.Address.ToUpperInvariant() ?? "localhost";
            sb.Append($"From: <{fromAddress}>\r\n");
            sb.Append($"To: <{toAddress}>\r\n");
            sb.Append($"Subject: {message.Subject}\r\n");

            if (message.Headers.AllKeys.Contains("Message-ID"))
            {
                sb.Append($"Message-ID: {message.Headers["Message-ID"]}\r\n");
            }

            sb.Append($"Content-Type: multipart/alternative; boundary=\"{boundary}\"\r\n");
            sb.Append($"\r\n--{boundary}\r\n");

            // Add plain text part (if available)
            if (!string.IsNullOrEmpty(message.AlternateViews.FirstOrDefault()?.ContentStream.ToString()))
            {
                sb.Append($"Content-Type: text/plain; charset=\"UTF-8\"\r\n");
                sb.Append($"Content-Transfer-Encoding: quoted-printable\r\n");
                sb.Append($"\r\n{message.AlternateViews.FirstOrDefault()?.ContentStream.ToString()}\r\n--{boundary}\r\n");
            }

            // Add HTML part
            sb.Append($"Content-Type: text/html; charset=\"UTF-8\"\r\n");
            sb.Append($"Content-Transfer-Encoding: quoted-printable\r\n");
            sb.Append($"\r\n{message.Body}\r\n--{boundary}--\r\n\r\n.\r\n");

            return new ArraySegment<byte>(Encoding.ASCII.GetBytes(sb.ToString()));
        }


        public static ReadOnlySpan<byte> GetMessageDataAsROSpan(this MailMessage message)
        {
            ReadOnlySpan<char> boundary = DateTime.Now.ToString("yyyyMMddhhmmss");
            StringBuilder sb = new StringBuilder();
            sb.Append($"Received: {message.From!.Host} to {message.To[0].Host}\r\n");
            sb.Append($"MIME-Version: 1.0\r\n");
            sb.Append($"Date: {DateTime.Now.ToLongDateString()}\r\n");
            sb.Append($"From: <{message.From.Address.ToUpperInvariant()}>\r\n");
            sb.Append($"To: <{message.To[0].Address.ToUpperInvariant()}>\r\n");
            sb.Append($"Subject: {message.Subject}\r\n");
            sb.Append($"Message-ID: {message.Headers["Message-ID"]}\r\n");
            sb.Append($"Content-Type: multipart/alternative; boundary=\"{boundary}\"\r\n");
            sb.Append($"\r\n--{boundary}\r\n");
            sb.Append($"Content-Type: text/html; charset=\"UTF-8\"\r\n");
            sb.Append($"\r\n{message.Body}\r\n--{boundary}\r\n\r\n.\r\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }


        public static DkimKeys? CheckDkimKeys(this MailMessage message)
        {
            return CurrentDkimKeys.GetCurrentDkimKeys();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="domainName"></param>
        /// <returns>Returns the combined hash resulted from the final encryption on the canonicalizationed smtp message</returns>
        public static string GenerateDkimHeader(this MailMessage message, string domainName)
        {
            message.BodyTransferEncoding = System.Net.Mime.TransferEncoding.QuotedPrintable;
            message.Headers.Add("Subject", $"<{message.Subject}>");
            message.Headers.Add("From", $"<{message.From!.Address}>");
            message.Headers.Add("To", message.To[0].Address);
            message.Headers.Add("MIME-Version", "1.0");
            message.Headers.Add("Message-ID", $"<MAILOUT.{DateTime.Now.ToString("yyyyMMddHHmmssFFFFFFF")}.{domainName.ToUpperInvariant()}>");

            Console.WriteLine(message.Headers["Message-ID"]);

            SmtpHeadersRolledForDKIN headers = CurrentDkimKeys.HashedHeaders(message);
            DKIMSignature dKIMSignature = new(domainName, headers.SignatureColonDelimited);

            // Step 1: Hash the canonicalized body
            dKIMSignature.BodyHash = CurrentDkimKeys.HashBody(message.Body);

            // Step 2: Concatenate headers and body, then hash
            string combinedHash = CurrentDkimKeys.HashHeadersAndBody(headers.CanonicalizationedSMTPHeaders, message.Body);
            
            // Step 3: Sign the combined hash value
            dKIMSignature.MessageDigitalSignature = CurrentDkimKeys.Sign(combinedHash);
            return dKIMSignature.ToString();
        }

    }
}


