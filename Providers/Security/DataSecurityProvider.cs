using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.WebCore.Interfaces;

namespace Starship.WebCore.Providers.Security {

    public class DataSecurityProvider : IsDataInterceptor {
        
        public DataSecurityProvider(AzureCosmosDbProvider data) {
            Data = data;
        }

        public async Task Delete(Account account, CosmosDocument document) {

            if(document.Type == "group") {
                var existingMembers = GetAccounts().Where(each => each.Groups.Contains(document.Id)).ToList();

                if(!existingMembers.Any(each => each.Id == account.Id)) {
                    existingMembers.Add(account);
                    account.RemoveGroup(document.Id);
                }

                foreach(var member in existingMembers) {
                    member.RemoveGroup(document.Id);
                }

                if(existingMembers.Any()) {
                    await Data.DefaultCollection.CallProcedure(Data.Settings.SaveProcedureName, existingMembers);
                }
            }
        }

        public async Task Save(Account account, CosmosDocument document) {

            if(document.Type == "group") {
                var participants = document.Participants.Select(each => each.Id).ToList();
                var existingMembers = GetAccounts().Where(each => each.Groups.Contains(document.Id)).ToList();
                var newMembers = GetAccounts().Where(each => participants.Contains(each.Id)).ToList();

                if(!newMembers.Any(each => each.Id == account.Id)) {
                    newMembers.Add(account);
                }
                
                var changeset = new List<CosmosDocument>();

                foreach(var member in existingMembers) {

                    if(member.Id == account.Id) {
                        continue;
                    }

                    if(!document.HasParticipant(member.Id)) {
                        member.RemoveGroup(document.Id);
                        changeset.Add(member);
                    }
                }

                foreach(var member in newMembers) {
                    if(!member.HasGroup(document.Id)) {
                        member.AddGroup(document.Id);
                        changeset.Add(member);
                    }
                }

                if(changeset.Any()) {
                    await Data.DefaultCollection.CallProcedure(Data.Settings.SaveProcedureName, changeset);
                }
            }
        }

        private IQueryable<Account> GetAccounts() {
            return Data.DefaultCollection.Get<Account>().Where(each => each.Type == "account");
        }

        private readonly AzureCosmosDbProvider Data;
    }
}