using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace SimpleMail.Library.Storage
{
    public sealed class EmailProvider
    {
        public static bool Send(EmailServiceProviders service, MailMessage message, bool enableSsl)
        {
            string host = null;
            string username = null;
            string password = null;

            switch(service)
            {
                case EmailServiceProviders.Amazon:
                    host = Properties.Settings.Default.AmazonHost;
                    username = Properties.Settings.Default.AmazonUsername;
                    password = Properties.Settings.Default.AmazonPassword;
                    break;
                case EmailServiceProviders.SendGrid:
                    host = Properties.Settings.Default.SendGridHost;
                    username = Properties.Settings.Default.SendGridUsername;
                    password = Properties.Settings.Default.SendGridPassword;
                    break;
                default:
                    break;
            }

            // configure credentials and send using smtp client
            var smtpClient = new SmtpClient(host, Convert.ToInt32(587));

            // use ssl
            smtpClient.EnableSsl = enableSsl;

            var credentials = new System.Net.NetworkCredential(username, password);
            smtpClient.Credentials = credentials;

            smtpClient.Send(message);
            
            return true;
        }
    }
}