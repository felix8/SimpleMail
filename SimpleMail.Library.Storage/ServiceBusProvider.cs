using System;
using System.Threading.Tasks;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace SimpleMail.Library.Storage
{
    public sealed class ServiceBusProvider
    {
        #region Synchronization Primitives

        private static volatile ServiceBusProvider _instance;
        private static readonly object _lock = new Object();
        public static ServiceBusProvider Instance
        {
            get
            {
                if (_instance != null) return _instance;
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new ServiceBusProvider();
                }
                return _instance;
            }
        }

        #endregion // Synchronization Primitives
        private QueueClient ServiceBusClient { get; set; }
        private const string QueueName = "emailJobs";

        private ServiceBusProvider()
        {
            var namespaceManager = NamespaceManager.CreateFromConnectionString(Properties.Settings.Default.ServiceBusConnectionString);
            if (!namespaceManager.QueueExists(ServiceBusProvider.QueueName))
                namespaceManager.CreateQueue(ServiceBusProvider.QueueName);
            this.ServiceBusClient = QueueClient.CreateFromConnectionString(Properties.Settings.Default.ServiceBusConnectionString, ServiceBusProvider.QueueName);
        }

        public async Task<bool> Send(Tuple<string, string> emailReference, string attachmentUrls)
        {
            var message = new BrokeredMessage();
            message.Properties["PartitionKey"] = emailReference.Item1;
            message.Properties["RowKey"] = emailReference.Item2;
            if (attachmentUrls != null)
                message.Properties["AttachmentUrls"] = attachmentUrls;
            await this.ServiceBusClient.SendAsync(message);
            return true;
        }
    }
}