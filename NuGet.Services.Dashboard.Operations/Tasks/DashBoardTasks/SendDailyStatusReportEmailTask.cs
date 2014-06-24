using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Web.UI;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    [Command("SendDailyStatusReportEmailTask", "Creates daily status report for various gallery metrics in the past 24 hours", AltName = "sdsret")]

    public class SendDailyStatusReportEmailTask : StorageTask
    {
        [Option("StartTime", AltName = "start")]
        public int StartTime { get; set; }

        [Option("EndTime", AltName = "end")]
        public int EndTime { get; set; }

        [Option("Date", AltName = "date")]
        public string Date { get; set; }

        public int Availability { get; set; }
        public int Downloads { get; set; }
        public int Restore { get; set; }
        public string[] SearchTerms { get; set; }
        public int Uploads { get; set; }
        public int NewUsers { get; set; }

        public int TrafficPerHour { get; set; }
        public int TrafficMax { get; set; }
        public int TrafficMin { get; set; }
        public string TrafficPerHourNotes { get; set; }

        public int RequestPerHour { get; set; }
        public int RequestMax { get; set; }
        public int RequestMin { get; set; }
        public string RequestPerHourNotes { get; set; }

        public int ErrorsPerHour { get; set; }
        public int ErrorsMax { get; set; }
        public int ErrorsMin { get; set; }
        public string ErrorsPerHourNotes { get; set; }

        public int IndexLag { get; set; }
        public int IndexMax { get; set; }
        public int IndexMin { get; set; }
        public string IndexLagNotes { get; set; }

        public int InstanceCount{
            get
            {
                return GetMetricValues("nuget-prod-0-v2galleryInstanceCount" + Date + "HourlyReport.json").First();
            }
        }
        public int InstanceMax { get; set; }
        public int InstanceMin { get; set; }
        public string InstanceCountNotes
        {
            get
            {
                return GetMetricValues("nuget-prod-0-v2galleryInstanceCount" + Date + "HourlyReport.json").First().ToString();
            }
        }

        public int OverallWorkerCount { get; set; }
        public int SuccessCount { get; set; }
        public string[] FailedJobNames { get; set; }
        public string[] NotableIssues { get; set; }

        public override void ExecuteCommand()
        {
            string dateSuffix = Date;  
            SendEmail();
        }

        private List<int> GetMetricValues(string blobName)
        {
            return GetMetricValuesFromBlob(blobName, ContainerName, 0000, 2300);
        }

        private List<int> GetMetricValuesFromBlob(string blobName, string containerName, int startTime, int endTime)
        {
            Dictionary<string, string> dict = ReportHelpers.GetDictFromBlob(StorageAccount, blobName, containerName);
            List<int> values = new List<int>();
            foreach (KeyValuePair<string, string> keyValuePair in dict)
            {
                int key = Convert.ToInt32(keyValuePair.Key.Replace(":", "").Replace("-", ""));

                if ((key >= startTime) && (key <= endTime))
                {
                    values.Add(Convert.ToInt32(keyValuePair.Value));
                }
            }
            return values;
        }

        private void SendEmail()
        {
            SmtpClient sc = new SmtpClient("smtphost");
            NetworkCredential nc = new NetworkCredential(ConfigurationManager.AppSettings["SmtpUserName"], ConfigurationManager.AppSettings["SmtpPassword"]);
            sc.UseDefaultCredentials = true;
            sc.Credentials = nc;
            sc.Host = "outlook.office365.com";
            sc.EnableSsl = true;
            sc.Port = 587;
            //ServicePointManager.ServerCertificateValidationCallback = delegate(object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) { return true; };
            System.Net.Mail.MailMessage message = new System.Net.Mail.MailMessage();
            message.From = new MailAddress(ConfigurationManager.AppSettings["SmtpUserName"], "NuGet Daily Status Report");
            message.To.Add(new MailAddress(ConfigurationManager.AppSettings["MailRecepientAddress"], ConfigurationManager.AppSettings["MailRecepientAddress"]));
            message.Subject = string.Format("NuGet Gallery Daily Status Report - " + DateTime.Now.ToShortTimeString());
            message.IsBodyHtml = true;
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(@"<html><body>" + GetMailContent() + "</body></html>", new ContentType("text/html")));

            try
            {
                sc.Send(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(" Error in sending mail : {0}", ex.Message);
                Console.ReadKey();
            }
        }

        private string GetMailContent()
        {
            StreamReader sr = new StreamReader(@"ScriptsAndReferences\DailyStatusReport.htm");
            string mailBody = sr.ReadToEnd();
            sr.Close();

            //mailBody = mailBody.Replace("{availability}", Availability.ToString());
            //mailBody = mailBody.Replace("{downloads}", Downloads.ToString());
            //mailBody = mailBody.Replace("{restore}", Restore.ToString());
            //mailBody = mailBody.Replace("{searchterms}", SearchTerms.ToString());
            //mailBody = mailBody.Replace("{uploads}", Uploads.ToString());
            //mailBody = mailBody.Replace("{newusers}", NewUsers.ToString());
            //mailBody = mailBody.Replace("{TrafficPerHour}", TrafficPerHour.ToString());
            //mailBody = mailBody.Replace("{trafficmax}", TrafficMax.ToString());
            //mailBody = mailBody.Replace("{trafficmin}", TrafficMin.ToString());
            //mailBody = mailBody.Replace("{TrafficPerHourNotes}", TrafficPerHourNotes.ToString());
            //mailBody = mailBody.Replace("{RequestPerHour}", RequestPerHour.ToString());
            //mailBody = mailBody.Replace("{requestmax}", RequestMax.ToString());
            //mailBody = mailBody.Replace("{requestmin}", RequestMin.ToString());
            //mailBody = mailBody.Replace("{RequestPerHourNotes}", RequestPerHourNotes.ToString());
            //mailBody = mailBody.Replace("{ErrorsPerHour}", ErrorsPerHour.ToString());
            //mailBody = mailBody.Replace("{ErrorsMax}", ErrorsMax.ToString());
            //mailBody = mailBody.Replace("{ErrorsMin}", ErrorsMin.ToString());
            //mailBody = mailBody.Replace("{ErrorsPerHourNotes}", ErrorsPerHourNotes.ToString());
            //mailBody = mailBody.Replace("{IndexLag}", IndexLag.ToString());
            //mailBody = mailBody.Replace("{indexmax}", IndexMax.ToString());
            //mailBody = mailBody.Replace("{indexmin}", IndexMin.ToString());
            //mailBody = mailBody.Replace("{IndexLagNotes}", IndexLagNotes.ToString());
            mailBody = mailBody.Replace("{InstanceCount}", InstanceCount.ToString());
            //mailBody = mailBody.Replace("{instancemax}", InstanceMax.ToString());
            //mailBody = mailBody.Replace("{instancemin}", InstanceMin.ToString());
            //mailBody = mailBody.Replace("{InstanceCountNotes}", InstanceCountNotes.ToString());
            //mailBody = mailBody.Replace("{overallworkercount}", OverallWorkerCount.ToString());
            //mailBody = mailBody.Replace("{successcount}", SuccessCount.ToString());
            //mailBody = mailBody.Replace("{failedjobnames}", FailedJobNames.ToString());
            //mailBody = mailBody.Replace("{notableissues}", NotableIssues.ToString());

            return mailBody;
        }
    }
}
