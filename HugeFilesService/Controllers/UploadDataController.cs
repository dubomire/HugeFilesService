// https://jonlabelle.com/snippets/view/csharp/upload-files-in-aspnet-core

using System.IO;
using System.Net;
using System.Threading.Tasks;
using HugeFilesService.Helpers;
using HugeFilesService.Utils;
using HugeFilesService.Utils.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using FormOptions = Microsoft.AspNetCore.Http.Features.FormOptions;

namespace HugeFilesService.Controllers
{
    [Route("api/[controller]")]
    public class UploadDataController : Controller
    {
        private FormOptions defaultFormOptions = new FormOptions
        {
            MultipartBoundaryLengthLimit = int.MaxValue-1
        };

        private ILogger<UploadDataController> logger;

        private string targetFilePath = "storage/";
        private string[] permittedExtensions = { ".rar", ".7zip", ".txt" };
        private int fileSizeLimit = 100000;

        public UploadDataController([FromServices] ILogger<UploadDataController> logger)
        {
            this.logger = logger;
        }

        [HttpPost("[action]")]
        [DisableFormValueModelBinding]
        //[ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPhysical()
        {
            if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
            {
                ModelState.AddModelError("File",
                    $"The request couldn't be processed (Error 1).");
                // Log error

                return BadRequest(ModelState);
            }

            string boundary = MultipartRequestHelper.GetBoundary(
                MediaTypeHeaderValue.Parse(Request.ContentType),
                defaultFormOptions.MultipartBoundaryLengthLimit);
            var reader = new MultipartReader(boundary, HttpContext.Request.Body);
            MultipartSection section = await reader.ReadNextSectionAsync();

            /// https://github.com/aspnet/Entropy/blob/15ed884dbf4d7d7ffdb6fee905ddb0ecb831cbd9/samples/Content.Upload.Multipart/Startup.cs#L29-L32

            while (section != null)
            {
                var hasContentDispositionHeader =
                    ContentDispositionHeaderValue.TryParse(
                        section.ContentDisposition, out ContentDispositionHeaderValue contentDisposition);

                if (hasContentDispositionHeader)
                {
                    // This check assumes that there's a file
                    // present without form data. If form data
                    // is present, this method immediately fails
                    // and returns the model error.
                    if (!MultipartRequestHelper
                        .HasFileContentDisposition(contentDisposition))
                    {
                        ModelState.AddModelError("File",
                            $"The request couldn't be processed (Error 2).");
                        // Log error

                        return BadRequest(ModelState);
                    }
                    else
                    {
                        // Don't trust the file name sent by the client. To display
                        // the file name, HTML-encode the value.
                        var trustedFileNameForDisplay = WebUtility.HtmlEncode(
                                contentDisposition.FileName.Value);
                        var trustedFileNameForFileStorage = Path.GetRandomFileName();

                        // **WARNING!**
                        // In the following example, the file is saved without
                        // scanning the file's contents. In most production
                        // scenarios, an anti-virus/anti-malware scanner API
                        // is used on the file before making the file available
                        // for download or for use by other systems. 
                        // For more information, see the topic that accompanies 
                        // this sample.
                        var streamedFileContent = await FileHelpers.ProcessStreamedFile(
                            section, contentDisposition, ModelState,
                            permittedExtensions, fileSizeLimit);

                        if (!ModelState.IsValid)
                        {
                            return BadRequest(ModelState);
                        }

                        using (var targetStream = System.IO.File.Create(
                            Path.Combine(targetFilePath, trustedFileNameForDisplay)))
                        {
                            await targetStream.WriteAsync(streamedFileContent);

                            logger.LogInformation(
                                "Uploaded file '{TrustedFileNameForDisplay}' saved to " +
                                "'{TargetFilePath}' as {TrustedFileNameForFileStorage}",
                                trustedFileNameForDisplay, targetFilePath,
                                trustedFileNameForFileStorage);
                        }
                    }
                }

                // Drain any remaining section body that hasn't been consumed and
                // read the headers for the next section.
                section = await reader.ReadNextSectionAsync();
            }

            return Created(nameof(UploadDataController), null);
        }


    }
}
