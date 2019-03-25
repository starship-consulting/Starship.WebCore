using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Community.OData.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.SystemFunctions;
using Newtonsoft.Json;
using Starship.Azure.Data;
using Starship.Azure.Json;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Extensions;
using Starship.Web.QueryModels;
using Starship.WebCore.Extensions;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class DataController : ApiController {
        
        public DataController(AzureDocumentDbProvider provider) {
            Provider = provider;
        }
        
        /*[HttpGet, Route("api/log")]
        public async Task<IActionResult> Log() {
            return Ok(await Provider.GetLog(DateTime.UtcNow.Subtract(TimeSpan.FromDays(3))));
        }*/

        [HttpGet, Route("api/data")]
        public IActionResult Get() {
            var account = GetAccount();
            var results = Provider.DefaultCollection.Get<CosmosDocument>().Where(each => each.Owner == account.Id).Select(each => each.Type).Distinct().ToList();
            return Ok(results);
        }
        
        [HttpGet, Route("api/data/{type}")]
        public IActionResult Get([FromRoute] string type, [FromQuery] DataQueryParameters parameters) {
            var account = GetAccount();
            var query = GetData(account, type, parameters);
            return query.ToArray().ToJsonResult(Provider.Settings.SerializerSettings);
        }

        [HttpGet, Route("api/data/{type}/{id}")]
        public IActionResult Find([FromRoute] string type, [FromRoute] string id) {

            var account = GetAccount();
            var entity = Provider.DefaultCollection.Find<CosmosDocument>(id);

            if(entity == null || !HasPermission(account, entity)) {
                return StatusCode(404);
            }
            
            return entity.ToJsonResult(Provider.Settings.SerializerSettings);
        }

        [HttpGet, Route("api/data/{type}/{id}/events")]
        public IActionResult GetEvents([FromRoute] string type, [FromRoute] string id) {

            var account = GetAccount();
            var entity = Provider.DefaultCollection.Find<CosmosDocument>(id);

            if(entity == null || !HasPermission(account, entity)) {
                return StatusCode(404);
            }

            var events = Provider.DefaultCollection.Get<CosmosEvent>()
                .Where(each => each.Type == "event" && each.Source.Id == id && each.Source.Type == type)
                .Select(each => new {
                    id = each.Id,
                    creationDate = each.CreationDate,
                    name = each.Name,
                    parameters = each.Parameters,
                    owner = each.Owner
                })
                .OrderBy(each => each.creationDate)
                .ToArray();
            
            return events.ToJsonResult(Provider.Settings.SerializerSettings);
        }
        
        [HttpDelete, Route("api/data/{type}/{id}")]
        public async Task<IActionResult> Delete([FromRoute] string type, [FromRoute] string id) {

            var account = this.GetAccount(Provider);

            var entity = Provider.DefaultCollection.Find<CosmosDocument>(id);

            if(entity == null || !HasPermission(account, entity)) {
                return StatusCode(404);
            }

            await Provider.DefaultCollection.DeleteAsync(id);

            return Ok(new { id });
        }

        [HttpDelete, Route("api/data/{type}")]
        public async Task<IActionResult> DeleteAll([FromRoute] string type) {

            var account = GetAccount();

            var items = GetData(account, type);

            foreach(var item in items) {
                await Provider.DefaultCollection.DeleteAsync(item.Id);
            }

            return Ok(true);
        }

        [HttpDelete, Route("api/data")]
        public async Task<IActionResult> Delete([FromBody] string[] ids) {

            var account = GetAccount();

            foreach(var id in ids) {
                var hasPermission = HasPermission(account, id);

                if(!hasPermission) {
                    return StatusCode(404);
                }
            }

            foreach(var id in ids) {
                await Provider.DefaultCollection.DeleteAsync(id);
            }

            return Ok(true);
        }
        
        [HttpPost, Route("api/data")]
        public async Task<IActionResult> Save([FromBody] ExpandoObject[] entities) {

            var account = GetAccount();
            var resources = new List<Resource>();

            foreach(var entity in entities) {
                var document = TryGetDocument(account, entity);

                if(document == null) {
                    return StatusCode(404);
                }

                var owner = document.GetPropertyValue<string>(Provider.Settings.OwnerIdPropertyName);

                if(owner.IsEmpty()) {
                    document.SetPropertyValue(Provider.Settings.OwnerIdPropertyName, account.Id);
                }
                
                resources.Add(document);
            }

            var result = await Provider.DefaultCollection.CallProcedure<Document>(Provider.Settings.SaveProcedureName, resources);
            return result.ToJsonResult(Provider.Settings.SerializerSettings);
        }

        [HttpPost, Route("api/data/{type}")]
        public async Task<IActionResult> Save([FromRoute] string type, [FromBody] ExpandoObject entity) {
            
            var account = GetAccount();
            var document = TryGetDocument(account, entity, type);

            if(document == null) {
                return StatusCode(404);
            }
            
            var owner = document.GetPropertyValue<string>(Provider.Settings.OwnerIdPropertyName);

            if(owner.IsEmpty()) {
                document.SetPropertyValue(Provider.Settings.OwnerIdPropertyName, account.Id);
            }

            var result = await Provider.DefaultCollection.SaveAsync(document);
            return result.ToJsonResult(Provider.Settings.SerializerSettings);
        }
        
        private Document TryGetDocument(Account account, ExpandoObject source, string defaultType = "") {

            var settings = new JsonSerializerSettings {
                ContractResolver = new DocumentContractResolver()
            };
            
            var serialized = JsonConvert.SerializeObject(source, settings);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(serialized)))  {
                var entity = JsonSerializable.LoadFrom<Document>(stream);
                var type = entity.GetPropertyValue<string>(Provider.Settings.TypePropertyName);

                if(type.IsEmpty()) {
                    entity.SetPropertyValue(Provider.Settings.TypePropertyName, defaultType);
                    type = defaultType;
                }

                if(type == null) {
                    throw new Exception("Unset entity type.");
                }

                if(!entity.Id.IsEmpty()) {
                    var existing = Provider.DefaultCollection.Find<CosmosDocument>(entity.Id);

                    if(existing != null) {

                        if((!HasPermission(account, existing) || type != existing.Type)) {
                            return null;
                        }
                        
                        if(type != existing.Type) {
                            throw new Exception("Mismatched entity type: '" + type + "' compared to '" + existing.Type + "'");
                        }

                        entity.SetPropertyValue(Provider.Settings.OwnerIdPropertyName, existing.Owner);
                    }
                }

                return entity;
            }
        }

        private bool HasPermission(Account account, string id) {
            var existing = Provider.DefaultCollection.Find<CosmosDocument>(id);

            if(existing != null) {
                return HasPermission(account, existing);
            }

            return true;
        }
        
        private IEnumerable<Document> GetData(Account account, string type, DataQueryParameters parameters = null) {
            
            if(parameters == null) {
                parameters = new DataQueryParameters();
            }
            
            var entityQuery = Provider.DefaultCollection.Get<CosmosDocument>();
            
            var query = entityQuery.Where(each => each.Type == type);

            if(account.IsAdmin()) {

                // Todo:  Cache claim in user identity or sproc could manage entire query
                var claimsQuery = Provider.DefaultCollection.Get<ClaimEntity>();
                var claims = claimsQuery.Where(each => each.Type == "claim" && each.Owner == account.Id && each.Status == 1).Select(each => each.Value);

                if(UseSecurity) {
                    query = query.Where(each => each.Owner == Provider.Settings.SystemOwnerName || each.Owner == account.Id || claims.Contains(each.Owner));
                }
            }

            if(parameters.IncludeInvalidated == null) {
                query = query.Where(each => !each.ValidUntil.IsDefined() || each.ValidUntil == null || each.ValidUntil > DateTime.UtcNow);
            }
            else {
                query = query.Where(each => !each.ValidUntil.IsDefined() || each.ValidUntil == null || each.ValidUntil > parameters.IncludeInvalidated);
            }
            
            if(!string.IsNullOrEmpty(parameters.Filter)) {
                return query.OData().Filter(parameters.Filter).ToList();
            }

            return query.ToList();
        }

        private Account GetAccount() {
            return this.GetAccount(Provider);
        }
        
        private bool HasPermission(Account account, CosmosDocument entity) {
            
            if(account.IsAdmin()) {
                return true;
            }

            return entity.Id == account.Id || entity.Owner == account.Id || entity.Owner == Provider.Settings.SystemOwnerName;
        }

        public static bool UseSecurity = true;

        private readonly AzureDocumentDbProvider Provider;
    }
}