using System;
using System.Linq;
using Microsoft.Azure.Documents.SystemFunctions;
using Starship.Azure.Data;

namespace Starship.WebCore.Models {
    public class DataQueryParameters {

        public IQueryable<T> Apply<T>(IQueryable<T> query) where T : CosmosDocument {

            if(IncludeInvalidated == null) {
                query = query.Where(each => !each.ValidUntil.IsDefined() || each.ValidUntil == null || each.ValidUntil > DateTime.UtcNow);
            }
            else {
                query = query.Where(each => !each.ValidUntil.IsDefined() || each.ValidUntil == null || each.ValidUntil > IncludeInvalidated);
            }

            if(Skip > 0) {
                query = query.Skip(Skip);
            }

            if(Top > 0) {
                query = query.Take(Top);
            }

            return query;
        }

        public DateTime? IncludeInvalidated { get; set; }

        public string Filter { get; set; }

        public string Partition { get; set; }

        public int Skip { get; set; }

        public int Top { get; set; }
    }
}