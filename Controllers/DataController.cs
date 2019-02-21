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
using Starship.Web.Security;
using Starship.WebCore.Extensions;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class DataController : ApiController {
        
        public DataController(AzureDocumentDbProvider provider) {
            Provider = provider;
        }
        
        [HttpGet, Route("api/data")]
        public IActionResult Get() {
            var user = this.GetUser();
            var results = Provider.DefaultCollection.Get<CosmosDocument>().Where(each => each.Owner == user.Id).Select(each => each.Type).Distinct().ToList();
            return Ok(results);
        }
        
        [HttpGet, Route("api/data/{type}")]
        public IActionResult Get([FromRoute] string type, [FromQuery] DataQueryParameters parameters) {
            var top = 0;

            var user = this.GetUser();

            var query = GetData(user, type, parameters);
            var results = query.ToArray();
            
            return BuildJsonResult(results);
        }

        [HttpGet, Route("api/data/{type}/{id}")]
        public IActionResult Find([FromRoute] string type, [FromRoute] string id) {

            var user = this.GetUser();
            var entity = Provider.DefaultCollection.Find<CosmosDocument>(id);

            if(entity == null || !HasPermission(user, entity)) {
                return StatusCode(404);
            }
            
            return BuildJsonResult(entity);
        }

        [HttpGet, Route("api/data/{type}/{id}/events")]
        public IActionResult GetEvents([FromRoute] string type, [FromRoute] string id) {

            var user = this.GetUser();
            var entity = Provider.DefaultCollection.Find<CosmosDocument>(id);

            if(entity == null || !HasPermission(user, entity)) {
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
            
            return BuildJsonResult(events);
        }
        
        [HttpDelete, Route("api/data/{type}/{id}")]
        public async Task<IActionResult> Delete([FromRoute] string type, [FromRoute] string id) {

            var user = this.GetUser();

            var entity = Provider.DefaultCollection.Find<CosmosDocument>(id);

            if(entity == null || !HasPermission(user, entity)) {
                return StatusCode(404);
            }

            await Provider.DefaultCollection.DeleteAsync(id);

            return Ok(new { id });
        }

        [HttpDelete, Route("api/data/{type}")]
        public async Task<IActionResult> DeleteAll([FromRoute] string type) {

            var user = this.GetUser();

            var items = GetData(user, type);

            foreach(var item in items) {
                await Provider.DefaultCollection.DeleteAsync(item.Id);
            }

            return Ok(true);
        }

        [HttpDelete, Route("api/data")]
        public async Task<IActionResult> Delete([FromBody] string[] ids) {

            var user = this.GetUser();

            foreach(var id in ids) {
                var hasPermission = HasPermission(user, id);

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

            var user = this.GetUser();
            var resources = new List<Resource>();

            foreach(var entity in entities) {
                var document = TryGetDocument(user, entity);

                if(document == null) {
                    return StatusCode(404);
                }

                var owner = document.GetPropertyValue<string>(Provider.Settings.OwnerIdPropertyName);

                if(owner.IsEmpty()) {
                    document.SetPropertyValue(Provider.Settings.OwnerIdPropertyName, user.Id);
                }
                
                resources.Add(document);
            }

            var result = await Provider.DefaultCollection.CallProcedure<Document>(Provider.Settings.SaveProcedureName, resources);
            return BuildJsonResult(result);
        }

        [HttpPost, Route("api/data/{type}")]
        public async Task<IActionResult> Save([FromRoute] string type, [FromBody] ExpandoObject entity) {
            
            var user = this.GetUser();
            var document = TryGetDocument(user, entity, type);

            if(document == null) {
                return StatusCode(404);
            }
            
            var owner = document.GetPropertyValue<string>(Provider.Settings.OwnerIdPropertyName);

            if(owner.IsEmpty()) {
                document.SetPropertyValue(Provider.Settings.OwnerIdPropertyName, user.Id);
            }

            var result = await Provider.DefaultCollection.SaveAsync(document);
            return BuildJsonResult(result);
        }
        
        private Document TryGetDocument(UserProfile user, ExpandoObject source, string defaultType = "") {

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

                        if((!HasPermission(user, existing) || type != existing.Type)) {
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

        private bool HasPermission(UserProfile user, string id) {
            var existing = Provider.DefaultCollection.Find<CosmosDocument>(id);

            if(existing != null) {
                return HasPermission(user, existing);
            }

            return true;
        }

        private IActionResult BuildJsonResult(Resource resource) {
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(resource));
            return new JsonResult(data, Provider.Settings.SerializerSettings);
        }

        private IActionResult BuildJsonResult(IEnumerable<object> documents) {
            var data = documents.Select(each => JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(each)));
            return new JsonResult(data, Provider.Settings.SerializerSettings);
        }
        
        private IEnumerable<Document> GetData(UserProfile user, string type, DataQueryParameters parameters = null) {
            
            if(parameters == null) {
                parameters = new DataQueryParameters();
            }
            
            var entityQuery = Provider.DefaultCollection.Get<CosmosDocument>();
            var claimsQuery = Provider.DefaultCollection.Get<ClaimEntity>();
            
            // Todo:  Cache claim in user identity or sproc could manage entire query
            var claims = claimsQuery.Where(each => each.Type == "claim" && each.Owner == user.Id && each.Status == 1).Select(each => each.Value);
            
            var query = entityQuery.Where(each => each.Type == type);

            if(UseSecurity) {
                query = query.Where(each => each.Owner == Provider.Settings.SystemOwnerName || each.Owner == user.Id || claims.Contains(each.Owner));
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
        
        private bool HasPermission(UserProfile user, CosmosDocument entity) {
            return entity.Id == user.Id || entity.Owner == user.Id || entity.Owner == Provider.Settings.SystemOwnerName;
        }

        public static bool UseSecurity = true;

        private readonly AzureDocumentDbProvider Provider;
    }
}