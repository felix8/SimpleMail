using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Net.Mime;
using System.Collections.Generic;
using SimpleMail.Library.Storage;

namespace SimpleMail.Web.Tests
{
    [TestClass]
    public class EmailTests
    {
        [TestMethod]
        public void AmazonTest()
        {
            var properties = new Dictionary<string, string>();
            properties["PartitionKey"] = Properties.Settings.Default.TestUser1;
            properties["RowKey"] = "08b7067b-73ad-4a37-9777-276122110dff";

            // read information from the ServiceBus message
            var senderEmailAddress = (string)properties["PartitionKey"];
            var emailUniqueId = (string)properties["RowKey"];
            string attachmentUrls = null;
            if (properties.ContainsKey("AttachmentUrls"))
                attachmentUrls = (string)properties["AttachmentUrls"];

            // read the email metadata from table storage
            var emailMetadata = TableStorageProvider.Instance.Read(senderEmailAddress, emailUniqueId);

            // if already sent, ignore
            if ((bool)emailMetadata["Sent"] != true)
            {
                // compose the email
                var message = new MailMessage();

                // to:
                var toRecipients = (string)emailMetadata["ToRecipientEmailIds"];
                message.To.Add(toRecipients);

                // from:
                message.From = new MailAddress(Properties.Settings.Default.TestUser2);

                // body:
                message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString((string)emailMetadata["Message"], null, MediaTypeNames.Text.Plain));

                // subject:
                if (emailMetadata.ContainsKey("Subject"))
                    message.Subject = (string)emailMetadata["Subject"];

                if (emailMetadata.ContainsKey("CcRecipientEmailIds"))
                {
                    var ccRecipients = (string)emailMetadata["CcRecipientEmailIds"];
                    message.CC.Add(ccRecipients);
                }

                if (emailMetadata.ContainsKey("BccRecipientEmailIds"))
                {
                    var bccRecipients = (string)emailMetadata["BccRecipientEmailIds"];
                    message.Bcc.Add(bccRecipients);
                }

                //// add attachments if any
                //if (attachmentUrls != null)
                //{
                //    foreach (var attachmentUrl in attachmentUrls.Split(new char[] { ',' }))
                //    {
                //        // read from blob
                //        var localStorage = RoleEnvironment.GetLocalResource("CacheStorage");
                //        BlobStorageProvider.Instance.Read(attachmentUrl, localStorage.RootPath);

                //        message.Attachments.Add(new Attachment(localStorage.RootPath + Path.GetFileName(attachmentUrl)));
                //    }
                //}

                // configure credentials and send using smtp client
                EmailProvider.Send(EmailServiceProviders.Amazon, message, true);
            }
        }
    }
}