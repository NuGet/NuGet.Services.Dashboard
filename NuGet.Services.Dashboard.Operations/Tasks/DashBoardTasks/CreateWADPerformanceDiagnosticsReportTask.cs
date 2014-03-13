//using System.Collections.Generic;
//using System.Linq;
//using System.Data;
//using System.Data.SqlClient;
//using System.IO;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.WindowsAzure.Storage;
//using Microsoft.WindowsAzure.Storage.Blob;
//using Newtonsoft.Json.Linq;
//using NuGetGallery.Operations.Common;
//using AnglicanGeek.DbExecutor;
//using System;
//using System.Net;
//using System.Web.Script.Serialization;
//using NuGetGallery;
//using NuGetGallery.Infrastructure;
//using Elmah;
//using System.IO;
//using Microsoft.WindowsAzure.Storage;
//using Microsoft.WindowsAzure.Storage.Table;


//namespace NuGetGallery.Operations
//{
//    [Command("CreateElmahErrorOverviewReportTask", "Creates trending report for Elmah error count", AltName = "ceeort")]
//    public class CreateWADPerformanceDiagnosticsReportTask : StorageTask
//    {
//        [Option("WADTableStorageAccount", AltName = "wad")]
//        public CloudStorageAccount WADTableStoragAccount { get; set; }

//        [Option("DeploymentID", AltName = "di")]
//        public CloudStorageAccount DeploymentID { get; set; }

//        public override void ExecuteCommand()
//        {
//            PerformanceDataContext context = new PerformanceDataContext(
//          accountStorage.TableEndpoint.ToString(), accountStorage.Credentials);
//            var data = context.PerfData;
//            CloudTableQuery<PerformanceData> query = null;
//            query = (from d in data
//                     where d.PartitionKey.CompareTo("0" + startPeriod.Ticks) >= 0
//                                            && d.PartitionKey.CompareTo
//                        ("0" + endPeriod.Ticks) <= 0
//                                             && d.CounterName == counterFullName
//                                                 && d.EventTickCount >= startPeriod.Ticks
//                                                     && d.EventTickCount <= endPeriod.Ticks
//                                                          && d.DeploymentId == deploymentid
//                                                             && d.Role == roleName
//                                                                 && d.RoleInstance ==
//                                roleInstanceName
//                     select d).AsTableServiceQuery<PerformanceData>();
//            List<PerformanceData> selectedData = new List<PerformanceData>();
//            try
//            {
//                selectedData = query.Execute().ToList<PerformanceData>();
//            }
//            catch
//            {
//            }
//            return selectedData;

//        }



//    }
//}


