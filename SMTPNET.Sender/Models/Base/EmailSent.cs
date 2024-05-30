using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace SMTPNET.Sender.Models.Base
{
    public struct EmailSent
    {
        public MailAddress Email { get; set; }
        public bool Received { get; set; }
    }
}
