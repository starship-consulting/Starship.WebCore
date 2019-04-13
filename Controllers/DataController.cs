using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Community.OData.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.SystemFunctions;
using Newtonsoft.Json;
using Starship.Azure.Data;
using Starship.Azure.Json;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Extensions;
using Starship.Core.Security;
using Starship.Data.Configuration;
using Starship.Web.QueryModels;
using Starship.WebCore.Extensions;
using Starship.WebCore.Providers.Authentication;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class DataController : ApiController {
        
        public DataController(UserRepository users, AzureDocumentDbProvider data) {
            Users = users;
            Data = data;
        }
        
        /*[HttpGet, Route("api/log")]
        public async Task<IActionResult> Log() {
            return Ok(await Provider.GetLog(DateTime.UtcNow.Subtract(TimeSpan.FromDays(3))));
        }*/

        /*[HttpGet, Route("api/test")]
        public async Task<IActionResult> Test() {

            var test = Data.DefaultCollection.Get<CosmosDocument>().Where(each => each.Owner == "google-oauth2|106287561953926890758").ToList().FirstOrDefault();
            test.Owner = "test";

            await Data.DefaultCollection.SaveAsync(test);

            return null;
        }*/

        [HttpGet, Route("api/cleardata")]
        public async Task ClearData() {

            var account = GetAccount();
            var documents = Data.DefaultCollection.Get<CosmosDocument>().Where(each => each.Owner == account.Id);
            var timestamp = Convert.ToInt32(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);

            foreach(var document in documents) {
                
                if(document.Type == "account") {
                    continue;
                }

                document.Owner += "-archived-" + timestamp;
                await Data.DefaultCollection.SaveAsync(document);
            }

            await HttpContext.SignOutAsync("Auth0", new AuthenticationProperties {
                RedirectUri = ""
            });

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        [HttpGet, Route("api/data")]
        public IActionResult Get() {
            var account = GetAccount();
            var results = Data.DefaultCollection.Get<CosmosDocument>().Where(each => each.Owner == account.Id).Select(each => each.Type).Distinct().ToList();
            return Ok(results);
        }
        
        [HttpGet, Route("api/data/{type}")]
        public IActionResult Get([FromRoute] string type, [FromQuery] DataQueryParameters parameters) {
            var account = GetAccount();
            var query = GetData(account, type, parameters);
            return query.ToArray().ToJsonResult(Data.Settings.SerializerSettings);
        }

        [HttpGet, Route("api/data/{type}/{id}")]
        public IActionResult Find([FromRoute] string type, [FromRoute] string id) {

            var account = GetAccount();
            var entity = Data.DefaultCollection.Find<CosmosDocument>(id);

            if(entity == null || account.GetPermission(entity) == PermissionTypes.None) {
                return StatusCode(404);
            }
            
            return entity.ToJsonResult(Data.Settings.SerializerSettings);
        }
        
        [HttpDelete, Route("api/data/{type}/{id}")]
        public async Task<IActionResult> Delete([FromRoute] string type, [FromRoute] string id) {
            
            var account = GetAccount();

            var entity = Data.DefaultCollection.Find<CosmosDocument>(id);

            if(entity == null || account.GetPermission(entity) <= PermissionTypes.Partial) {
                return StatusCode(404);
            }

            await Data.DefaultCollection.DeleteAsync(id);

            return Ok(new { id });
        }

        [HttpDelete, Route("api/data/{type}")]
        public async Task<IActionResult> DeleteAll([FromRoute] string type) {
            
            var account = GetAccount();

            var documents = GetData(account, type).ToList();

            foreach(var document in documents) {
                if(account.GetPermission(document) <= PermissionTypes.Partial) {
                    return BadRequest();
                }
            }

            foreach(var document in documents) {
                await Data.DefaultCollection.DeleteAsync(document.Id);
            }

            return Ok(true);
        }

        [HttpDelete, Route("api/data")]
        public async Task<IActionResult> Delete([FromBody] string[] ids) {
            
            var account = GetAccount();

            foreach(var id in ids) {
                if(account.GetPermission(Data.DefaultCollection.Find<CosmosDocument>(id)) <= PermissionTypes.Partial) {
                    return StatusCode(404);
                }
            }

            foreach(var id in ids) {
                await Data.DefaultCollection.DeleteAsync(id);
            }

            return Ok(true);
        }
        
        [HttpPost, Route("api/data")]
        public async Task<IActionResult> Save([FromBody] ExpandoObject[] entities) {

            var account = GetAccount();
            var resources = new List<Resource>();

            foreach(var entity in entities) {
                var document = TryGetResource(account, entity);

                if(document == null || account.GetPermission(document) <= PermissionTypes.Partial) {
                    return StatusCode(404);
                }

                var owner = document.GetPropertyValue<string>(Data.Settings.OwnerIdPropertyName);

                if(owner.IsEmpty()) {
                    document.SetPropertyValue(Data.Settings.OwnerIdPropertyName, account.Id);
                }
                
                resources.Add(document);
            }

            var result = await Data.DefaultCollection.CallProcedure<Document>(Data.Settings.SaveProcedureName, resources);
            return result.ToJsonResult(Data.Settings.SerializerSettings);
        }

        [HttpPost, Route("api/data/{type}")]
        public async Task<IActionResult> Save([FromRoute] string type, [FromBody] ExpandoObject entity) {
            
            var account = GetAccount();
            var document = TryGetResource(account, entity, type);

            if(document == null || account.GetPermission(document) == PermissionTypes.None) {
                return StatusCode(404);
            }
            
            var owner = document.GetPropertyValue<string>(Data.Settings.OwnerIdPropertyName);

            if(owner.IsEmpty()) {
                document.SetPropertyValue(Data.Settings.OwnerIdPropertyName, account.Id);
            }
            
            var result = await Data.DefaultCollection.SaveAsync(document);
            return result.ToJsonResult(Data.Settings.SerializerSettings);
        }
        
        private CosmosDocument TryGetResource(Account account, ExpandoObject source, string defaultType = "") {

            var settings = new JsonSerializerSettings {
                ContractResolver = new DocumentContractResolver()
            };
            
            var serialized = JsonConvert.SerializeObject(source, settings);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(serialized)))  {
                var entity = JsonSerializable.LoadFrom<CosmosDocument>(stream);
                var type = entity.GetPropertyValue<string>(Data.Settings.TypePropertyName);

                if(type.IsEmpty()) {
                    entity.SetPropertyValue(Data.Settings.TypePropertyName, defaultType);
                    type = defaultType;
                }

                if(type == null) {
                    throw new Exception("Unset entity type.");
                }
                
                if(!entity.Id.IsEmpty()) {
                    var existing = Data.DefaultCollection.Find<CosmosDocument>(entity.Id);

                    if(existing != null) {
                        
                        if(type != existing.Type) {
                            return null;
                        }

                        if(type == "account") {
                            
                            if(account.GetPermission(existing) == PermissionTypes.None) {
                                return null;
                            }

                            foreach(var property in source) {

                                var match = typeof(Account).GetProperties().FirstOrDefault(each => each.Name.ToLower() == property.Key.ToLower());

                                /*if(match != null && match.HasAttribute<SecureAttribute>()) {
                                    var propertyName = match.Name;
                                    var json = match.GetCustomAttribute<JsonPropertyAttribute>(true);

                                    if(json != null) {
                                        propertyName = json.PropertyName;
                                    }

                                    var value = entity.GetPropertyValue<object>(propertyName);
                                    entity.SetPropertyValue(propertyName, value);
                                }*/

                                if(match == null || !match.HasAttribute<SecureAttribute>()) {
                                    existing.SetPropertyValue(property.Key, property.Value);
                                }
                            }

                            return existing;
                        }
                        else if(account.GetPermission(existing) <= PermissionTypes.Partial) {
                            return null;
                        }

                        entity.SetPropertyValue(Data.Settings.OwnerIdPropertyName, existing.Owner);
                    }
                }

                return entity;
            }
        }
        
        private IEnumerable<CosmosDocument> GetData(Account account, string type, DataQueryParameters parameters = null) {
            
            if(parameters == null) {
                parameters = new DataQueryParameters();
            }
            
            var entityQuery = Data.DefaultCollection.Get<CosmosDocument>();
            
            var query = entityQuery.Where(each => each.Type == type);

            if(!string.IsNullOrEmpty(parameters.Partition)) {
                query = query.Where(each => each.Owner == parameters.Partition || each.Owner == GlobalDataSettings.SystemOwnerName);
            }

            if(!account.IsAdmin()) {

                // Todo:  Cache claim in user identity or sproc could manage entire query
                //var claimsQuery = Data.DefaultCollection.Get<ClaimEntity>();
                //var claims = claimsQuery.Where(each => each.Type == "claim" && each.Owner == account.Id && each.Status == 1).Select(each => each.Value);

                if(UseSecurity) {
                    query = query.Where(each => each.Owner == GlobalDataSettings.SystemOwnerName || each.Owner == account.Id);
                    //query = query.Where(each => each.Owner == GlobalDataSettings.SystemOwnerName || each.Owner == account.Id || claims.Contains(each.Owner));
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
            return Users.GetAccount();
        }

        public static bool UseSecurity = true;

        private readonly AzureDocumentDbProvider Data;

        private readonly UserRepository Users;
    }
}