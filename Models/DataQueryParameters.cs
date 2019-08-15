using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Linq;
using Starship.Azure.Data;
using Starship.Core.Extensions;
using Starship.WebCore.OData;

namespace Starship.WebCore.Models {
    public class DataQueryParameters {
        
        public IQueryable<T> Apply<T>(IQueryable<T> query) where T : CosmosDocument {
            
            if(IncludeInvalidated == null) {
                query = query.Where(each => !each.ValidUntil.IsDefined() || each.ValidUntil == null || each.ValidUntil > DateTime.UtcNow);
            }
            else {
                query = query.Where(each => !each.ValidUntil.IsDefined() || each.ValidUntil == null || each.ValidUntil > IncludeInvalidated);
            }

            if(!Filter.IsEmpty()) {
                var predicate = new DictionaryODataFilterLanguage().Parse<T>(Filter);
                query = query.Where(predicate);
            }

            if(Skip > 0) {
                query = query.Skip(Skip);
            }

            if(!string.IsNullOrEmpty(Order)) {
                if(Order.Contains(" desc")) {
                    var order = Order.Split(" ")[0];
                    query = query.OrderByDescending(x => x[order]);
                }
                else {
                    var order = Order.Split(" ")[0];
                    query = query.OrderBy(x => x[order]);
                }
            }

            if(Top > 0) {
                query = query.Take(Top);
            }

            return query;
        }

        public DateTime? IncludeInvalidated { get; set; }
        
        [FromQuery(Name = "$filter")]
        public string Filter { get; set; }
        
        [FromQuery(Name = "$orderby")]
        public string Order { get; set; }

        [FromQuery(Name = "$skip")]
        public int Skip { get; set; }

        [FromQuery(Name = "$top")]
        public int Top { get; set; }
    }
}