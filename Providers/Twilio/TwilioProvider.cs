using System;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Starship.WebCore.Providers.Twilio {
    public class TwilioProvider {

        public TwilioProvider(TwilioSettings settings) {
            Settings = settings;
            TwilioClient.Init(Settings.SID, Settings.AuthToken);
        }

        public async Task SendTextMessageAsync(string message, string fromNumber, string toNumber) {
            await MessageResource.CreateAsync(
                body: message,
                from: new PhoneNumber(fromNumber), // phone number format example: "+15017122661"
                to: new PhoneNumber(toNumber)
            );
        }

        public void SendTextMessage(string message, string fromNumber, string toNumber) {
            MessageResource.Create(
                body: message,
                from: new PhoneNumber(fromNumber),
                to: new PhoneNumber(toNumber)
            );
        }

        private TwilioSettings Settings { get; set; }
    }
}