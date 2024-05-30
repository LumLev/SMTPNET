using Microsoft.Extensions.Logging;
using SMTPNET.Sender;
using SMTPNET.Sender.Extensions;
using SMTPNET.Sender.Models;
using SMTPNET.Sender.Models.Base;
using System.Net.Mail;
using System.Text.Json;


using var loggerFactory = LoggerFactory.Create(builder =>{ builder.AddConsole();});
ILogger logger = loggerFactory.CreateLogger<Program>();

MailMessage mail = new("from@domain.tld", "to@domain.tld", "subOk", "ok");

if (mail.From is null) { return; }

ConsoleKeyInfo key;
reread:
Console.WriteLine("K = Keys and repeat, M = Sends Email, S = Save Email, E = Custom Email");
key = Console.ReadKey();
if (key.Key == ConsoleKey.K)
{
    Console.WriteLine(JsonSerializer.Serialize<DkimKeys?>(CurrentDkimKeys.GetCurrentDkimKeys(), 
        new JsonSerializerOptions() { WriteIndented = true }));
    goto reread;
}
else if(key.Key == ConsoleKey.M)
{
    await SendEmail(mail);
  
}
else if(key.Key == ConsoleKey.E)
{
    Console.Write("To:");
    string to = Console.ReadLine()!;
    Console.Write("Subject:");
    string? subject = Console.ReadLine();
    Console.Write("Body:");
    string? body = Console.ReadLine();
    MailMessage mailE = new("from@domain.tld", to, subject, body);
    await SendEmail(mailE);
    goto reread;

}
else if(key.Key == ConsoleKey.S)
{
    using FileStream fs = File.Create("mail.eml");
    fs.Write(mail.GetMessageData());
}
Console.WriteLine("bye");


async Task SendEmail(MailMessage message)
{
    SMTPRequest mailRequest = new(logger: logger);
    
    var emailsSent = await mailRequest.SendMessageAsync(message, true);
    foreach (var emailSent in emailsSent)
    {
        Console.WriteLine($"{emailSent.Email} = {emailSent.Received}");
    }
}