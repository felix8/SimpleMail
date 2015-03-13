using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using SimpleMail.Library.Storage;
using System.Threading.Tasks;
using System.Net.Mail;
using System.IO;
using System.Net.Mime;

namespace SimpleMail.SendGrid.Worker
{
    public class WorkerRole : RoleEntryPoint
    {
        /// <summary>
        /// Queue from which to receive jobs
        /// </summary>
        private const string QueueName = "emailJobs";

        // QueueClient is thread-safe.
        QueueClient Client;
        ManualResetEvent CompletedEvent = new ManualResetEvent(false);

        public override void Run()
        {
            Trace.WriteLine("Starting processing of emails");

            // Initiates the message pump and callback is invoked for each message that is received,
            // calling close on the client will stop the pump.
            Client.OnMessage((receivedMessage) =>
            {
                try
                {
                    // malformed messages should be dropped.
                    if (receivedMessage.Properties == null)
                        receivedMessage.Complete();

                    // read information from the ServiceBus message
                    var senderEmailAddress = (string)receivedMessage.Properties["PartitionKey"];
                    var emailUniqueId = (string)receivedMessage.Properties["RowKey"];
                    string attachmentUrls = null;
                    if (receivedMessage.Properties.ContainsKey("AttachmentUrls"))
                        attachmentUrls = (string)receivedMessage.Properties["AttachmentUrls"];

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
                        message.From = new MailAddress(senderEmailAddress);

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

                        // add attachments if any
                        if (attachmentUrls != null)
                        {
                            foreach (var attachmentUrl in attachmentUrls.Split(new char[] { ',' }))
                            {
                                // read from blob and store in local cache ("CacheStorage")
                                var localStorage = RoleEnvironment.GetLocalResource("CacheStorage");
                                BlobStorageProvider.Instance.Read(attachmentUrl, localStorage.RootPath);

                                message.Attachments.Add(new Attachment(localStorage.RootPath + Path.GetFileName(attachmentUrl)));
                            }
                        }

                        // send mail
                        EmailProvider.Send(EmailServiceProviders.SendGrid, message, enableSsl: true);
                    }

                    // flag the message as processed at this point
                    receivedMessage.Complete();
                }
                catch (Exception ex)
                {
                    Trace.Write(ex);

                    // For any exception during message receive,
                    // immediately abandon. This returns the message back to the queue.
                    // In our case, this is a rare, transient network error since we are
                    // expecting simple jobIds (references to table storage)
                    receivedMessage.Abandon();
                }
            });

            // We will block here until OnStop() is called
            CompletedEvent.WaitOne();
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // Create the queue if it does not exist already
            var connectionString = Properties.Settings.Default.ServiceBusConnectionString;
            var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
            if (!namespaceManager.QueueExists(QueueName))
            {
                namespaceManager.CreateQueue(QueueName);
            }

            // Initialize the connection to Service Bus Queue
            Client = QueueClient.CreateFromConnectionString(connectionString, QueueName);
            return base.OnStart();
        }

        public override void OnStop()
        {
            // Close the connection to Service Bus Queue
            Client.Close();
            CompletedEvent.Set();
            base.OnStop();
        }
    }
}