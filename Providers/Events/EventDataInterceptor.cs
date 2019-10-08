using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Starship.Azure.Data;
using Starship.Azure.Json;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.EventSourcing;
using Starship.Core.Security;
using Starship.Data.Entities;
using Starship.Data.Interfaces;
using Starship.Data.Utilities;

namespace Starship.WebCore.Providers.Events {
    public class EventDataInterceptor : IsDataInterceptor {
        
        public EventDataInterceptor(AzureCosmosDbProvider data) {
            Data = data;
        }

        public async Task<DocumentChangeset> Save(IsSecurityContext context, DocumentEntity document) {
            
            var changeset = new DocumentChangeset { document };

            if(document.Type == "event") {
                var cosmosEvent = JsonConvert.DeserializeObject<CosmosEvent>(JsonConvert.SerializeObject(document));
                var eventSink = EventSinkRegistrar.GetEventSink(cosmosEvent.Name);

                if(cosmosEvent.Parameters == null) {
                    cosmosEvent.Parameters = new CosmosEventParameters();
                }

                if(eventSink != null) {
                    
                    var aggregateDocument = Data.DefaultCollection.Get<CosmosDocument>()
                        .Where(each => each.Type == cosmosEvent.Source.Type && each.Id == cosmosEvent.Source.Id)
                        .Take(1)
                        .ToList()
                        .FirstOrDefault();
                    
                    if(aggregateDocument != null) {
                        
                        var eventObject = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(cosmosEvent.Parameters), eventSink.EventType);

                        var aggregateObject = aggregateDocument.ConvertTo(eventSink.AggregateType);

                        eventSink.Apply(eventObject, aggregateObject);

                        var settings = new JsonSerializerSettings {
                            ContractResolver = new DocumentContractResolver()
                        };

                        var aggregateJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(aggregateObject, settings), settings);
                        aggregateDocument.Apply(context, aggregateJson, eventSink.AggregateType.Name);

                        changeset.Add(aggregateDocument);
                    }
                }
            }

            return changeset;
        }

        public async Task Delete(IsSecurityContext context, DocumentEntity document) {

        }

        private readonly AzureCosmosDbProvider Data;
    }
}