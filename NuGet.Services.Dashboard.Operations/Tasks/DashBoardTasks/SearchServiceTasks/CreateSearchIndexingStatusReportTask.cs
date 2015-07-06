using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGetGallery.Operations.Common;
using AnglicanGeek.DbExecutor;
using System;
using System.Net;
using System.Web.Script.Serialization;
using NuGetGallery;
using NuGetGallery.Infrastructure;
using Elmah;
using NuGet.Services.Dashboard.Common;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    [Command("CreateSearchIndexingStatusReportTask", "Creates report for the delta between lucene index and database", AltName = "csisrt")]
    public class CreateSearchIndexingStatusReportTask : DatabaseAndStorageTask
    {
        [Option("SearchEndPoint", AltName = "se")]
        public string SearchEndPoint { get; set; }

        [Option("SearchAdminUserName", AltName = "sa")]
        public string SearchAdminUserName { get; set; }

        [Option("SearchAdminkey", AltName = "sk")]
        public string SearchAdminKey { get; set; }

        [Option("AllowedLag", AltName = "al")]
        public int AllowedLagInMinutes { get; set; }

        public override void ExecuteCommand()
        {
            AlertThresholds thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));
            int diff = GetTotalPackageCountFromDatabase() - GetTotalPackageCountFromLucene();
            if (diff > thresholdValues.LuceneIndexLagAlertErrorThreshold || diff < -200) //Increasing the value for negative lag due to bug https://github.com/NuGet/NuGetGallery/issues/2328/. TBD : Make the threshold configurable.
            {
                new SendAlertMailTask
                {
                    AlertSubject = "Error: Search Service Alert activated for Lucene index lag",
                    Details = string.Format("Delta between the packages between in database and lucene index is {0}. Error Threshold lag : {1} packages", diff.ToString(), thresholdValues.LuceneIndexLagAlertErrorThreshold),
                    AlertName = "Error: Alert for LuceneIndexLag",
                    Component = "SearchService",
                    Level = "Error"
                }.ExecuteCommand();
            }
            else if (diff > thresholdValues.LuceneIndexLagAlertWarningThreshold)
            {
                new SendAlertMailTask
                {
                    AlertSubject = "Warning: Search Service Alert activated for Lucene index lag",
                    Details = string.Format("Delta between the packages between in database and lucene index is {0}. Warning Threshold lag : {1} packages", diff.ToString(), thresholdValues.LuceneIndexLagAlertWarningThreshold),
                    AlertName = "Warning: Alert for LuceneIndexLag",
                    Component = "SearchService",
                    Level = "Warning"
                }.ExecuteCommand();
            }

            ReportHelpers.AppendDatatoBlob(StorageAccount,  "IndexingDiffCount" + string.Format("{0:MMdd}", DateTime.Now) + "HourlyReport.json", new Tuple<string, string>(string.Format("{0:HH-mm}", DateTime.Now), diff.ToString()), 24 * 12, ContainerName);

            DateTime lastActivityTime = GetLastCreatedOrEditedActivityTimeFromDB();
            DateTime luceneCommitTimeStamp = GetCommitTimeStampFromLucene();
            double lag = lastActivityTime.Subtract(luceneCommitTimeStamp).TotalMinutes;

            if( lag > AllowedLagInMinutes)
            {
                new SendAlertMailTask
                {
                    AlertSubject = "Warning: Lucene index out of date alert",
                    Details = string.Format("Search Index for endpoint {3} last updated {0} minutes back. Last activity (create/edit) in DB is at {1}, but lucene is update @ {2}", lag, lastActivityTime,luceneCommitTimeStamp,SearchEndPoint),
                    AlertName = "Warning: Alert for LuceneIndexLag",
                    Component = "SearchService",
                    Level = "Error"
                }.ExecuteCommand();
            }

            ReportHelpers.AppendDatatoBlob(StorageAccount, "IndexingLagCount" + string.Format("{0:MMdd}", DateTime.Now) + "HourlyReport.json", new Tuple<string, string>(string.Format("{0:HH-mm}", DateTime.Now), lag.ToString()), 24 * 12, ContainerName);
        }

        private DateTime GetLastCreatedOrEditedActivityTimeFromDB()
        {
            string sqlLastUpdated = "select Top(1) [LastUpdated] from [dbo].[Packages] order by [LastUpdated] desc";
            SqlConnection connection = new SqlConnection(ConnectionString.ConnectionString);
            connection.Open();
            SqlCommand command = new SqlCommand(sqlLastUpdated, connection);
            SqlDataReader reader = command.ExecuteReader(CommandBehavior.CloseConnection);
            if (reader != null)
            {
                while (reader.Read())
                {
                    DateTime dbLastUpdatedTimeStamp = Convert.ToDateTime(reader["LastUpdated"]);
                    return dbLastUpdatedTimeStamp;
                }
            }
            return DateTime.MinValue;
        }
        private int GetTotalPackageCountFromDatabase()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    var connectionCount = dbExecutor.Query<Int32>("select count(*) from dbo.Packages").SingleOrDefault();
                    return connectionCount;
                }
            } 
        }

        public int GetTotalPackageCountFromLucene()
        {
            NetworkCredential nc = new NetworkCredential(SearchAdminUserName, SearchAdminKey);
            WebRequest request = WebRequest.Create(SearchEndPoint); 
            request.Credentials = nc;
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());
                int count = (int)objects["NumDocs"];
                return count;
            }
        }

        public DateTime GetCommitTimeStampFromLucene()
        {
            NetworkCredential nc = new NetworkCredential(SearchAdminUserName, SearchAdminKey);
            WebRequest request = WebRequest.Create(SearchEndPoint);
            request.Credentials = nc;
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());
                DateTime count = Convert.ToDateTime(objects["CommitUserData"]["commit-time-stamp"]);
                return count;
            }
        }   

    }
}
