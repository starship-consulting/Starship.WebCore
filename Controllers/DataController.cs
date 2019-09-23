using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Starship.Azure.Data;
using Starship.Azure.Json;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Extensions;
using Starship.Core.Security;
using Starship.Data.Configuration;
using Starship.Data.Entities;
using Starship.Data.Interfaces;
using Starship.WebCore.ActionFilters;
using Starship.WebCore.Extensions;
using Starship.WebCore.Models;
using Starship.WebCore.Providers.Authentication;

namespace Starship.WebCore.Controllers {
    
    [ApiAuthorizationFilter]
    public class DataController : ApiController {
        
        public DataController(IServiceProvider serviceProvider) {
            Users = serviceProvider.GetRequiredService<AccountManager>();
            Data = serviceProvider.GetRequiredService<AzureCosmosDbProvider>();
            Interceptor = serviceProvider.GetService<IsDataInterceptor>();
        }

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
        
        [HttpGet, Route("api/data/{types}")]
        public async Task<object> Get([FromRoute] string types, [FromQuery] DataQueryParameters parameters) {
            
            var account = GetAccount();

            if(!types.Contains(",")) {
                var query = parameters.Apply(GetData(account, types));
                var data = query.ToList();
                return data.ToJsonResult(Data.Settings.SerializerSettings);
            }

            var typeList = types.ToLower().Split(",").ToList();
            var results = new ConcurrentDictionary<string, List<CosmosDocument>>();
            var tasks = new List<Task>();
            
            foreach(var type in typeList) {
                
                var task = Task.Factory.StartNew(()=> {
                    var query = parameters.Apply(GetData(account, type));
                    var items = query.ToList();
                    results.AddOrUpdate(type, items, (updateType, updateItems) => updateItems);
                });

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
            
            return new JsonResult(results, Data.Settings.SerializerSettings);
        }

        [HttpGet, Route("api/data/{type}/{id}")]
        public IActionResult Find([FromRoute] string type, [FromRoute] string id) {
            var account = GetAccount();
            var entity = Data.DefaultCollection.Get<CosmosDocument>()
                .Where(each => each.Type == type && each.Id == id)
                .ToList()
                .FirstOrDefault();

            if(entity == null || !account.CanRead(entity, GetSharingParticipants(account))) {
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
                    await Interceptor.Delete(account, document);
                }

                await Data.DefaultCollection.DeleteAsync(document.Id);
            }

            return Ok(true);
        }
        
        [HttpPost, Route("api/data")]
        public async Task<IActionResult> Save([FromBody] ExpandoObject[] entities) {

            var account = GetAccount();
            var resources = new List<DocumentEntity>();

            foreach(var entity in entities) {
                var document = TryUpdateResource(account, entity);

                if(document == null || !account.CanUpdate(document, GetSharingParticipants(account))) {
                    return StatusCode(404);
                }
                
                if(document.Owner.IsEmpty()) {
                    document.Owner = account.Id;
                }
                
                resources.Add(document);
            }

            var result = await Data.DefaultCollection.CallProcedure<List<DocumentEntity>>(Data.Settings.SaveProcedureName, resources);
            return result.ToJsonResult(Data.Settings.SerializerSettings);
        }

        [HttpPost, Route("api/data/{type}")]
        public async Task<IActionResult> Save([FromRoute] string type, [FromBody] ExpandoObject entity) {
            
            var account = GetAccount();
            var document = TryUpdateResource(account, entity, type);

            if(document == null || !account.CanUpdate(document, GetSharingParticipants(account))) {
                return StatusCode(404);
            }
            
            if(document.Owner.IsEmpty()) {
                document.Owner = account.Owner;
            }

            if(Interceptor != null) {
                await Interceptor.Save(account, document);
            }
            
            var result = await Data.DefaultCollection.SaveAsync(document);
            return result.ToJsonResult(Data.Settings.SerializerSettings);
        }
        
        private CosmosDocument TryUpdateResource(Account account, ExpandoObject source, string defaultType = "") {

            var settings = new JsonSerializerSettings {
                ContractResolver = new DocumentContractResolver()
            };
            
            var serialized = JsonConvert.SerializeObject(source, settings);
            var model = JsonConvert.DeserializeObject<CosmosDocument>(serialized);
            
            //using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(serialized)))  {
                //var model = JsonSerializable.LoadFrom<CosmosDocument>(stream);

                if(model.Type.IsEmpty()) {
                    model.Type = defaultType;
                }
                
                if(model.Type == null) {
                    throw new Exception("Unset entity type.");
                }

                model.Type = model.Type.ToLower();
                
                if(!model.Id.IsEmpty()) {

                    var entity = Data.DefaultCollection.Get<CosmosDocument>()
                        .Where(each => model.Id == each.Id && model.Type == each.Type)
                        .ToList()
                        .FirstOrDefault();

                    if(entity != null) {
                        
                        if(!account.CanUpdate(entity, GetSharingParticipants(account))) {
                            return null;
                        }

                        PropertyInfo[] properties = model.Type == "account" ? typeof(Account).GetProperties() : typeof(CosmosDocument).GetProperties();

                        var editableProperties = source
                            .Where(each => !properties.Any(property => property.Name.ToLower() == each.Key.ToLower() && property.HasAttribute<SecureAttribute>()))
                            .ToList();

                        foreach(var property in editableProperties) {
                            entity.Set(property.Key, property.Value);
                        }
                            
                        entity.UpdatedBy = account.Id;
                        return entity;
                    }
                }

                // Todo:  Prevent editing secure fields?
                model.UpdatedBy = account.Id;

                if(model.Owner.IsEmpty()) {
                    model.Owner = account.Id;
                }
                else if(!account.IsAdmin()) {

                    var participants = GetSharingParticipants(account);

                    if(!participants.Contains(model.Owner)) {
                        model.Owner = account.Id;
                    }
                }

                return model;
            //}
        }
        
        private IQueryable<CosmosDocument> GetData(Account account, string type) {

            //if(parameters.Partition == "self") {
            //    parameters.Partition = account.Id;
            //}
            
            var participants = GetSharingParticipants(account);

            if(type == "account") {

                var accounts = Data.DefaultCollection.Get<Account>().Where(each => each.Type == "account");

                // Non-admin users can see participating accounts and group member accounts
                if(!account.IsAdmin()) {
                    accounts = accounts.Where(each => each.Id == account.Id || participants.Contains(each.Id));
                }
                
                return accounts;
            }

            var query = Data.DefaultCollection.Get<CosmosDocument>().Where(each => each.Type.ToLower() == type);
            var claims = new List<string> { account.Id, GlobalDataSettings.SystemOwnerName };
            
            if(GlobalDataSettings.MultiTenant && !account.IsAdmin()) {
                
                if(type == "task" || type == "field" || type == "goalstrategy" || type == "goal" || type == "dataset") {
                    query = query.Where(each => claims.Contains(each.Owner) || participants.Contains(each.Owner));
                }
                else if(type == "contact") {
                    
                    query = query.Where(contact =>
                        claims.Contains(contact.Owner)
                        || participants.Contains(contact.Owner)
                        //|| groups.Contains(contact.Owner)
                        || contact.Participants.Any(participant => participant.Id == account.Id));
                }
                else if(type == "group") {
                    query = query.Where(group => (group.Owner == account.Id || group.Participants.Any(participant => participant.Id == account.Id)));
                }
                else {
                    query = query.Where(each => claims.Contains(each.Owner) || each.Permissions.Any(permission => permission.Subject == account.Id && participants.Contains(each.Owner)));
                }
            }
            
            return query;
        }
        
        // Todo:  Move to AccountManager
        private List<string> GetSharingParticipants(Account account) {

            var participants = account.GetParticipants().Select(each => each.Id).ToList();
            var memberships = account.GetGroups();

            var groups = Data.DefaultCollection.Get<CosmosDocument>()
                .Where(each => each.Type == "group" && memberships.Contains(each.Id))
                .IsValid()
                .ToList();

            if(groups.Any()) {
                var groupOwners = groups.Select(each => each.Owner).ToList();
                var owners = Data.DefaultCollection.Get<Account>()
                    .Where(each => each.Type == "account" && groupOwners.Contains(each.Id))
                    .ToList();

                foreach(var group in groups) {
                    var owner = owners.FirstOrDefault(each => each.Id == group.Owner);

                    if(owner != null) {
                        participants.Add(owner.Id);

                        if(owner.IsGroupLeader()) {

                            if(owner.Id != account.Id) {
                                continue;
                            }
                        }

                        participants.AddRange(group.Participants.Select(each => each.Id));
                    }
                }
            }

            return participants.Distinct().ToList();
        }
        
        private Account GetAccount() {
            return Users.GetAccount();
        }

        private readonly AzureCosmosDbProvider Data;

        private readonly AccountManager Users;

        private readonly IsDataInterceptor Interceptor;
    }
}