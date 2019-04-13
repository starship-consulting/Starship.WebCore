using System;
using System.Linq;
using System.Threading.Tasks;
using PostmarkDotNet;
using PostmarkDotNet.Legacy;
using PostmarkDotNet.Model;
using Starship.Azure.Data;
using Starship.Core.Email;
using Starship.WebCore.Interfaces;

namespace Starship.WebCore.Providers.Postmark {
    public class PostmarkProvider : IsEmailProvider {

        public PostmarkProvider(PostmarkSettings settings) {
            Settings = settings;
            AdminClient = new PostmarkAdminClient(settings.AccountApiToken);
            Client = new PostmarkClient(settings.ServerToken);
        }

        public async Task<bool> Verify(Account account, string email) {

            PostmarkCompleteSenderSignature signature = null;

            try {
                signature = await GetSignature(account);
            }
            catch {
            }

            if(signature != null) {
                if(signature.EmailAddress.ToLower() == email.ToLower()) {

                    if(signature.Confirmed) {
                        return true;
                    }

                    await AdminClient.ResendSignatureVerificationEmailAsync(account.OutboundEmailId);
                }
                else {
                    await AdminClient.DeleteSignatureAsync(account.OutboundEmailId);
                    signature = null;
                }
            }

            if(signature == null) {
                signature = await AdminClient.CreateSignatureAsync(email, account.FirstName + " " + account.LastName);
            }

            account.OutboundEmail = email;
            account.OutboundEmailId = signature.ID;

            return false;
        }

        public async Task<bool> Send(Account account, EmailModel email) {

            if(account.OutboundEmailId == 0) {
                return false;
            }

            var signature = await AdminClient.GetSenderSignatureAsync(account.OutboundEmailId);

            if(signature == null) {
                return false;
            }
            
            var message = new PostmarkMessage();
            message.To = string.Join(',', email.To.ToArray());
            message.Subject = email.Subject;
            message.TextBody = email.GetPlainText();
            message.HtmlBody = email.GetHtml();
            message.From = signature.EmailAddress;

            if(account.OutboundEmailBCC) {
                message.Bcc = signature.EmailAddress;
            }

            Client.SendMessage(message);
            return true;
        }

        private async Task<PostmarkCompleteSenderSignature> GetSignature(Account account) {
            
            if(account.OutboundEmailId == 0) {
                return null;
            }
            
            var signature = await AdminClient.GetSenderSignatureAsync(account.OutboundEmailId);

            return signature;
        }

        private PostmarkSettings Settings { get; set; }

        private PostmarkAdminClient AdminClient { get; set; }

        private PostmarkClient Client { get; set; }
    }
}