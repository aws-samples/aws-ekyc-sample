using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using ekyc_api.DataDefinitions;

namespace ekyc_api.Utils
{
    public class Notifications
    {
        public async Task SendVerificationFailureNotification(SessionObject session, string error)
        {
            if (string.IsNullOrEmpty(Globals.ApprovalsSnsTopic))
                return;

            var client =
                new AmazonSimpleNotificationServiceClient();

            var msg =
                $"The livness verification for session {session.Id} has failed due to the following reason: \n\n{error}." +
                "\n\nPlease check the log files for futher information.";

            await client.PublishAsync(new PublishRequest(Globals.ApprovalsSnsTopic, msg));
        }
    }
}