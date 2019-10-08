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

        public EventController(AccountManager users, AzureCosmosDbProvider data) {
            Users = users;
            Data = data;
        }

        [HttpGet, Route("api/events")]
        public IActionResult GetEvents([FromQuery] EventQueryParameters parameters) {

            if(parameters == null || parameters.Type.IsEmpty()) {
                return BadRequest();
            }
            
            var account = Users.GetAccount();
            
            var events = Data.DefaultCollection.Get<CosmosEvent>().Where(each => each.Type == "event" && each.Source.Type == parameters.Type);

            if(!string.IsNullOrEmpty(parameters.Name)) {
                var eventNames = parameters.Name.Split(',').ToArray();
                events = events.Where(each => eventNames.Contains(each.Name));
            }

            events = events.IsValid();

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

                var participants = GetSharingParticipants(account);

                if(account.IsManager()) {
                    var sharedAccounts = GetAccounts().Where(each => each.Role != "coordinator" && participants.Contains(each.Id)).Select(each => each.Id).ToList();
                    events = events.Where(e => e.Owner == account.Id || sharedAccounts.Contains(e.Owner));
                }
                else if(account.IsCoordinator()) {
                    events = events.Where(e => e.Owner == account.Id || participants.Contains(e.Owner));
                }
                else {
                    events = events.Where(each => each.Owner == account.Id);
                }
            }

            if(!parameters.StartDate.IsEmpty()) {
                var startDate = DateTime.Parse(parameters.StartDate);
                events = events.Where(each => each.CreationDate >= startDate);
            }

            if(!parameters.EndDate.IsEmpty()) {
                var endDate = DateTime.Parse(parameters.EndDate);
                events = events.Where(each => each.CreationDate < endDate);
            }
            
            return events.Select(each => new {
                parameters = each.Parameters,
                name = each.Name,
                date = each.CreationDate,
                source = each.Source.Id
            })
            .ToArray()
            .ToJsonResult(Data.Settings.SerializerSettings);
        }

        /*[HttpPost, Route("api/events")]
        public async Task<IActionResult> Save([FromBody] ExpandoObject entity) {

            var account = GetAccount();
            var document = TryUpdateResource(account, entity, type);

            if(document == null || !account.CanUpdate(document, GetSharingParticipants(account))) {
                return StatusCode(404);
            }
        }*/
        
        [HttpGet, Route("api/events/{id}")]
        public IActionResult GetEvents([FromRoute] string id, [FromQuery] EventQueryParameters parameters) {

            var account = Users.GetAccount();
            var entity = Data.DefaultCollection.Find<CosmosDocument>(id);

            if(entity == null || !account.CanRead(entity, GetSharingParticipants(account))) {
                return StatusCode(404);
            }
            
            var events = Data.DefaultCollection.Get<CosmosEvent>()
                .Where(each => each.Type == "event" && each.Source.Id == id)
                .IsValid()
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
                .IsValid()
                .OrderByDescending(each => each.CreationDate)
                .Take(1)
                .ToList()
                .FirstOrDefault();

            if(result != null) {
                await Data.DefaultCollection.DeleteAsync(result.Id);
            }

            return Ok(new { success = true });
        }

        private IQueryable<Account> GetAccounts() {
            return Data.DefaultCollection.Get<Account>().Where(each => each.Type == "account").IsValid();
        }

        // Todo:  Move to AccountManager
        private List<string> GetSharingParticipants(Account account) {
            var participants = account.GetParticipants().Select(each => each.Id).ToList();
            var groups = account.GetGroups();

            var groupParticipants = Data.DefaultCollection.Get<CosmosDocument>()
                .Where(each => each.Type == "group" && groups.Contains(each.Id))
                .IsValid()
                .SelectMany(each => each.Participants)
                .Select(each => each.Id)
                .ToList();

            participants.AddRange(groupParticipants);
            return participants.Distinct().ToList();
        }
        
        private readonly AzureCosmosDbProvider Data;

        private readonly AccountManager Users;
    }
}