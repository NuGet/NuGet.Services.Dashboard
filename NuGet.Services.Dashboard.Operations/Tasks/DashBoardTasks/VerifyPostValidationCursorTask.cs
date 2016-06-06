using System;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    [Command("VerifyPostValidationCursorTask", "Checks the post-validation cursor lag.", AltName = "vpvc")]
    class VerifyPostValidationCursorTask : OpsTask
    {
        [Option("CursorUrl", AltName = "cu")]
        public string CursorUrl { get; set; }

        public override void ExecuteCommand()
        {
            WebRequest request = WebRequest.Create(CursorUrl);
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());

                var lastCreated = DateTimeOffset.Parse(objects["lastCreated"]);

                if (lastCreated.UtcDateTime < DateTimeOffset.UtcNow.AddDays(-2).UtcDateTime)
                {
                    new SendAlertMailTask
                    {
                        AlertSubject = "Error: Post validation job may not be running",
                        Details = string.Format("Post validation job may not be running. Cursor was last updated {0} (UTC).", lastCreated.UtcDateTime.ToString("O")),
                        AlertName = "Error: Post validation",
                        Component = "PostValidation",
                        Level = "Error"
                    }.ExecuteCommand();
                }
                else if (lastCreated.UtcDateTime < DateTimeOffset.UtcNow.AddDays(-1).UtcDateTime)
                {
                    new SendAlertMailTask
                    {
                        AlertSubject = "Warning: Post validation job may not be running",
                        Details = string.Format("Post validation job may not be running. Cursor was last updated {0} (UTC).", lastCreated.UtcDateTime.ToString("O")),
                        AlertName = "Warning: Post validation",
                        Component = "PostValidation",
                        Level = "Warning"
                    }.ExecuteCommand();
                }
            }
        }
    }
}