using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace SMTPNET.Sender.Models.Base
{
    public struct EmailSent
    {
        public EmailSent(MailAddress email) { Email = email; Received = false;}
        public EmailSent(string emailString) { Email = new(emailString); Received = false;}
        public MailAddress Email { get; set; }
        public bool Received { get; set; }
    }
}
