using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
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
using Starship.Data.Configuration;
using Starship.WebCore.Extensions;
using Starship.WebCore.Interfaces;
using Starship.WebCore.Models;
using Starship.WebCore.Providers.Authentication;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class DataController : ApiController {
        
        public DataController(IServiceProvider serviceProvider) {
            Users = serviceProvider.GetRequiredService<AccountManager>();
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

        /*[HttpGet, Route("api/procs/{procedure}")]
        public IActionResult GetProcedure([FromRoute] string procedure, [FromQuery] ExpandoObject parameters) {
            var account = GetAccount();
            var query = Data.DefaultCollection.CallProcedure<CosmosDocument>(procedure, parameters);
            //var query = GetData(account, type, parameters);
            return query.ToArray().ToJsonResult(Data.Settings.SerializerSettings);
        }*/
        
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
            var resources = new List<Resource>();

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

            var result = await Data.DefaultCollection.CallProcedure<Document>(Data.Settings.SaveProcedureName, resources);
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

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(serialized)))  {
                var model = JsonSerializable.LoadFrom<CosmosDocument>(stream);

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
                            entity.SetPropertyValue(property.Key, property.Value);
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
            }
        }
        
        private IEnumerable<CosmosDocument> GetData(Account account, string type, DataQueryParameters parameters = null) {
            
            if(parameters == null) {
                parameters = new DataQueryParameters();
            }

            //if(parameters.Partition == "self") {
            //    parameters.Partition = account.Id;
            //}
            
            var participants = GetSharingParticipants(account);

            if(type == "account") {

                var accounts = GetAccounts();

                // Non-admin users can see participating accounts and group member accounts
                if(!account.IsAdmin()) {
                    
                    accounts = accounts.Where(each => each.Id == account.Id || participants.Contains(each.Id));
                }

                accounts = parameters.Apply(accounts);

                return accounts.ToList();
            }

            var query = Data.DefaultCollection.Get<CosmosDocument>().Where(each => each.Type == type);
            var claims = new List<string> { account.Id, GlobalDataSettings.SystemOwnerName };
            
            //if(!account.IsAdmin()) {
                if(type == "task" || type == "field" || type == "goalstrategy" || type == "goal") {
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
                    query = query.Where(group => group.Participants.Any(participant => participant.Id == account.Id));
                }
                else {
                    query = query.Where(each => claims.Contains(each.Owner) || each.Permissions.Any(permission => permission.Subject == account.Id && participants.Contains(each.Owner)));
                }
            //}
            
            query = parameters.Apply(query);
            
            if(!string.IsNullOrEmpty(parameters.Filter)) {
                return query.OData().Filter(parameters.Filter).ToList();
            }

            return query.ToList();
        }
        
        // Todo:  Move to AccountManager
        private List<string> GetSharingParticipants(Account account) {
            var participants = account.GetParticipants().Select(each => each.Id).ToList();
            var groups = account.GetGroups();
            var groupParticipants = Data.DefaultCollection.Get<CosmosDocument>().Where(each => each.Type == "group" && groups.Contains(each.Id)).SelectMany(each => each.Participants).Select(each => each.Id).ToList();

            participants.AddRange(groupParticipants);
            return participants.Distinct().ToList();
        }

        private IQueryable<Account> GetAccounts() {
            return Data.DefaultCollection.Get<Account>().Where(each => each.Type == "account" && each.ValidUntil == null);
        }
        
        private Account GetAccount() {
            return Users.GetAccount();
        }
        
        private readonly IsDataInterceptor Interceptor;

        private readonly AzureDocumentDbProvider Data;

        private readonly AccountManager Users;
    }
}