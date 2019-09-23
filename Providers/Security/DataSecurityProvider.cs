using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Security;
using Starship.Data.Entities;
using Starship.Data.Interfaces;

namespace Starship.WebCore.Providers.Security {

    public class DataSecurityProvider : IsDataInterceptor {
        
        public DataSecurityProvider(AzureCosmosDbProvider data) {
            Data = data;
        }

        public async Task Delete(IsSecurityContext context, DocumentEntity document) {

            if(document.Type == "group") {
                var existingMembers = GetAccounts().Where(each => each.Groups.Contains(document.Id)).ToList();

                if(!existingMembers.Any(each => each.Id == context.Id)) {
                    existingMembers.Add(context);
                    context.RemoveGroup(document.Id);
                }

                foreach(var member in existingMembers) {
                    member.RemoveGroup(document.Id);
                }

                if(existingMembers.Any()) {
                    await Data.DefaultCollection.CallProcedure(Data.Settings.SaveProcedureName, existingMembers);
                }
            }
        }

        public async Task Save(IsSecurityContext context, DocumentEntity document) {

            if(document.Type == "group") {
                var participants = document.Participants.Select(each => each.Id).ToList();
                var existingMembers = GetAccounts().Where(each => each.Groups.Contains(document.Id)).ToList();
                var newMembers = GetAccounts().Where(each => participants.Contains(each.Id)).ToList();

                if(!newMembers.Any(each => each.Id == context.Id)) {
                    newMembers.Add(context);
                }
                
                var changeset = new List<IsSecurityContext>();

                foreach(var member in existingMembers) {

                    if(member.Id == context.Id) {
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

        private IQueryable<IsSecurityContext> GetAccounts() {
            return Data.DefaultCollection.Get<Account>().Where(each => each.Type == "account");
        }

        private readonly AzureCosmosDbProvider Data;
    }
}