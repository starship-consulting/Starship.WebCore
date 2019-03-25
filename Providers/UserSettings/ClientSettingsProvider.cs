using System;
using Starship.Azure.Data;
using Starship.WebCore.Configuration;
using Starship.WebCore.Providers.Interfaces;

namespace Starship.WebCore.Providers.UserSettings {

    public class ClientSettingsProvider : IsUserSettingsProvider {

        public ClientSettingsProvider(ClientSettings settings) {
            Settings = settings;
        }

        public void Apply(Account user) {
            user.clientSettings = Settings;
        }

        private ClientSettings Settings { get; set; }
    }
}