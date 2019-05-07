using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Starship.Data.Converters;

namespace Starship.WebCore.Controllers {

    public class SpreadsheetController : ApiController {
        
        [HttpPost, Route("api/spreadsheet")]
        public IActionResult ReadSpreadsheet() {

            if (Request.HasFormContentType && Request.Form.Files.Any()) {
                var file = Request.Form.Files.First();
                
                using (var stream = file.OpenReadStream()) {

                    var results = new SpreadsheetConverter().Read(stream, file.ContentType);
                    return new JsonResult(results);
                }
            }

            return Ok();
        }
    }
}