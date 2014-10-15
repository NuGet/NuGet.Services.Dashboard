using Newtonsoft.Json.Linq;
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
using System.Web.Script.Serialization;


namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    [Command("SendDailyStatusReportEmailTask", "Creates daily status report for various gallery metrics in the past 24 hours", AltName = "sdsret")]

    public class SendDailyStatusReportEmailTask : StorageTask
    {
        [Option("Recepient", AltName = "rec")]
        public string MailRecepientAddress { get; set; }

        [Option("Date", AltName = "date")]
        public string Date 
        { 
            get
            {
                _ydate = string.Format("{0:MMdd}", DateTime.Now.AddDays(-1));
                return _ydate;
            } 
            set         
            {
                _ydate = value;
            }
        }

        private string _ydate;
        public const int SecondsInAnHour = 3600;

        public double Availability
        {
            get
            {
                return CalculateGalleryDailyUpTime();
            }
        }

        public int Downloads
        {
            get
            {
                int allDownloads;
                allDownloads = GetDownloadNumbersFromBlob("Install1Day.json") + GetDownloadNumbersFromBlob("Install-Dependency1Day.json") +
                                GetDownloadNumbersFromBlob("Update1Day.json") + GetDownloadNumbersFromBlob("Update-Dependency1Day.json") +
                                GetDownloadNumbersFromBlob("Reinstall1Day.json") + GetDownloadNumbersFromBlob("Reinstall-Dependency7Day.json");
                return allDownloads;
            }
        }

        public int Restore
        {
            get
            {
                return GetDownloadNumbersFromBlob("Restore1Day.json") + GetDownloadNumbersFromBlob("Restore-Dependency1Day.json");
            }
        }

        public int SearchQueries
        {
            get
            {
                int avgTime = 0;
                List<string> _dates = new List<string>();
                _dates.Add(Date);
                return ReportHelpers.GetQueryNumbers("Search", out avgTime, _dates, StorageAccount, ContainerName);
            }
        }

        private string CreateTableForIISRequestsDistribution()
        {
            List<string> _dates = new List<string>();
            _dates.Add(Date);
            return ReportHelpers.CreateTableForIISRequestsDistribution(StorageAccount, ContainerName, _dates);
        }


        public string[] SearchTerms
        {
            get
            {
                return new string[] { "jQuery", "Json.net" };
            }
        }

        public int Uploads
        {
            get
            {
                return GetTupleMetricValues("Uploads" + Date + "HourlyReport.json").Item2;
            }
        }

        public int UniqueUploads
        {
            get
            {
                return GetTupleMetricValues("UniqueUploads" + Date + "HourlyReport.json").Item2;
            }
        }

        public int NewUsers
        {
            get
            {
                return GetTupleMetricValues("Users" + Date + "HourlyReport.json").Item2;
            }
        }

        public int TrafficPerHour
        {
            get
            {
                return GetTupleMetricValues("IISRequests" + Date + ".json").Item1;
            }
        }

        public int TrafficMax
        {
            get
            {
                return GetTupleMetricValues("IISRequests" + Date + ".json").Item3;
            }
        }

        public int TrafficMin
        {
            get
            {
                return GetTupleMetricValues("IISRequests" + Date + ".json").Item4;
            }
        }

        public int RequestPerHour
        {
            get
            {
                return GetTupleMetricValues("DBRequests" + Date + ".json").Item1;
            }
        }

        public int RequestMax
        {
            get
            {
                return GetTupleMetricValues("DBRequests" + Date + ".json").Item3;
            }
        }

        public int RequestMin
        {
            get
            {
                return GetTupleMetricValues("DBRequests" + Date + ".json").Item4;
            }
        }

        public int ErrorsPerHour
        {
            get
            {
                return GetTupleMetricValues("ErrorRate" + Date + ".json").Item1;
            }
        }
        public int ErrorsMax
        {
            get
            {
                return GetTupleMetricValues("ErrorRate" + Date + ".json").Item3;
            }
        }
        public int ErrorsMin
        {
            get
            {
                return GetTupleMetricValues("ErrorRate" + Date + ".json").Item4;
            }
        }

        public int IndexLag
        {
            get
            {
                return GetTupleMetricValues("IndexingDiffCount" + Date + "HourlyReport.json").Item1;
            }
        }

        public int IndexMax
        {
            get
            {
                return GetTupleMetricValues("IndexingDiffCount" + Date + "HourlyReport.json").Item3;
            }
        }

        public int IndexMin
        {
            get
            {
                return GetTupleMetricValues("IndexingDiffCount" + Date + "HourlyReport.json").Item4;
            }
        }

        public int InstanceCount
        {
            get
            {
                return GetTupleMetricValues("nuget-prod-0-v2galleryInstanceCount" + Date + "HourlyReport.json").Item1;
            }
        }

        public int InstanceMax
        {
            get
            {
                return GetTupleMetricValues("nuget-prod-0-v2galleryInstanceCount" + Date + "HourlyReport.json").Item3;
            }
        }

        public int InstanceMin
        {
            get
            {
                return GetTupleMetricValues("nuget-prod-0-v2galleryInstanceCount" + Date + "HourlyReport.json").Item4;
            }
        }

        public int OverallWorkerCount
        {
            get
            {
                List<WorkInstanceDetail> details = GetWorkJobDetail();
                return details.Count;
            }
        }

        public int SuccessCount
        {
            get
            {
                return GetFailedJobDetails().Item1;
            }
        }

        public string[] FailedJobNames
        {
            get
            {
                return GetFailedJobDetails().Item2;
            }
        }

        public string[] NotableIssues
        {
            get
            {
                return GetFailedJobDetails().Item3;
            }
        }

        public override void ExecuteCommand()
        {
            string dateSuffix = Date;  
            SendEmail();
        }

        /// <summary>
        /// Creates the Html for a table using Install/ Update/ Restore data
        /// </summary>
        /// <returns></returns>
        public string InstallUpdatesRestoresByNuGetVersion()
        {
            string[] installBlobNames = { "Install1Day.json", "Install-Dependency1Day.json", "Update1Day.json", "Update-Dependency1Day.json", "Reinstall1Day.json", "Reinstall-Dependency1Day.json" };
            List<string> versions = new List<string>();
            List<object> installs = new List<object>();
            List<object> restores = new List<object>();

            ReportHelpers.GetValuesFromBlobs(installBlobNames, StorageAccount, ContainerName, out versions, out installs);

            string[] restoreBlobNames = { "Restore1Day.json", "Restore-Dependency1Day.json" };
            ReportHelpers.GetValuesFromBlobs(restoreBlobNames, StorageAccount, ContainerName, out versions, out restores);

            string installChartHtml = ReportHelpers.GetOperationsPerNuGetVersionTable(installs, restores, versions, "Install/Updates and Restores");
            return installChartHtml;
        }

        /// <summary>
        /// Creates the Html for a table using Install/ Update/ Restore data
        /// </summary>
        /// <returns></returns>
        public string InstallUpdatesRestoresByVSVersion()
        {
            string[] installBlobNames = { "VsTrend1Day.json" };
            List<string> versions = new List<string>();
            List<object> installs = new List<object>();
            List<object> restores = new List<object>();

            ReportHelpers.GetValuesFromBlobs(installBlobNames, StorageAccount, ContainerName, out versions, out installs);

            string[] restoreBlobNames = { "VsRestoreTrend1Day.json"};
            ReportHelpers.GetValuesFromBlobs(restoreBlobNames, StorageAccount, ContainerName, out versions, out restores);

            string installChartHtml = ReportHelpers.GetOperationsPerNuGetVersionTable(installs, restores, versions, "Install/Updates and Restores");
            return installChartHtml;
        }

        public string IPDetails()
        {
            return ReportHelpers.CreateTableForIPDistribution(StorageAccount, ContainerName, DateTime.Now);
        }

        public string ResponseTimeDetails()
        {
            return ReportHelpers.CreateTableForResponseTime(StorageAccount, ContainerName, DateTime.Now);
        }

        public string UserAgentDetails()
        {
            return ReportHelpers.CreateTableForUserAgent(StorageAccount, ContainerName, DateTime.Now);
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
            message.To.Add(new MailAddress(MailRecepientAddress, MailRecepientAddress));
            message.Subject = string.Format("NuGet Gallery Daily Status Report - for " + DateTime.Today.AddDays(-1).ToShortDateString());
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

            mailBody = mailBody.Replace("{availability}", Availability.ToString("f2") + "%");
            mailBody = mailBody.Replace("{downloads}", Downloads.ToString("#,##0"));
            mailBody = mailBody.Replace("{restore}", Restore.ToString("#,##0"));
            if (Downloads == 0 || Restore == 0)
            {
                mailBody = mailBody.Replace("{warning}", "Please note that this report will not show the correct numbers for download and restore when ReplicatePackageStats Job is disabled or failing.");
            }
            else
            {
                mailBody = mailBody.Replace("{warning}", "");
            }
            mailBody = mailBody.Replace("{searchqueries}", SearchQueries.ToString("#,##0"));
            mailBody = mailBody.Replace("{uploads}", Uploads.ToString());
            mailBody = mailBody.Replace("{uniqueuploads}", UniqueUploads.ToString());
            mailBody = mailBody.Replace("{newusers}", NewUsers.ToString());
            mailBody = mailBody.Replace("{TrafficPerHour}", TrafficPerHour.ToString("#,##0"));
            mailBody = mailBody.Replace("{trafficmax}", TrafficMax.ToString("#,##0"));
            mailBody = mailBody.Replace("{trafficmin}", TrafficMin.ToString("#,##0"));
            mailBody = mailBody.Replace("{RequestPerHour}", RequestPerHour.ToString());
            mailBody = mailBody.Replace("{requestmax}", RequestMax.ToString());
            mailBody = mailBody.Replace("{requestmin}", RequestMin.ToString());
            mailBody = mailBody.Replace("{ErrorsPerHour}", ErrorsPerHour.ToString());
            mailBody = mailBody.Replace("{errormax}", ErrorsMax.ToString());
            mailBody = mailBody.Replace("{errormin}", ErrorsMin.ToString());
            mailBody = mailBody.Replace("{IndexLag}", IndexLag.ToString());
            mailBody = mailBody.Replace("{indexmax}", IndexMax.ToString());
            mailBody = mailBody.Replace("{indexmin}", IndexMin.ToString());
            mailBody = mailBody.Replace("{InstanceCount}", InstanceCount.ToString());
            mailBody = mailBody.Replace("{instancemax}", InstanceMax.ToString());
            mailBody = mailBody.Replace("{instancemin}", InstanceMin.ToString());
            mailBody = mailBody.Replace("{overallworkercount}", OverallWorkerCount.ToString());
            mailBody = mailBody.Replace("{successcount}", SuccessCount.ToString());
            mailBody = mailBody.Replace("{failedjobnames}", string.Join(", ", FailedJobNames));
            mailBody = mailBody.Replace("{notableissues}", string.Join("<br/>", NotableIssues));
            mailBody = mailBody.Replace("{InstallUpdatesRestoresPerNuGetVersion}", InstallUpdatesRestoresByNuGetVersion());
            mailBody = mailBody.Replace("{InstallUpdatesRestoresPerVSVersion}", InstallUpdatesRestoresByVSVersion());
            mailBody = mailBody.Replace("{IISRequestsDistribution}", CreateTableForIISRequestsDistribution());
            mailBody = mailBody.Replace("{IPRequestDistribution}", IPDetails());
            mailBody = mailBody.Replace("{ResponseTime}", ResponseTimeDetails());
            mailBody = mailBody.Replace("{UserAgent}", UserAgentDetails());
            return mailBody;
        }

        #region Helper Methods
        private Tuple<int, int, int, int> GetTupleMetricValues(string blobName)
        {
            List<int> list = GetMetricValues(blobName);
            int average = list.Sum() / list.Count;
            int sum = list.Sum();
            int maximum = list.Max();
            int minimum = list.Min();
            return new Tuple<int, int, int, int>(average, sum, maximum, minimum);
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

        private int GetMetricCountFromBlob(string blobName)
        {
            string content = ReportHelpers.Load(StorageAccount, blobName, ContainerName);
            JArray jArray = JArray.Parse(content);
            return jArray.Count;
        }

        private double CalculateGalleryDailyUpTime()
        {
            // Up time for Feed.top.30.by.downloads
            double feedTop30DownloadsUpTime = GetTupleMetricValues("feed.top.30.by.downloads" + Date + "outageReport.json").Item1;
            // Up time for Package.restore.download
            double packageRestoreDownloadUpTime = GetTupleMetricValues("package.restore.download" + Date + "outageReport.json").Item1;
            // Up time for Package.restore.lookup
            double packageRestoreLookupUpTime = GetTupleMetricValues("package.restore.lookup" + Date + "outageReport.json").Item1;

            List<double> uptimes = new List<double>() { feedTop30DownloadsUpTime, packageRestoreDownloadUpTime, packageRestoreLookupUpTime };
            double overallUpTime = (uptimes.Min() / SecondsInAnHour) * 100.00;
            return overallUpTime;
        }

        private int GetDownloadNumbersFromBlob(string blobName)
        {
            Dictionary<string, string> dict = ReportHelpers.GetDictFromBlob(StorageAccount, blobName, ContainerName);
            List<int> values = new List<int>();
            foreach (KeyValuePair<string, string> keyValuePair in dict)
            {
                values.Add(Convert.ToInt32(keyValuePair.Value));
            }
            return values.Sum();
        }

        private int GetSearchQueryNumbersFromBlob()
        {
            Dictionary<string, string> dict = ReportHelpers.GetDictFromBlob(StorageAccount, "IISRequestDetails" + Date + ".json", ContainerName);
            List<IISRequestDetails> requestDetails = new List<IISRequestDetails>();
            int totalSearchRequestNumber = 0;
            
            if (dict != null)
            {
                foreach (KeyValuePair<string, string> keyValuePair in dict)
                {
                    requestDetails = new JavaScriptSerializer().Deserialize<List<IISRequestDetails>>(keyValuePair.Value);
                    foreach (IISRequestDetails detail in requestDetails)
                    {
                        if (detail.ScenarioName == "Search")
                        {
                            totalSearchRequestNumber += detail.RequestsPerHour;
                        }
                    }
                }
            }
            return totalSearchRequestNumber;
        }

        private List<WorkInstanceDetail> GetWorkJobDetail()
        {
            List<WorkInstanceDetail> jobDetail = new List<WorkInstanceDetail>();
            var content = ReportHelpers.Load(StorageAccount, "WorkJobDetail.json", ContainerName);
            if (content != null)
            {
                jobDetail = new JavaScriptSerializer().Deserialize<List<WorkInstanceDetail>>(content);
            }
            return jobDetail;
        }

        private Tuple<int, string[], string[]> GetFailedJobDetails()
        {
            List<WorkInstanceDetail> jobDetail = GetWorkJobDetail();
            List<string> failedJobNames = new List<string>();
            List<string> notableIssues = new List<string>();
            int count = jobDetail.Count;
            foreach (WorkInstanceDetail detail in jobDetail)
            {
                if (detail.FaultedNo != "0")
                {
                    count--;
                    failedJobNames.Add(detail.jobName);
                    if (detail.ErrorMessage.Keys.First().Length > 100)
                    {
                        notableIssues.Add(detail.ErrorMessage.Keys.First().Substring(0, 100) + ".....<br/>");
                    }
                    else
                    {
                        notableIssues.Add(detail.ErrorMessage.Keys.First().ToString() + ".....<br/>");
                    }
                }
            }
            notableIssues.Add("<br/>For more details, please refer to https://dashboard.nuget.org/WorkJobs/WorkJobs_Detail.");
            return new Tuple<int, string[], string[]>(count, failedJobNames.ToArray(), notableIssues.ToArray());
        }
        #endregion
    }
}
