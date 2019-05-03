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
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Starship.Azure.Data;
using Starship.Azure.Json;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Extensions;
using Starship.Core.Security;
using Starship.WebCore.Extensions;
using Starship.WebCore.Interfaces;
using Starship.WebCore.Models;
using Starship.WebCore.Providers.Authentication;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class DataController : ApiController {
        
        public DataController(IServiceProvider serviceProvider) {
            Users = serviceProvider.GetRequiredService<UserRepository>();
            Data = serviceProvider.GetRequiredService<AzureDocumentDbProvider>();
            Interceptor = serviceProvider.GetService<IsDataInterceptor>();
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
            var entity = Data.DefaultCollection.Get<CosmosDocument>()
                .Where(each => each.Type == type && each.Id == id)
                .ToList()
                .FirstOrDefault();

            if(entity == null || !account.CanRead(entity)) {
                return StatusCode(404);
            }
            
            return entity.ToJsonResult(Data.Settings.SerializerSettings);
        }
        
        [HttpDelete, Route("api/data/{type}/{id}")]
        public async Task<IActionResult> Delete([FromRoute] string type, [FromRoute] string id) {
            var account = GetAccount();
            var documents = Data.DefaultCollection.Get<CosmosDocument>().Where(each => each.Type == type && each.Id == id).ToArray();
            return await Delete(account, documents);
        }

        [HttpDelete, Route("api/data/{type}")]
        public async Task<IActionResult> DeleteAll([FromRoute] string type) {
            var account = GetAccount();
            var documents = GetData(account, type).ToArray();
            return await Delete(account, documents);
        }

        [HttpDelete, Route("api/data")]
        public async Task<IActionResult> Delete([FromBody] string[] ids) {
            var account = GetAccount();
            var documents = Data.DefaultCollection.Get<CosmosDocument>().Where(each => ids.Contains(each.Id)).ToArray();
            return await Delete(account, documents);
        }

        private async Task<IActionResult> Delete(Account account, params CosmosDocument[] documents) {

            if(documents.Any(document => !account.CanDelete(document))) {
                return BadRequest();
            }

            foreach(var document in documents) {

                if(Interceptor != null) {
                    await Interceptor.Delete(document);
                }

                await Data.DefaultCollection.DeleteAsync(document.Id);
            }

            return Ok(true);
        }
        
        [HttpPost, Route("api/data")]
        public async Task<IActionResult> Save([FromBody] ExpandoObject[] entities) {

            var account = GetAccount();
            var resources = new List<Resource>();

            foreach(var entity in entities) {
                var document = TryGetResource(account, entity);

                if(document == null || !account.CanUpdate(document)) {
                    return StatusCode(404);
                }
                
                if(document.Owner.IsEmpty()) {
                    document.Owner = account.Id;
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

            if(document == null || !account.CanUpdate(document)) {
                return StatusCode(404);
            }
            
            if(document.Owner.IsEmpty()) {
                document.Owner = account.Owner;
            }

            if(Interceptor != null) {
                await Interceptor.Save(document);
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

                if(entity.Type.IsEmpty()) {
                    entity.Type = defaultType;
                }

                if(entity.Type == null) {
                    throw new Exception("Unset entity type.");
                }
                
                if(!entity.Id.IsEmpty()) {
                    var existing = Data.DefaultCollection.Find<CosmosDocument>(entity.Id);

                    if(existing != null) {
                        
                        if(entity.Type != existing.Type) {
                            return null;
                        }

                        if(entity.Type == "account") {
                            
                            if(!account.CanUpdate(existing)) {
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
                        else if(!account.CanUpdate(existing)) {
                            return null;
                        }

                        entity.Owner = existing.Owner;
                    }
                }

                return entity;
            }
        }
        
        private IEnumerable<CosmosDocument> GetData(Account account, string type, DataQueryParameters parameters = null) {
            
            if(parameters == null) {
                parameters = new DataQueryParameters();
            }

            if(parameters.Partition == "self") {
                parameters.Partition = account.Id;
            }
            
            if(type == "account") {

                var accounts = GetAccounts();

                // Non-admin users can see participating accounts and group member accounts
                if(!account.IsAdmin()) {
                    
                    var participants = account.Participants.Select(each => each.Id);
                        
                    accounts = accounts.Where(each => each.Id == account.Id || participants.Contains(each.Id) || each.Groups.Any(group => account.Groups.Contains(group)));
                }

                accounts = parameters.Apply(accounts);

                return accounts.ToList();
            }

            var query = Data.DefaultCollection.Get<CosmosDocument>().Where(each => each.Type == type);
            var claims = account.GetClaims().Select(each => each.Scope).ToList();

            query = query.Where(each => claims.Contains(each.Owner) || each.Participants.Any(participant => participant.Id == account.Id));

            query = parameters.Apply(query);
            
            if(!string.IsNullOrEmpty(parameters.Filter)) {
                return query.OData().Filter(parameters.Filter).Take(1000).ToList();
            }

            return query.Take(1000).ToList();
        }

        private IQueryable<Account> GetAccounts() {
            return Data.DefaultCollection.Get<Account>().Where(each => each.Type == "account");
        }
        
        private Account GetAccount() {
            return Users.GetAccount();
        }
        
        private readonly IsDataInterceptor Interceptor;

        private readonly AzureDocumentDbProvider Data;

        private readonly UserRepository Users;
    }
}