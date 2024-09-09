using System.Net.Mail;
using SMTPNET.Sender.Models;
using System.Text;
using Microsoft.Extensions.Logging;



MailMessage mm = new MailMessage("ok@domain.tld", "ping@tools.mxtoolbox.com", "sub", "good");
mm.BodyEncoding = Encoding.UTF8;
mm.HeadersEncoding = Encoding.UTF8;
mm.SubjectEncoding = Encoding.UTF8;
mm.BodyTransferEncoding = System.Net.Mime.TransferEncoding.EightBit;
//
await SendEmail(mm);


async Task SendEmail(MailMessage message)
{
    SMTPRequest mailRequest = new();
    var emailsSent = await mailRequest.SendMessageAsync(message);

    foreach (var emailSent in emailsSent)
    {
        Console.WriteLine($"{emailSent.Email} = {emailSent.Received}");
    }
}

