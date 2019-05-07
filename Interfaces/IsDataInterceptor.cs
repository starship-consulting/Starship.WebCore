using System;
using System.Threading.Tasks;
using Starship.Azure.Data;

namespace Starship.WebCore.Interfaces {
    public interface IsDataInterceptor {

        Task Save(Account account, CosmosDocument document);

        Task Delete(Account account, CosmosDocument document);
    }
}