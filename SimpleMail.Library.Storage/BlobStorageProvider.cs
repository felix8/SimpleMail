using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Web;
using System.IO;

namespace SimpleMail.Library.Storage
{
    /// <summary>
    /// Singleton class for synchronized write-only (hence, the Writer suffix) access into Azure Blob Storage.
    /// </summary>
    /// <remarks>
    /// Azure Blob Client instance is thread-safe and we want to keep it in memory for sharing
    /// across threads as much as possible.
    /// </remarks>
    public sealed class BlobStorageProvider
    {
        #region Synchronization Primitives

        private static volatile BlobStorageProvider instance;
        private static readonly object _lock = new Object();
        public static BlobStorageProvider Instance
        {
            get
            {
                if (instance != null) return instance;
                lock (_lock)
                {
                    if (instance == null)
                        instance = new BlobStorageProvider();
                }
                return instance;
            }
        }

        #endregion // Synchronization Primitives

        private CloudBlobContainer BlobContainer { get; set; }
        private const string ContainerName = "attachments";

        private BlobStorageProvider()
        {
            // @todo: fix hardcoded connection strings
            var storageAccount = CloudStorageAccount.Parse(Properties.Settings.Default.AzureStorageConnectionString);
            var client = storageAccount.CreateCloudBlobClient();
            this.BlobContainer = client.GetContainerReference(BlobStorageProvider.ContainerName);
            this.BlobContainer.CreateIfNotExists();
        }

        /// <summary>
        /// Combines multiple files names into a single string
        /// </summary>
        /// <example>
        /// CollateAttachments(string[] {"abc", "ced", "def"}) = "abc,ced,def"
        /// </example>
        /// <remarks>
        /// This wouldn't work if the link has a ',' character.
        /// That is why it is a private method and only works with Guids
        /// </remarks>
        /// <param name="links">Blob names</param>
        /// <returns>Attachment URLs collated together using ';' character</returns>
        private string CollateAttachments(string[] links)
        {
            if ((links == null) || (links.Length < 1))
                return null;

            var sb = new StringBuilder();
            foreach (var link in links)
            {
                sb.Append(link);
                sb.Append(',');
            }
            return sb.ToString().TrimEnd(',');
        }

        /// <summary>
        /// Writes to Azure Blob Storage asynchronously
        /// </summary>
        /// <param name="attachments">Attachments provided with the email.</param>
        /// <returns></returns>
        public async Task<string> Write(IEnumerable<HttpPostedFileBase> attachments)
        {
            var links = new ConcurrentBag<string>();
            var tasks = new ConcurrentBag<Task>();

            // @todo: (issue: #3) check total file size

            foreach (var attachment in attachments)
            {
                var currentAttachment = attachment;
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    // seems like even when we pass no attachments,
                    // we still get an empty file.
                    if ((currentAttachment == null) || (currentAttachment.ContentLength < 1))
                        return;

                    var blockName = currentAttachment.FileName;
                    var blockBlob = this.BlobContainer.GetBlockBlobReference(blockName);

                    links.Add(blockName);

                    // Create blob with contents from the cached file;
                    // blob name collisions are a function of Guid.NewGuid()
                    using (var fileStream = currentAttachment.InputStream)
                    {
                        blockBlob.UploadFromStream(fileStream);
                    }
                }));
            }

            await Task.WhenAll(tasks.ToArray());
            return this.CollateAttachments(links.ToArray());
        }

        public void Read(string attachmentUrl, string directory)
        {
            var blockBlob = this.BlobContainer.GetBlockBlobReference(attachmentUrl);

            // Save blob contents to a file.
            using (var fileStream = System.IO.File.OpenWrite(directory + Path.GetFileName(attachmentUrl)))
            {
                blockBlob.DownloadToStream(fileStream);
            };
        }
    }
}