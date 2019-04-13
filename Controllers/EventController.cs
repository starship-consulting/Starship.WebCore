using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Security;
using Starship.Web.QueryModels;
using Starship.WebCore.Extensions;
using Starship.WebCore.Providers.Authentication;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class EventController : ApiController {

        public EventController(UserRepository users, AzureDocumentDbProvider data) {
            Users = users;
            Data = data;
        }

        [HttpGet, Route("api/events")]
        public IActionResult GetEvents([FromQuery] EventQueryParameters parameters) {

            if(parameters == null || string.IsNullOrEmpty(parameters.Type) || string.IsNullOrEmpty(parameters.Name)) {
                return BadRequest();
            }
            
            var account = Users.GetAccount();

            var events = Data.DefaultCollection.Get<CosmosEvent>()
                .Where(each => each.Type == "event" && each.Source.Type == parameters.Type && each.Name == parameters.Name && each.Owner == account.Id);
            
            /*if(!account.IsAdmin()) {
                events = events.Where(each => each.Owner == account.Id);
            }*/
            
            if(!string.IsNullOrEmpty(parameters.Filter)) {
                //events = events.OData().Filter(parameters.Filter);
            }
            
            return events.Select(each => new {
                parameters = each.Parameters,
                source = each.Source.Id
            })
            .ToArray()
            .ToJsonResult(Data.Settings.SerializerSettings);
        }
        
        [HttpGet, Route("api/events/{id}")]
        public IActionResult GetEvents([FromRoute] string id, [FromQuery] EventQueryParameters parameters) {

            var account = Users.GetAccount();
            var entity = Data.DefaultCollection.Find<CosmosDocument>(id);

            if(entity == null || account.GetPermission(entity) == PermissionTypes.None) {
                return StatusCode(404);
            }

            var events = Data.DefaultCollection.Get<CosmosEvent>()
                .Where(each => each.Type == "event" && each.Source.Id == id)
                .Select(each => new {
                    id = each.Id,
                    creationDate = each.CreationDate,
                    name = each.Name,
                    parameters = each.Parameters,
                    owner = each.Owner
                })
                .OrderBy(each => each.creationDate)
                .ToArray();
            
            return events.ToJsonResult(Data.Settings.SerializerSettings);
        }

        [HttpDelete, Route("api/events/{id}/{eventName}")]
        public async Task<IActionResult> DeleteLastEventOfType([FromRoute] string id, [FromRoute] string eventName) {
            var account = Users.GetAccount();
            var entity = Data.DefaultCollection.Find<CosmosDocument>(id);

            if(entity == null || account.GetPermission(entity) == PermissionTypes.None) {
                return StatusCode(404);
            }

            var result = Data.DefaultCollection.Get<CosmosEvent>()
                .Where(each => each.Type == "event" && each.Name == eventName && each.Source.Id == id)
                .OrderByDescending(each => each.CreationDate)
                .Take(1)
                .ToList()
                .FirstOrDefault();

            if(result != null) {
                await Data.DefaultCollection.DeleteAsync(result.Id);
            }

            return Ok(new { success = true });
        }

        private readonly AzureDocumentDbProvider Data;

        private readonly UserRepository Users;
    }
}