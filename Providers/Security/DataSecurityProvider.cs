using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.WebCore.Extensions;
using Starship.WebCore.Interfaces;

namespace Starship.WebCore.Providers.Security {

    public class DataSecurityProvider : IsDataInterceptor {
        
        public DataSecurityProvider(AzureDocumentDbProvider data) {
            Data = data;
        }

        public async Task Delete(CosmosDocument document) {

            if(document.Type == "group") {
                var existingMembers = GetAccounts().Where(each => each.Groups.Contains(document.Id)).ToList();

                foreach(var member in existingMembers) {
                    member.Groups = member.Groups.Where(each => each != document.Id).ToList();
                }

                if(existingMembers.Any()) {
                    await Data.DefaultCollection.CallProcedure<Document>(Data.Settings.SaveProcedureName, existingMembers.Cast<Resource>().ToList());
                }
            }
        }

        public async Task Save(CosmosDocument document) {

            if(document.Type == "group") {

                var existingMembers = GetAccounts().Where(each => each.Groups.Contains(document.Id)).ToList();
                var newMembers = GetAccounts().Where(each => document.Participants.Any(participant => participant.Id == each.Id)).ToList();

                var changeset = new List<CosmosDocument>();

                foreach(var member in existingMembers) {
                    if(!document.HasParticipant(member.Id)) {
                        member.Groups = member.Groups.Where(each => each != document.Id).ToList();
                        changeset.Add(member);
                    }
                }

                foreach(var member in newMembers) {
                    if(!member.Groups.Contains(document.Id)) {
                        var groups = member.Groups.ToList();
                        groups.Add(document.Id);
                        member.Groups = groups.ToList();
                        changeset.Add(member);
                    }
                }

                if(changeset.Any()) {
                    await Data.DefaultCollection.CallProcedure<Document>(Data.Settings.SaveProcedureName, changeset.Cast<Resource>().ToList());
                }
            }
        }

        private IQueryable<Account> GetAccounts() {
            return Data.DefaultCollection.Get<Account>().Where(each => each.Type == "account");
        }

        private readonly AzureDocumentDbProvider Data;
    }
}