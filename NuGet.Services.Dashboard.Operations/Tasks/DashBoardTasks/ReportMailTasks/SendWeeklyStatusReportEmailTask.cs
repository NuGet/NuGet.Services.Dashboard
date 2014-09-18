using Newtonsoft.Json.Linq;
using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Web.Script.Serialization;
using DotNet.Highcharts.Enums;
using DotNet.Highcharts.Helpers;
using DotNet.Highcharts.Options;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks.ReportMailTasks
{
    [Command("SendWeeklyStatusReportEmailTask", "Creates weekly status report for various gallery metrics in the past week", AltName = "swsret")]
    public class SendWeeklyStatusReportEmailTask : StorageTask
    {
        [Option("Recepient", AltName = "rec")]
        public string MailRecepientAddress { get; set; }

        [Option("Date", AltName = "date")]
        public List<string> DatesInWeek
        {
            get
            {
                if (!flag)
                {
                    for (int i = 1; i <= 7; i++)
                    {
                        string date = string.Format("{0:MMdd}", DateTime.Now.AddDays(-i));
                        _dates.Add(date);
                    }
                    flag = true;
                }
                return _dates;
            }
            set
            {
                _dates = value;
            }
        }

        private List<string> _dates = new List<string>();
        public const int SecondsInAnHour = 3600;
        private bool flag;

        #region Charts
 
        /// <summary>
        /// Creates the Html for a table using Install/ Update/ Restore data
        /// </summary>
        /// <returns></returns>
        public string InstallUpdatesRestoresByNuGetVersion()
        {
            string[] installBlobNames = {"Install7Day.json", "Install-Dependency7Day.json", "Update7Day.json", "Update-Dependency7Day.json", "Reinstall7Day.json", "Reinstall-Dependency7Day.json"};
            List<string> versions = new List<string>();
            List<object> installs = new List<object>();
            List<object> restores = new List<object>();

            ReportHelpers.GetValuesFromBlobs(installBlobNames, StorageAccount, ContainerName, out versions, out installs);

            string[] restoreBlobNames = { "Restore7Day.json", "Restore-Dependency7Day.json"};
            ReportHelpers.GetValuesFromBlobs(restoreBlobNames, StorageAccount, ContainerName, out versions, out restores);
           
            string installChartHtml = ReportHelpers.GetOperationsPerNuGetVersionTable(installs,restores, versions, "Install/Updates and Restores");
            return installChartHtml;
        }

        /// <summary>
        /// Creates the Html for a table using Install/ Update/ Restore data
        /// </summary>
        /// <returns></returns>
        public string InstallUpdatesRestoresByVSVersion()
        {
            string[] installBlobNames = { "VsTrend7Day.json"};
            List<string> versions = new List<string>();
            List<object> installs = new List<object>();
            List<object> restores = new List<object>();

            ReportHelpers.GetValuesFromBlobs(installBlobNames, StorageAccount, ContainerName, out versions, out installs);

            string[] restoreBlobNames = { "VsRestoreTrend7Day.json"};
            ReportHelpers.GetValuesFromBlobs(restoreBlobNames, StorageAccount, ContainerName, out versions, out restores);

            string installChartHtml = ReportHelpers.GetOperationsPerNuGetVersionTable(installs, restores, versions, "Install/Updates and Restores");
            return installChartHtml;
        }

        /// <summary>
        /// Creates the Html for a Chart using Restore data
        /// </summary>
        /// <returns></returns>
        public string RestoreByNuGetVersionChart()
        {
            string[] blobNames = { "Restore7Day.json", "Restore-Dependency7Day.json"};
            List<string> xValues = new List<string>();
            List<object> yValues = new List<Object>();

            ReportHelpers.GetValuesFromBlobs(blobNames, StorageAccount, ContainerName, out xValues, out yValues);

            List<DotNet.Highcharts.Options.Series> seriesSet = new List<DotNet.Highcharts.Options.Series>();

            seriesSet.Add(new DotNet.Highcharts.Options.Series
            {
                Data = new Data(yValues.ToArray())

            });

            DotNet.Highcharts.Highcharts installChart = ReportHelpers.GetBarChart(seriesSet, xValues, "Restores_Per_NuGet_Version");
            string installChartHtml = installChart.ToHtmlString();
            return installChartHtml;
        }
        #endregion

        #region IISRequests

        private string CreateTableForIISRequestsDistribution()
        {
            return ReportHelpers.CreateTableForIISRequestsDistribution(StorageAccount, ContainerName, DatesInWeek);
        }

        #endregion

        public double Availability
        {
            get
            {
                return CalculateWeeklyUpdateTimeAverage();
            }
        }

        public int Downloads
        {
            get
            {
                int allDownloads;
                allDownloads = GetDownloadNumbersFromBlob("Install7Day.json") + GetDownloadNumbersFromBlob("Install-Dependency7Day.json") +
                                GetDownloadNumbersFromBlob("Update7Day.json") + GetDownloadNumbersFromBlob("Update-Dependency7Day.json") +
                                GetDownloadNumbersFromBlob("Reinstall7Day.json") + GetDownloadNumbersFromBlob("Reinstall-Dependency7Day.json");
                return allDownloads;
             }
        }

        public int Restore
        {
            get
            {
                return GetDownloadNumbersFromBlob("Restore7Day.json") + GetDownloadNumbersFromBlob("Restore-Dependency7Day.json");
            }
        }

        public int SearchQueries
        {
            get
            {
                int avgTime = 0;
                return ReportHelpers.GetQueryNumbers("Search", out avgTime, DatesInWeek, StorageAccount, ContainerName);
            }
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
                return GetWeeklyTupleMetricValues("Uploads", "HourlyReport.json").Item2;
            }
        }

        public int UniqueUploads
        {
            get
            {
                return GetWeeklyTupleMetricValues("UniqueUploads", "HourlyReport.json").Item2;
            }
        }

        public int NewUsers
        {
            get
            {
                return GetWeeklyTupleMetricValues("Users", "HourlyReport.json").Item2;
            }
        }

        public int TrafficPerHour
        {
            get
            {
                return GetWeeklyTupleMetricValues("IISRequests", ".json").Item1;
            }
        }

        public int TrafficMax
        {
            get
            {
                return GetWeeklyTupleMetricValues("IISRequests", ".json").Item3;
            }
        }

        public int TrafficMin
        {
            get
            {
                return GetWeeklyTupleMetricValues("IISRequests", ".json").Item4;
            }
        }

        public int RequestPerHour
        {
            get
            {
                return GetWeeklyTupleMetricValues("DBRequests", ".json").Item1;
            }
        }

        public int RequestMax
        {
            get
            {
                return GetWeeklyTupleMetricValues("DBRequests", ".json").Item3;
            }
        }

        public int RequestMin
        {
            get
            {
                return GetWeeklyTupleMetricValues("DBRequests", ".json").Item4;
            }
        }

        public int ErrorsPerHour
        {
            get
            {
                return GetWeeklyTupleMetricValues("ErrorRate", ".json").Item1;
            }
        }
        public int ErrorsMax
        {
            get
            {
                return GetWeeklyTupleMetricValues("ErrorRate", ".json").Item3;
            }
        }
        public int ErrorsMin
        {
            get
            {
                return GetWeeklyTupleMetricValues("ErrorRate", ".json").Item4;
            }
        }

        public int IndexLag
        {
            get
            {
                return GetWeeklyTupleMetricValues("IndexingDiffCount", "HourlyReport.json").Item1;
            }
        }

        public int IndexMax
        {
            get
            {
                return GetWeeklyTupleMetricValues("IndexingDiffCount", "HourlyReport.json").Item3;
            }
        }

        public int IndexMin
        {
            get
            {
                return GetWeeklyTupleMetricValues("IndexingDiffCount", "HourlyReport.json").Item4;
            }
        }

        public int InstanceCount
        {
            get
            {
                return GetWeeklyTupleMetricValues("nuget-prod-0-v2galleryInstanceCount", "HourlyReport.json").Item1;
            }
        }

        public int InstanceMax
        {
            get
            {
                return GetWeeklyTupleMetricValues("nuget-prod-0-v2galleryInstanceCount", "HourlyReport.json").Item3;
            }
        }

        public int InstanceMin
        {
            get
            {
                return GetWeeklyTupleMetricValues("nuget-prod-0-v2galleryInstanceCount", "HourlyReport.json").Item4;
            }
        }

        public List<int> OverallWorkerCount
        {
            get
            {
                List<int> counts = new List<int>();
                foreach (string date in DatesInWeek)
                {
                    List<WorkInstanceDetail> details = GetWorkJobDetail(date);
                    counts.Add(details.Count);
                }
                return counts;
            }
        }

        public List<int> SuccessCount
        {
            get
            {
                List<int> successCounts = new List<int>();
                foreach (string date in DatesInWeek)
                {
                    int successCount = GetFailedJobDetails(date).Item1;
                    successCounts.Add(successCount);
                }
                return successCounts;
            }
        }

        public List<string[]> FailedJobNames
        {
            get
            {
                List<string[]> weeklyJobNames = new List<string[]>();
                foreach (string date in DatesInWeek)
                {
                    string[] jobNames = GetFailedJobDetails(date).Item2;
                    weeklyJobNames.Add(jobNames);
                }
                return weeklyJobNames;
            }
        }

        public List<string[]> NotableIssues
        {
            get
            {
                List<string[]> weeklyIssues = new List<string[]>();
                for (int i = 1; i <= 7; i++)
                {
                    string[] issues;
                    if (i != 7)
                    {
                        issues = GetFailedJobDetails(DatesInWeek[i - 1]).Item3;
                    }
                    else
                    {
                        issues = GetFailedJobDetails(DatesInWeek[i - 1], true).Item3;
                    }
                    weeklyIssues.Add(issues);
                }
                return weeklyIssues;
            }
        }

        public override void ExecuteCommand()
        {
            SendEmail();
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
            message.From = new MailAddress(ConfigurationManager.AppSettings["SmtpUserName"], "NuGet Weekly Status Report");
            message.To.Add(new MailAddress(MailRecepientAddress, MailRecepientAddress));
            message.Subject = string.Format("NuGet Gallery Weekly Status Report - for Week of " + DateTime.Today.AddDays(-7).ToShortDateString() + " ~ " + DateTime.Today.AddDays(-1).ToShortDateString());
            message.IsBodyHtml = true;
            //message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(@"<html><body>" + GetMailContent() + "</body></html>", new ContentType("text/html")));
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString( GetMailContent() , new ContentType("text/html")));
                                 
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
            StreamReader sr = new StreamReader(@"ScriptsAndReferences\WeeklyStatusReport.htm");
            string mailBody = sr.ReadToEnd();
            sr.Close();

            mailBody = mailBody.Replace("{availability}", Availability.ToString("f2") + "%");
            mailBody = mailBody.Replace("{downloads}", Downloads.ToString("#,##0"));
            mailBody = mailBody.Replace("{restore}", Restore.ToString("#,##0"));
            mailBody = mailBody.Replace("{searchqueries}", SearchQueries.ToString("#,##0"));
            mailBody = mailBody.Replace("{uniqueuploads}", UniqueUploads.ToString());
            mailBody = mailBody.Replace("{uploads}", Uploads.ToString());
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
            mailBody = mailBody.Replace("{InstallUpdatesRestoresPerNuGetVersion}", InstallUpdatesRestoresByNuGetVersion());
            mailBody = mailBody.Replace("{InstallUpdatesRestoresPerVSVersion}", InstallUpdatesRestoresByVSVersion());
            mailBody = mailBody.Replace("{IISRequestsDistribution}", CreateTableForIISRequestsDistribution());
            mailBody = ReplaceWorkJobDetails(mailBody, DatesInWeek);

            return mailBody;
        }

        private string ReplaceWorkJobDetails(string mailBody, List<string> DatesInWeek)
        {
            for (int i = 1; i <= 7; i++)
            {
                string date = DatesInWeek[i - 1].Substring(0, 2) + "/" + DatesInWeek[i - 1].Substring(2, 2) + "/" + DateTime.Now.Year.ToString();
                string workCount = OverallWorkerCount[i - 1] == 0 ? "N/A" : OverallWorkerCount[i - 1].ToString();
                string successCount = SuccessCount[i - 1] == 0 ? "N/A" : SuccessCount[i - 1].ToString();
                mailBody = mailBody.Replace("{day" + i + "}", date);         
                mailBody = mailBody.Replace("{overallworkercount" + i + "}", workCount);
                mailBody = mailBody.Replace("{successcount" + i + "}", successCount);
                mailBody = mailBody.Replace("{failedjobnames" + i + "}", string.Join(", ", FailedJobNames[i - 1]));
                mailBody = mailBody.Replace("{notableissues" + i + "}", string.Join("<br/>", NotableIssues[i - 1]));
            }

            return mailBody;
        }

        #region Helper Methods

        private Tuple<int, int, int, int> GetWeeklyTupleMetricValues(string blobNamePart1, string blobNamePart2)
        {
            List<int> averages = new List<int>();
            List<int> sums = new List<int>();
            List<int> maxs = new List<int>();
            List<int> mins = new List<int>();
            foreach (string date in DatesInWeek)
            {
                Tuple<int, int, int, int> values = GetTupleMetricValues(blobNamePart1 + date + blobNamePart2);
                averages.Add(values.Item1);
                sums.Add(values.Item2);
                maxs.Add(values.Item3);
                mins.Add(values.Item4);
            }
            double aver = Math.Round(averages.Average());
            return new Tuple<int, int, int, int>(Convert.ToInt32(aver), sums.Sum(), maxs.Max(), mins.Min());
        }

        private Tuple<int, int, int, int> GetTupleMetricValues(string blobName)
        {
            List<int> list = GetMetricValues(blobName);
            int average = 0;
            int sum = 0;
            int maximum = 0;
            int minimum = 0;
            if (list.Count != 0)
            {
                average = list.Sum() / list.Count;
                sum = list.Sum();
                maximum = list.Max();
                minimum = list.Min();
            }
            return new Tuple<int, int, int, int>(average, sum, maximum, minimum);
        }

        private List<int> GetMetricValues(string blobName)
        {
            return GetMetricValuesFromBlob(blobName, ContainerName, 0000, 2300);
        }

        private List<int> GetMetricValuesFromBlob(string blobName, string containerName, int startTime, int endTime)
        {
            Dictionary<string, string> dict =  new Dictionary<string,string>();
            try
            {
                dict = ReportHelpers.GetDictFromBlob(StorageAccount, blobName, containerName);
            }
            catch (Exception) { }
            List<int> values = new List<int>();
            if (dict != null)
            {
                foreach (KeyValuePair<string, string> keyValuePair in dict)
                {
                    int key = Convert.ToInt32(keyValuePair.Key.Replace(":", "").Replace("-", ""));

                    if ((key >= startTime) && (key <= endTime))
                    {
                        values.Add(Convert.ToInt32(keyValuePair.Value));
                    }
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

        private double CalculateWeeklyUpdateTimeAverage()
        {
            List<double> uptimes = new List<double>();
            foreach (string date in DatesInWeek)
            {
                double uptime = CalculateGalleryDailyUpTime(date);
                uptimes.Add(uptime);
            }
            return uptimes.Average();
        }

        private double CalculateGalleryDailyUpTime(string Date)
        {
            // Up time for DC0.feed.raw.packages.list
            double DC0FeedUpTime = GetTupleMetricValues("DC0.-.feed.raw.packages.list" + Date + "outageReport.json").Item1;
            // Up time for Feed.top.30.by.downloads
            double feedTop30DownloadsUpTime = GetTupleMetricValues("feed.top.30.by.downloads" + Date + "outageReport.json").Item1;
            // Up time for Package.restore.download
            double packageRestoreDownloadUpTime = GetTupleMetricValues("package.restore.download" + Date + "outageReport.json").Item1;
            // Up time for Package.restore.lookup
            double packageRestoreLookupUpTime = GetTupleMetricValues("package.restore.lookup" + Date + "outageReport.json").Item1;

            List<double> uptimes = new List<double>() { DC0FeedUpTime, feedTop30DownloadsUpTime, packageRestoreDownloadUpTime, packageRestoreLookupUpTime };
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

        

        private List<WorkInstanceDetail> GetWorkJobDetail(string date)
        {
            List<WorkInstanceDetail> jobDetail = new List<WorkInstanceDetail>();
            try
            {
                var content = ReportHelpers.Load(StorageAccount, "WorkJobDetail" + date + ".json", ContainerName);

                if (content != null)
                {
                    jobDetail = new JavaScriptSerializer().Deserialize<List<WorkInstanceDetail>>(content);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return jobDetail;
        }

        private Tuple<int, string[], string[]> GetFailedJobDetails(string date, bool addFooter = false)
        {
            List<WorkInstanceDetail> jobDetail = GetWorkJobDetail(date);
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
            if (addFooter)
            {
                notableIssues.Add("<br/>For more details, please refer to https://dashboard.nuget.org/WorkJobs/WorkJobs_Detail.");
            }
            return new Tuple<int, string[], string[]>(count, failedJobNames.ToArray(), notableIssues.ToArray());
        }

        
            

        #endregion
    }
}
