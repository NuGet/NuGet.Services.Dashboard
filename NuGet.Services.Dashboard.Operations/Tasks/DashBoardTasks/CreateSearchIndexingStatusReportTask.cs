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
        public override void ExecuteCommand()
        {
            int diff = GetTotalPackageCountFromDatabase() - GetTotalPackageCountFromLucene();
            ReportHelpers.AppendDatatoBlob(StorageAccount,  "IndexingDiffCount" + string.Format("{0:MMdd}", DateTime.Now) + "HourlyReport.json", new Tuple<string, string>(string.Format("{0:HH-mm}", DateTime.Now), diff.ToString()), 24, ContainerName);
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

    }
}
