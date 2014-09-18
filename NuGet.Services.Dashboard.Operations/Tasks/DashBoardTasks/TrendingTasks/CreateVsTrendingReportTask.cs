using AnglicanGeek.DbExecutor;
using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace NuGetGallery.Operations
{
    [Command("CreateVsTrendingReportTask","create vs trending report task", AltName = "cvtrt")]
    public class CreateVsTrendingReportTask : DatabaseAndStorageTask
    {
        [Option("LastNDays", AltName = "n")]
        public int LastNDays { get; set; }
        private const string sqlQueryForOperation = @"SELECT sum([DownloadCount]) 
                                                    FROM  [dbo].[Dimension_Date] join [dbo].[Fact_Download] 
                                                    on [dbo].[Fact_Download].[Dimension_Date_Id] = [dbo].[Dimension_Date].[Id]
                                                    join [dbo].[Dimension_UserAgent]
                                                    on [dbo].[Fact_Download].[Dimension_UserAgent_Id] = [dbo].[Dimension_UserAgent].[Id]
                                                    join [dbo].[Dimension_Operation]
                                                    on [dbo].[Fact_Download].[Dimension_Operation_Id] = [dbo].[Dimension_Operation].[Id]
                                                    where [ClientMajorVersion] = {0} and [ClientMinorVersion] = {1} and [Operation] = '{2}' and [dbo].[Dimension_Date].[Date] >= '{3}'";

        private const string sqlQueryForVS = @"SELECT sum([DownloadCount]) 
                                                FROM  [dbo].[Dimension_Date] join [dbo].[Fact_Download] 
                                                on [dbo].[Fact_Download].[Dimension_Date_Id] = [dbo].[Dimension_Date].[Id]
                                                join [dbo].[Dimension_UserAgent]
                                                on [dbo].[Fact_Download].[Dimension_UserAgent_Id] = [dbo].[Dimension_UserAgent].[Id]
                                                where [dbo].[Dimension_Date].[Date] >= '{0}' and [Value] like '% VS %/{1}%'
                                                and [dbo].[Fact_Download].[Dimension_Operation_Id] IN (2, 6, 5, 9, 3, 7)";

        private const string sqlQueryForVSRestore = @"SELECT sum([DownloadCount]) 
                                                FROM  [dbo].[Dimension_Date] join [dbo].[Fact_Download] 
                                                on [dbo].[Fact_Download].[Dimension_Date_Id] = [dbo].[Dimension_Date].[Id]
                                                join [dbo].[Dimension_UserAgent]
                                                on [dbo].[Fact_Download].[Dimension_UserAgent_Id] = [dbo].[Dimension_UserAgent].[Id]
                                                where [dbo].[Dimension_Date].[Date] >= '{0}' and [Value] like '% VS %/{1}%'
                                                and [dbo].[Fact_Download].[Dimension_Operation_Id] IN (4, 8)";

        public override void ExecuteCommand()
        {
            CreateReportForVSTask();
            CreateRestoreReportForVSTask();
            CreateReportForOperationTask();

        }

        private void CreateReportForVSTask()
        {
            string[] VsQuery = new JavaScriptSerializer().Deserialize<string[]>(ReportHelpers.Load(StorageAccount, "VsVersion.json", ContainerName));
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    DateTime date = DateTime.UtcNow.AddDays(-LastNDays);
                    List<VsRequest> requests = new List<VsRequest>();
                    foreach (string each in VsQuery)
                    {
                        try
                        {
                            var request = dbExecutor.Query<Int32>(string.Format(sqlQueryForVS, date.ToString("yyyy-MM-dd"), each)).SingleOrDefault();
                            requests.Add(new VsRequest("VS" + each, request.ToString()));
                        }

                        catch
                        {
                            requests.Add(new VsRequest("VS" + each, "0"));
                        }

                    }
                    var json = new JavaScriptSerializer().Serialize(requests);
                    ReportHelpers.CreateBlob(StorageAccount, "VsTrend" + LastNDays.ToString() + "Day.json", ContainerName, "application/json", ReportHelpers.ToStream(json));



                }
            }
        }

        private void CreateRestoreReportForVSTask()
        {
            string[] VsQuery = new JavaScriptSerializer().Deserialize<string[]>(ReportHelpers.Load(StorageAccount, "VsVersion.json", ContainerName));
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    DateTime date = DateTime.UtcNow.AddDays(-LastNDays);
                    List<VsRequest> requests = new List<VsRequest>();
                    foreach (string each in VsQuery)
                    {
                        try
                        {
                            var request = dbExecutor.Query<Int32>(string.Format(sqlQueryForVSRestore, date.ToString("yyyy-MM-dd"), each)).SingleOrDefault();
                            requests.Add(new VsRequest("VS" + each, request.ToString()));
                        }

                        catch
                        {
                            requests.Add(new VsRequest("VS" + each, "0"));
                        }

                    }
                    var json = new JavaScriptSerializer().Serialize(requests);
                    ReportHelpers.CreateBlob(StorageAccount, "VsRestoreTrend" + LastNDays.ToString() + "Day.json", ContainerName, "application/json", ReportHelpers.ToStream(json));



                }
            }
        }

        private void CreateReportForOperationTask()
        {
            DateTime date = DateTime.UtcNow.AddDays(-LastNDays);
            string[] agentVersion = new JavaScriptSerializer().Deserialize<string[]>(ReportHelpers.Load(StorageAccount, "agentVersion.json", ContainerName));
            string[] Operation = new JavaScriptSerializer().Deserialize<string[]>(ReportHelpers.Load(StorageAccount, "OperationType.json", ContainerName));
           

            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();

                    foreach (string opt in Operation)
                    {
                        List<agentRequest> result = new List<agentRequest>();
                        foreach (string version in agentVersion)
                        {
                       
                            string major = version[0].ToString();
                            string minor = version[2].ToString();
                            try
                            {
                                var requests = dbExecutor.Query<Int32>(string.Format(sqlQueryForOperation, major, minor, opt, date.ToString("yyyy-MM-dd"))).SingleOrDefault();
                                result.Add(new agentRequest(version, requests));
                            }

                            catch
                            {
                                result.Add(new agentRequest(version, 0));
                            }
                        }


                        var json = new JavaScriptSerializer().Serialize(result);
                        ReportHelpers.CreateBlob(StorageAccount, opt + LastNDays.ToString() + "Day.json", ContainerName, "application/json", ReportHelpers.ToStream(json));
                    }

                }
            }
        }
    }
}
