using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MimeTypes;
using Starship.Core.Storage;

namespace Starship.WebCore.Controllers {

    public class FilesController : ApiController {

        public FilesController(IsFileStorageProvider storageProvider) {
            StorageProvider = storageProvider;
        }

        [HttpPost, Route("api/files/{*path}")]
        public async Task<IActionResult> Save([FromRoute] string path) {
            
            if(Request.HasFormContentType && Request.Form.Files.Any()) {
                var file = Request.Form.Files.First();
                
                using (var stream = file.OpenReadStream()) {
                    path += "/" + file.FileName;
                    var result = await StorageProvider.UploadAsync(string.Empty, stream, path);
                    return Ok(result);
                }
            }
            
            StorageProvider.CreateDirectories(string.Empty, path);

            return Ok();
        }
        
        [HttpGet, Route("api/fileinfo/{*path}")]
        public async Task<object> FileInfo([FromRoute] string path) {
            
            var file = await GetFileAsync(path);

            if (file == null) {
                return NotFound();
            }

            return new {
                url = "/files/" + path,
                type = file.ContentType,
                size = file.Length,
                created = file.LastModified
            };
        }
        
        [HttpGet, HttpHead, Route("api/files/{*path}")]
        public async Task<IActionResult> Get([FromRoute] string path, [FromQuery] string alias, [FromQuery] bool attachment = false) {
            
            if(path == null) {
                path = string.Empty;
            }

            var take = 0;

            if (!string.IsNullOrEmpty(path)) {
                try {
                    var file = await GetFileAsync(path);

                    if (file == null) {
                        return NotFound();
                    }

                    if(!file.IsFolder) {

                        if (string.IsNullOrEmpty(alias)) {
                            alias = file.Path.Split('/').Last();
                        }
                    
                        var contentType = file.ContentType;//MimeTypeMap.GetMimeType(path)
                        var extension = "";

                        if (alias.Contains(".")) {
                            extension = alias.Split('.').Last();
                            contentType = MimeTypeMap.GetMimeType(extension);
                        }
                        else {
                            extension = MimeTypeMap.GetExtension(file.ContentType);
                        }

                        if(attachment) {
                            var contentDisposition = new ContentDispositionHeaderValue("attachment");
                            contentDisposition.SetHttpFileName(alias);
                            Response.Headers[HeaderNames.ContentDisposition] = contentDisposition.ToString();
                        }
                    
                        Response.Headers["content-length"] = file.Length.ToString();
                    
                        return File(file.Stream, contentType);
                    }
                }
                catch {
                    return NotFound();
                }
            }
            
            var files = await StorageProvider.ListFilesAsync(string.Empty, path);

            if (take > 0) {
                files = files.Take(take);
            }

            return Ok(files.OrderByDescending(each => each.LastModified).ToList());
        }

        [HttpDelete, Route("api/files/{*path}")]
        public async Task<IActionResult> Delete([FromRoute] string path) {
            
            var result = await StorageProvider.DeleteAsync(string.Empty, path);

            if(result) {
                return Ok();
            }

            return NotFound();
        }
        
        private async Task<FileReference> GetFileAsync(string path) {
            return await StorageProvider.GetFileAsync(string.Empty, path);
        }
        
        private readonly IsFileStorageProvider StorageProvider;
    }
}