using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using MimeTypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using Starship.Core.Extensions;
using Starship.Core.Storage;
using Starship.WebCore.Extensions;

namespace Starship.WebCore.Controllers {

    public class ThumbnailsController : ApiController {

        public ThumbnailsController(IsFileStorageProvider storageProvider, IHostingEnvironment hostingEnvironment) {
            StorageProvider = storageProvider;
            HostingEnvironment = hostingEnvironment;
        }
        
        [Authorize, HttpGet, Route("api/thumbnails/{*path}")]
        public async Task<IActionResult> Thumbnail([FromRoute] string path, [FromQuery] int width = 64, [FromQuery] int height = 64) {

            var prefix = StorageProvider.GetDefaultPartitionName() + "/";

            path = path.Substring(prefix.Length);

            var user = User.GetUserProfile();

            var file = await GetFile(user.Id, path);

            var extension = new FileInfo(file.Path).Extension;
            var contentType = MimeTypeMap.GetMimeType(extension);

            IImageEncoder encoder = new PngEncoder();

            if (extension.EndsWith("bmp")) {
                encoder = new BmpEncoder();
            }
            else if (extension.EndsWith("gif")) {
                encoder = new GifEncoder();
            }
            else if (extension.EndsWith("png")) {
            }
            else if (extension.EndsWith("jpg")) {
                encoder = new JpegEncoder();
            }
            else {
                var png = MimeTypeMap.GetMimeType("png");

                if (extension.EndsWith("mp4")) {
                    using (var stream = new MemoryStream(System.IO.File.ReadAllBytes(HostingEnvironment.ContentRootPath + "//images//icons//video-icon.png"))) {
                        return ResizedImage(stream, width, height, encoder, png);
                    }
                }

                if (extension.EndsWith("wav")) {
                    using (var stream = new MemoryStream(System.IO.File.ReadAllBytes(HostingEnvironment.ContentRootPath + "//images//icons//sound-icon.png"))) {
                        return ResizedImage(stream, width, height, encoder, png);
                    }
                }

                using (var stream = new MemoryStream(System.IO.File.ReadAllBytes(HostingEnvironment.ContentRootPath + "//images//icons//file-icon.png"))) {
                    return ResizedImage(stream, width, height, encoder, png);
                }
            }

            return ResizedImage(file.Stream, width, height, encoder, contentType);
        }

        private IActionResult ResizedImage(Stream stream, int width, int height, IImageEncoder encoder, string contentType) {

            using (var image = Image.Load(stream)) {
                
                image.Mutate(x => x.Resize(new ResizeOptions {
                    Mode = ResizeMode.Pad,
                    Position = AnchorPositionMode.Center,
                    Size = new Size(width, height)
                }));

                using (var memoryStream = new MemoryStream()) {
                    image.Save(memoryStream, encoder);
                    return File(memoryStream.GetBuffer(), contentType);
                }
            }
        }

        private async Task<FileReference> GetFile(string partitionId, string path) {
            if (path.Trim().IsEmpty()) {
                //path = partitionId;
            }
            else {
                //path = partitionId + "/" + path;
            }

            if (!string.IsNullOrEmpty(path)) {
                try {
                    var file = await GetFileAsync(path);

                    if (file == null) {
                        return null;
                    }

                    if (!file.IsFolder) {
                        return file;
                    }
                }
                catch {
                }
            }

            return null;
        }

        private async Task<FileReference> GetFileAsync(string path) {
            return await StorageProvider.GetFileAsync(string.Empty, path);
        }

        private readonly IsFileStorageProvider StorageProvider;

        private readonly IHostingEnvironment HostingEnvironment;
    }
}