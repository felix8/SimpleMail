using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace SimpleMail.Library.Storage
{
    /// <summary>
    /// Singleton class for synchronized write-only (hence, the Writer suffix) access into Azure Table Storage.
    /// </summary>
    /// <remarks>
    /// Azure Table Client instance is thread-safe and we want to keep it in memory for sharing
    /// across threads as much as possible.
    /// </remarks>
    public sealed class TableStorageProvider
    {
        #region Synchronization Primitives

        private static volatile TableStorageProvider instance;
        private static readonly object _lock = new Object();
        public static TableStorageProvider Instance
        {
            get
            {
                if (instance != null) return instance;
                lock (_lock)
                {
                    if (instance == null)
                        instance = new TableStorageProvider();
                }
                return instance;
            }
        }

        #endregion // Synchronization Primitives

        private CloudTable Table { get; set; }
        private const string TableName = "emails";

        /// <summary>
        /// Initialize a shared instance of TableStorageWriter
        /// </summary>
        private TableStorageProvider()
        {
            // @todo: fix hardcoded connection strings
            var storageAccount = CloudStorageAccount.Parse(Properties.Settings.Default.AzureStorageConnectionString);
            var client = storageAccount.CreateCloudTableClient();
            this.Table = client.GetTableReference(TableStorageProvider.TableName);
            this.Table.CreateIfNotExists();
        }

        /// <summary>
        /// Create a DynamicTableEntity from the sender email id, email information and attachments.
        /// </summary>
        /// <param name="senderMailId">Sender's email address</param>
        /// <param name="email">Email information such as To recipients, CC recipients</param>
        /// <param name="attachmentUrls">List of attachments urls (pointing to blob storage)</param>
        /// <returns></returns>
        private static DynamicTableEntity Update(string senderEmailAddress, Dictionary<string, string> email, string attachmentUrls, bool isSent = false)
        {
            var entity = new DynamicTableEntity();
            var data = new Dictionary<string, EntityProperty>
            {
                {"SenderEmail", new EntityProperty(senderEmailAddress)},
                {"ToRecipientEmailIds", new EntityProperty(email["ToRecipientEmailIds"])},
                {"CcRecipientEmailIds", new EntityProperty(email["CcRecipientEmailIds"])},
                {"BccRecipientEmailIds", new EntityProperty(email["BccRecipientEmailIds"])},
                {"Subject", new EntityProperty(email["Subject"])},
                {"Message", new EntityProperty(email["Message"])},
                {"Sent", new EntityProperty(isSent)},
                {"AttachmentUrls", new EntityProperty(attachmentUrls)}
            };

            entity.Properties = data;
            entity.PartitionKey = senderEmailAddress;
            entity.RowKey = Guid.NewGuid().ToString();

            return entity;
        }

        /// <summary>
        /// Writes email information to Azure Table Storage
        /// </summary>
        /// <param name="sender">Sender of the email.</param>
        /// <param name="email">Email information</param>
        /// <param name="attachmentUrls">Links to attachments uploaded to Blob Storage</param>
        /// <returns>Tuple containing the Primary and Row keys for the table entry</returns>
        public async Task<Tuple<string, string>> Write(string sender, Dictionary<string, string> email, string attachmentUrls)
        {
            var entity = TableStorageProvider.Update(sender, email, attachmentUrls);
            var operation = TableOperation.Insert(entity);
            await this.Table.ExecuteAsync(operation);
            return new Tuple<string, string>(entity.PartitionKey, entity.RowKey);
        }

        /// <summary>
        /// Read email metadata from Azure Table Storage
        /// </summary>
        /// <param name="senderEmailAddress">Sender's email address</param>
        /// <param name="emailUniqueId">A GUID serving as unique id for this table row</param>
        /// <returns>Dictionary containing email metadata</returns>
        public Dictionary<string, object> Read(string senderEmailAddress, string emailUniqueId)
        {
            var operation = TableOperation.Retrieve<DynamicTableEntity>(senderEmailAddress, emailUniqueId);
            var result = this.Table.Execute(operation);
            if (result == null)
                return null;
            var entity = (DynamicTableEntity)result.Result;
            var entries = new Dictionary<string, object>();

            // must be there
            entries["ToRecipientEmailIds"] = entity.Properties["ToRecipientEmailIds"].StringValue;
            entries["Message"] = entity.Properties["Message"].StringValue;
            entries["Sent"] = entity.Properties["Sent"].BooleanValue;

            // check if available
            if (entity.Properties.ContainsKey("CcRecipientEmailIds"))
                entries["CcRecipientEmailIds"] = entity.Properties["CcRecipientEmailIds"].StringValue;

            if (entity.Properties.ContainsKey("BccRecipientEmailIds"))
                entries["BccRecipientEmailIds"] = entity.Properties["BccRecipientEmailIds"].StringValue;

            if (entity.Properties.ContainsKey("Subject"))
                entries["Subject"] = entity.Properties["Subject"].StringValue;

            // update entries as "read"
            return entries;
        }

        /// <summary>
        /// Marks a given email as sent.
        /// </summary>
        /// <param name="senderEmailAddress">Sender's email address</param>
        /// <param name="emailUniqueId">A GUID serving as unique id for this table row</param>
        /// <returns>Void</returns>
        public void MarkSent(string senderEmailAddress, string emailUniqueId)
        {
            var operation = TableOperation.Retrieve<DynamicTableEntity>(senderEmailAddress, emailUniqueId);
            var result = this.Table.Execute(operation);
            if (result == null)
                return;

            var entity = (DynamicTableEntity)result.Result;

            // mark as sent
            entity.Properties["Sent"] = true;

            // upload back to table storage
            var updateOperation = TableOperation.Replace(entity);
            Table.Execute(updateOperation);
        }
    }
}