using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;
using Starship.Core.Extensions;
using Starship.Core.Storage;

namespace Starship.WebCore.Azure {
    public class AzureFileStorageProvider : IsFileStorageProvider {

        public AzureFileStorageProvider(AzureFileStorageSettings settings) {
            Settings = settings;
            Client = CloudStorageAccount.Parse(settings.ConnectionString).CreateCloudFileClient();
        }

        public AzureFileStorageProvider(string connectionstring) {
            Client = CloudStorageAccount.Parse(connectionstring).CreateCloudFileClient();
        }

        static AzureFileStorageProvider() {
            Shares = new Dictionary<string, CloudFileShare>();
            Directories = new Dictionary<string, CloudFileDirectory>();
        }

        public async Task<FileReference> UploadAsync(string partition, Stream stream, string path) {

            if(string.IsNullOrEmpty(partition)) {
                partition = Settings.DefaultPartitionName;
            }

            var file = PrepareUpload(partition, path);
            await file.UploadFromStreamAsync(stream);
            return new FileReference(file.Uri.LocalPath);
        }

        public async Task<FileReference> UploadAsync(string partition, byte[] data, string path) {

            if(string.IsNullOrEmpty(partition)) {
                partition = Settings.DefaultPartitionName;
            }

            var file = PrepareUpload(partition, path);
            await file.UploadFromByteArrayAsync(data, 0, data.Length);
            return new FileReference(file.Uri.LocalPath);
        }

        private CloudFile PrepareUpload(string partition, string path) {

            if(string.IsNullOrEmpty(partition)) {
                partition = Settings.DefaultPartitionName;
            }

            if(path.StartsWith("/")) {
                path = path.Substring(1);
            }

            CreateDirectories(partition, GetFolders(path));
            return GetShare(partition).GetFileReference(path);
        }

        public async Task<IEnumerable<FileReference>> ListFilesAsync(string partition, string path) {

            if(string.IsNullOrEmpty(partition)) {
                partition = Settings.DefaultPartitionName;
            }

            var directory = GetItem(partition, path) as CloudFileDirectory;
            var files = directory.ListFilesAndDirectories().ToList();
            return files.Select(ToFileReference);
        }

        public void Rename(string partition, string path, string name) {

            if(string.IsNullOrEmpty(partition)) {
                partition = Settings.DefaultPartitionName;
            }

            var item = GetItem(partition, path);

            if(item is CloudFile file) {
                //file.StartCopy()
            }
            else if(item is CloudFileDirectory directory) {

            }
        }

        private string GetFolders(string path) {
            var segments = path.Split('/');
            var directories = segments.Take(segments.Length - 1).ToList();
            return string.Join('/', directories);
        }

        private FileReference ToFileReference(IListFileItem item) {

            if(item == null) {
                return null;
            }

            if(item is CloudFileDirectory) {
                return new FileReference(item.Uri.LocalPath, true);
            }

            var file = item as CloudFile;

            return new FileReference(item.Uri.LocalPath) {
                Length = file.Properties.Length,
                ContentType = file.Properties.ContentType,
                Stream = file.OpenRead()
            };
        }

        public async Task<bool> DeleteAsync(string partition, string path) {

            if(string.IsNullOrEmpty(partition)) {
                partition = Settings.DefaultPartitionName;
            }

            var item = GetItem(partition, path);
            return await DeleteAsync(item);
        }

        private async Task<bool> DeleteAsync(IListFileItem item) {

            if(item == null) {
                return false;
            }

            if(item is CloudFile) {
                await item.As<CloudFile>().DeleteAsync();
            }
            else {
                var directory = item.As<CloudFileDirectory>();

                foreach(var each in directory.ListFilesAndDirectories()) {
                    await DeleteAsync(each);
                }

                await directory.DeleteAsync();
            }

            return true;
        }
        
        public async Task<FileReference> GetFileAsync(string partition, string path) {
            var item = GetItem(partition, path);
            return ToFileReference(item);
        }

        private IListFileItem GetItem(string partition, string path) {

            if(string.IsNullOrEmpty(partition)) {
                partition = Settings.DefaultPartitionName;
            }

            var currentDirectory = GetShare(partition);

            if(!string.IsNullOrEmpty(path)) {
            
                var index = 0;
                var segments = path.Split('/');
            
                foreach(var each in segments) {
                    index += 1;

                    if(index == segments.Length) {
                        var files = currentDirectory.ListFilesAndDirectories().ToList();
                        var match = files.FirstOrDefault(file => file.Uri.LocalPath.EndsWith(path, StringComparison.InvariantCultureIgnoreCase));
                        return match;
                    }

                    currentDirectory = currentDirectory.GetDirectoryReference(each);
                }
            }

            return currentDirectory;
        }

        public void CreateDirectories(string partition, string path) {
            
            if(string.IsNullOrEmpty(partition)) {
                partition = Settings.DefaultPartitionName;
            }

            if(string.IsNullOrEmpty(path)) {
                return;
            }

            lock(Directories) {
                if(Directories.ContainsKey(path)) {
                    return;
                }

                var currentDirectory = GetShare(partition);

                foreach(var each in path.Split('/')) {
                    currentDirectory = currentDirectory.GetDirectoryReference(each);
                    currentDirectory.CreateIfNotExists();
                }
            }
        }

        private CloudFileDirectory GetShare(string partition) {

            if(string.IsNullOrEmpty(partition)) {
                partition = Settings.DefaultPartitionName;
            }

            lock(Shares) {
                if(!Shares.ContainsKey(partition)) {
                    Shares.Add(partition, Client.GetShareReference(partition));
                    Shares[partition].CreateIfNotExists();
                    Shares[partition].GetRootDirectoryReference().CreateIfNotExists();
                }

                return Shares[partition].GetRootDirectoryReference();
            }
        }

        /*public async Task<FileReference> UploadAsync(string partition, Stream stream, string path) {
            var file = new StoredFileReference(partition, path);
            var reference = await BlobProvider.UploadAsync(partition, stream, file.RowKey);
            return await SaveFileReference(file, reference);
        }

        public async Task<FileReference> UploadAsync(string partition, byte[] data, string path) {
            var file = new StoredFileReference(partition, path);
            var reference = await BlobProvider.UploadAsync(partition, data, file.RowKey);
            return await SaveFileReference(file, reference);
        }

        private async Task<FileReference> SaveFileReference(StoredFileReference file, FileReference reference) {
            file.WriteTo(reference);
            TableProvider.Add(file);
            await TableProvider.SaveAsync();
            return reference;
        }

        public async Task<IEnumerable<FileReference>> ListFilesAsync(string partition, string path) {
            var files = TableProvider.Get<StoredFileReference>().ToList();

            return await BlobProvider.ListFilesAsync(partition, path);
        }

        public async Task<bool> DeleteAsync(string partition, string path) {
            return await BlobProvider.DeleteAsync(partition, path);
        }

        public async Task<FileReference> GetFileAsync(string partition, string path) {
            return await BlobProvider.GetFileAsync(partition, path);
        }

        private AzureTableStorageProvider TableProvider { get; set; }

        private AzureBlobStorageProvider BlobProvider { get; set; }*/

        private static Dictionary<string, CloudFileShare> Shares { get; set; }

        private static Dictionary<string, CloudFileDirectory> Directories { get; set; }

        private CloudFileClient Client { get; set; }

        private AzureFileStorageSettings Settings { get; set; }
    }
}