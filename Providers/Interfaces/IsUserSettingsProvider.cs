using System;
using Starship.Azure.Data;

namespace Starship.WebCore.Providers.Interfaces {
    public interface IsUserSettingsProvider {
        void Apply(Account user);
    }
}