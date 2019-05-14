using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Extensions;
using Starship.Web.QueryModels;
using Starship.WebCore.Extensions;
using Starship.WebCore.Providers.Authentication;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class EventController : ApiController {

        public EventController(AccountManager users, AzureDocumentDbProvider data) {
            Users = users;
            Data = data;
        }

        [HttpGet, Route("api/events")]
        public IActionResult GetEvents([FromQuery] EventQueryParameters parameters) {

            if(parameters == null || parameters.Type.IsEmpty() || parameters.Name.IsEmpty()) {
                return BadRequest();
            }
            
            var eventNames = parameters.Name.Split(',').ToArray();
            var account = Users.GetAccount();
            
            var events = Data.DefaultCollection.Get<CosmosEvent>()
                .Where(each => each.Type == "event" && each.Source.Type == parameters.Type && eventNames.Contains(each.Name));

            /*var claims = account.GetClaims();

            if(parameters.Partition.IsEmpty()) {
                events = events.Where(each => claims.Contains(each.Owner));
            }
            else {
                if(!account.HasClaim(parameters.Type, parameters.Partition)) {
                    return new List<CosmosEvent>().ToJsonResult(Data.Settings.SerializerSettings);
                }
                
                events = events.Where(each => each.Owner == parameters.Partition);
            }*/

            if(!parameters.Partition.IsEmpty()) {
                events = events.Where(each => each.Owner == parameters.Partition);
            }
            else {
                events = events.Where(each => each.Owner == account.Id);
            }

            if(!parameters.StartDate.IsEmpty()) {
                var startDate = DateTime.Parse(parameters.StartDate);
                events = events.Where(each => each.Parameters.Date >= startDate);
            }

            if(!parameters.EndDate.IsEmpty()) {
                var endDate = DateTime.Parse(parameters.EndDate);
                events = events.Where(each => each.Parameters.Date < endDate);
            }
            
            return events.Select(each => new {
                parameters = each.Parameters,
                name = each.Name,
                source = each.Source.Id
            })
            .ToArray()
            .ToJsonResult(Data.Settings.SerializerSettings);
        }
        
        [HttpGet, Route("api/events/{id}")]
        public IActionResult GetEvents([FromRoute] string id, [FromQuery] EventQueryParameters parameters) {

            var account = Users.GetAccount();
            var entity = Data.DefaultCollection.Find<CosmosDocument>(id);

            if(entity == null || !account.CanRead(entity, GetSharingParticipants(account))) {
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

            if(entity == null || !account.CanUpdate(entity, GetSharingParticipants(account))) {
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

        // Todo:  Move to AccountManager
        private List<string> GetSharingParticipants(Account account) {
            var participants = account.GetParticipants().Select(each => each.Id).ToList();
            var groups = account.GetGroups();
            var groupParticipants = Data.DefaultCollection.Get<CosmosDocument>().Where(each => each.Type == "group" && groups.Contains(each.Id)).SelectMany(each => each.Participants).Select(each => each.Id).ToList();

            participants.AddRange(groupParticipants);
            return participants.Distinct().ToList();
        }

        private readonly AzureDocumentDbProvider Data;

        private readonly AccountManager Users;
    }
}