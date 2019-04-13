using System;
using System.Threading.Tasks;
using Starship.Azure.Data;
using Starship.Core.Email;

namespace Starship.WebCore.Interfaces {
    public interface IsEmailProvider {
        Task<bool> Verify(Account account, string email);

        Task<bool> Send(Account account, EmailModel email);
    }
}