using System;
using Starship.WebCore.Configuration;

namespace Starship.WebCore.Interfaces {
    public interface IsClientSettingsProvider {

        void ApplySettings(ClientSettings settings);
    }
}