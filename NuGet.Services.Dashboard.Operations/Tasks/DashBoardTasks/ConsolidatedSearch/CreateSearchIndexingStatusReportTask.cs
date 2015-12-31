using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Web.Script.Serialization;
using AnglicanGeek.DbExecutor;
using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks.SearchServiceTasks.ConsolidatedSearch
{
    [Command("CreateConsolidatedSearchIndexingStatusReportTask", "Creates report for the delta between consolidated Lucene index and database", AltName = "ccsisrt")]
    public class CreateConsolidatedSearchIndexingStatusReportTask 
        : DatabaseAndStorageTask
    {
        [Option("SearchEndPoint", AltName = "se")]
        public string SearchEndPoint { get; set; }

        [Option("SearchAdminUserName", AltName = "sa")]
        public string SearchAdminUserName { get; set; }

        [Option("SearchAdminkey", AltName = "sk")]
        public string SearchAdminKey { get; set; }

        [Option("AllowedLagSev1", AltName = "alsev1")]
        public int AllowedLagInMinutesSev1 { get; set; }

        [Option("AllowedLagSev2", AltName = "alsev2")]
        public int AllowedLagInMinutesSev2 { get; set; }

        public override void ExecuteCommand()
        {
            var thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(
                ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));

            var difference = GetTotalPackageCountFromDatabase() - GetTotalPackageCountFromLucene();

            if (difference > thresholdValues.LuceneIndexLagAlertErrorThreshold)
            {
                new SendAlertMailTask
                {
                    AlertSubject = string.Format("Consolidated Lucene index for {0} lagging behind database by {1} packages", SearchEndPoint, difference),
                    Details = string.Format("Delta between the packages between in database and Lucene index is {0}. Allowed Threshold lag : {1} packages", difference, thresholdValues.LuceneIndexLagAlertErrorThreshold),
                    AlertName = "Error: Alert for LuceneIndexLag",
                    Component = "SearchService",
                    Level = "Error"
                }.ExecuteCommand();
            }
            else if (difference > thresholdValues.LuceneIndexLagAlertWarningThreshold)
            {
                new SendAlertMailTask
                {
                    AlertSubject = "Warning: Search Service Alert activated for Consolidated Lucene index lag",
                    Details = string.Format("Delta between the packages between in database and Lucene index is {0}. Warning Threshold lag : {1} packages", difference, thresholdValues.LuceneIndexLagAlertWarningThreshold),
                    AlertName = "Warning: Alert for LuceneIndexLag",
                    Component = "SearchService",
                    Level = "Warning"
                }.ExecuteCommand();
            }

            ReportHelpers.AppendDatatoBlob(StorageAccount,  "ConsolidatedIndexingDiffCount" + string.Format("{0:MMdd}", DateTime.Now) + "HourlyReport.json", 
                new Tuple<string, string>(string.Format("{0:HH-mm}", DateTime.Now), difference.ToString()), 24 * 12, ContainerName);

            var lastActivityTime = GetLastCreatedOrEditedActivityTimeFromDatabase();
            var luceneCommitTimeStamp = GetCommitTimeStampFromLucene();
            var indexLagInMinutes = lastActivityTime.Subtract(luceneCommitTimeStamp).TotalMinutes;

            if (indexLagInMinutes > AllowedLagInMinutesSev1)
            {
                new SendAlertMailTask
                {
                    AlertSubject = string.Format("Error: Consolidated Lucene index for {0} out of date by {1} minutes", SearchEndPoint, Math.Round(indexLagInMinutes,2)),
                    Details = string.Format("Search Index for endpoint {3} last updated {0} minutes back. Last activity (create/edit) in DB is at {1}, but Lucene is updated @ {2}", Math.Round(indexLagInMinutes, 2), lastActivityTime,luceneCommitTimeStamp,SearchEndPoint),
                    AlertName = "Error: Alert for LuceneIndexLag",
                    Component = "SearchService",
                    Level = "Error",
                    EscPolicy = "Sev1"
                }.ExecuteCommand();
            }

            else if (indexLagInMinutes > AllowedLagInMinutesSev2)
            {
                new SendAlertMailTask
                {
                    AlertSubject = string.Format("Warning: Consolidated Lucene index for {0} out of date  by {1} minutes", SearchEndPoint, Math.Round(indexLagInMinutes, 2)),
                    Details = string.Format("Search Index for endpoint {3} last updated {0} minutes back. Last activity (create/edit) in DB is at {1}, but Lucene is updated @ {2}", Math.Round(indexLagInMinutes, 2), lastActivityTime, luceneCommitTimeStamp, SearchEndPoint),
                    AlertName = "Warning: Alert for LuceneIndexLag",
                    Component = "SearchService",
                    Level = "Error"
                }.ExecuteCommand();
            }

            ReportHelpers.AppendDatatoBlob(StorageAccount, "ConsolidatedIndexingLagCount" + string.Format("{0:MMdd}", DateTime.Now) + "HourlyReport.json", 
                new Tuple<string, string>(string.Format("{0:HH-mm}", DateTime.Now), indexLagInMinutes.ToString(CultureInfo.InvariantCulture)), 24 * 12, ContainerName);
        }

        private DateTime GetLastCreatedOrEditedActivityTimeFromDatabase()
        {
            string sqlLastUpdated = "SELECT TOP(1) [LastUpdated] FROM [dbo].[Packages] ORDER BY [LastUpdated] DESC";

            using (var connection = new SqlConnection(ConnectionString.ConnectionString))
            {
                connection.Open();

                var command = new SqlCommand(sqlLastUpdated, connection);
                var reader = command.ExecuteReader(CommandBehavior.CloseConnection);

                while (reader.Read())
                {
                    var dbLastUpdatedTimeStamp = Convert.ToDateTime(reader["LastUpdated"]);
                    return dbLastUpdatedTimeStamp;
                }
            }

            return DateTime.MinValue;
        }

        private int GetTotalPackageCountFromDatabase()
        {
            using (var connection = new SqlConnection(ConnectionString.ConnectionString))
            {
                connection.Open();

                using (var executor = new SqlExecutor(connection))
                {

                    var connectionCount = executor.Query<Int32>("SELECT COUNT(*) FROM [dbo].[Packages]").SingleOrDefault();
                    return connectionCount;
                }
            } 
        }

        public int GetTotalPackageCountFromLucene()
        {
            var credential = new NetworkCredential(SearchAdminUserName, SearchAdminKey);
            var request = WebRequest.Create(SearchEndPoint); 
            request.Credentials = credential;
            request.PreAuthenticate = true;
            request.Method = "GET";

            using (var response = request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var js = new JavaScriptSerializer();

                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());

                var count = (int)objects["NumDocs"];
                return count;
            }
        }

        public DateTime GetCommitTimeStampFromLucene()
        {
            var time = DateTime.MinValue;

            int retries = 5;
            while (retries-- > 0)
            {
                try
                {
                    var credential = new NetworkCredential(SearchAdminUserName, SearchAdminKey);
                    var request = WebRequest.Create(SearchEndPoint);
                    request.Credentials = credential;
                    request.PreAuthenticate = true;
                    request.Method = "GET";

                    using (var response = request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var js = new JavaScriptSerializer();

                        var objects = js.Deserialize<dynamic>(reader.ReadToEnd());

                        time = Convert.ToDateTime(objects["CommitUserData"]["commit-time-stamp"]);
                        return time;
                    }
                }
                catch (Exception)
                {
                    // If more retry attempts are possible just log and retry.Else throw the exception so that we will get alerted.
                    if (retries > 0)
                    {
                        Console.WriteLine(
                            "Unable to get commit time stamp from {0} endpoint to check for Lucene index lag",
                            SearchEndPoint);
                    }
                    else
                    {
                        throw new InvalidDataException(
                            string.Format(
                                "Unable to get commit time stamp from {0} endpoint to check for Lucene index lag",
                                SearchEndPoint));
                    }
                }
            }
            return time;
        }   

    }
}
