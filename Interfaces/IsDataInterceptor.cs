using System;
using System.Threading.Tasks;
using Starship.Azure.Data;

namespace Starship.WebCore.Interfaces {
    public interface IsDataInterceptor {

        Task Save(CosmosDocument document);

        Task Delete(CosmosDocument document);
    }
}