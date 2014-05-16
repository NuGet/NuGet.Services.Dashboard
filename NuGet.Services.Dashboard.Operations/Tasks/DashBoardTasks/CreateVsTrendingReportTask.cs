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

        private string[] VsQuery = {"10.0","11.0","12.0"};
        private string[] Operation = { "Install", "(unknown)", "Install-Dependency", "Restore", "Restore-Dependency", "Update", "Update-Dependency" };

//        private const string sqlQueryForOperation = @"SELECT [Operation], sum([DownloadCount]) as download
//                                                      FROM  [dbo].[Dimension_Date] join [dbo].[Fact_Download] 
//                                                      on [dbo].[Fact_Download].[Dimension_Date_Id] = [dbo].[Dimension_Date].[Id]
//                                                      join [dbo].[Dimension_Operation]
//                                                      on [dbo].[Fact_Download].[Dimension_Operation_Id] = [dbo].[Dimension_Operation].[Id]
//                                                      where [dbo].[Dimension_Date].[Date] >= '{0}'
//                                                      group by [Operation]";
        private const string sqlQueryForOperation = @"SELECT sum([DownloadCount]) 
                                                    FROM  [dbo].[Dimension_Date] join [dbo].[Fact_Download] 
                                                    on [dbo].[Fact_Download].[Dimension_Date_Id] = [dbo].[Dimension_Date].[Id]
                                                    join [dbo].[Dimension_UserAgent]
                                                    on [dbo].[Fact_Download].[Dimension_UserAgent_Id] = [dbo].[Dimension_UserAgent].[Id]
                                                    join [dbo].[Dimension_Operation]
                                                    on [dbo].[Fact_Download].[Dimension_Operation_Id] = [dbo].[Dimension_Operation].[Id]
                                                    where [ClientMajorVersion] = 2 and [ClientMinorVersion] = {0} and [Operation] = '{1}' and [dbo].[Dimension_Date].[Date] >= '{2}'";

        private const string sqlQueryForVS = @"SELECT sum([DownloadCount]) 
                                                FROM  [dbo].[Dimension_Date] join [dbo].[Fact_Download] 
                                                on [dbo].[Fact_Download].[Dimension_Date_Id] = [dbo].[Dimension_Date].[Id]
                                                join [dbo].[Dimension_UserAgent]
                                                on [dbo].[Fact_Download].[Dimension_UserAgent_Id] = [dbo].[Dimension_UserAgent].[Id]
                                                where [dbo].[Dimension_Date].[Date] >= '{0}' and [Value] like '% VS %/{1}%'";

        public override void ExecuteCommand()
        {
            CreateReportForVSTask();
            CreateReportForOperationTask();

        }

        private void CreateReportForVSTask()
        {
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

        private void CreateReportForOperationTask()
        {
            DateTime date = DateTime.UtcNow.AddDays(-LastNDays);
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();

                    foreach (string opt in Operation)
                    {
                        List<agentRequest> result = new List<agentRequest>();
                        for (int version = 5; version < 9; version++)
                        {
                            try
                            {
                                var requests = dbExecutor.Query<Int32>(string.Format(sqlQueryForOperation, version, opt, date.ToString("yyyy-MM-dd"))).SingleOrDefault();
                                result.Add(new agentRequest("2." + version.ToString(), requests));
                            }

                            catch
                            {
                                result.Add(new agentRequest("2." + version.ToString(), 0));
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
