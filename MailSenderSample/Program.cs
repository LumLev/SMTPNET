using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SMTPNET.Sender;
using SMTPNET.Sender.Models;
using SMTPNET.Sender.Models.Base;
using SMTPNET.Sender.Extensions;




MailMessage mm = new MailMessage("user@domain.tld", "ping@tools.mxtoolbox.com", "subject", "body");
//
await SendEmail(mm);


async Task SendEmail(MailMessage message)
{
    using var loggerFactory = LoggerFactory.Create(builder =>{ builder.AddConsole();});
    SMTPRequest mailRequest = new(logger: loggerFactory.CreateLogger<Program>());
    var emailsSent = await mailRequest.SendMessageAsync(message, true);
    foreach (var emailSent in emailsSent)
   {
        Console.WriteLine($"{emailSent.Email} = {emailSent.Received}");
    }
}

