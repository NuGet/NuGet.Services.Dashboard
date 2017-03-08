using System;
using System.Web.Script.Serialization;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using NuGetGallery.Operations.Common;


namespace NuGetGallery.Operations
{
    [Command("CreatePerfUsageReportTask", "Create performance counter usage report task", AltName = "cpurt")]
    public class CreateWADPerformanceDiagnosticsReportTask : StorageTask
    {
        [Option("PerfCounterTableStorageAccount", AltName = "table")]
        public string PerfCounterTableStorageAccount { get; set; }

        [Option("PerfCounterName", AltName = "counter")]
        public string PerfCounterName { get; set; }

        [Option("frequencyInMin", AltName = "f")]
        public int frequencyInMin { get; set; }

        [Option("ServiceName", AltName = "name")]
        public string ServiceName { get; set; }

        public override void ExecuteCommand()
        {
            string DeployId = new JavaScriptSerializer().Deserialize<string>(ReportHelpers.Load(StorageAccount, "DeploymentId_" + ServiceName + ".json", ContainerName));
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(PerfCounterTableStorageAccount);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference("WAD"+DeployId+"PT5MRTable");
            int count = 0;
            double sum = 0;

            TableQuery<dataEntity> rangeQuery = new TableQuery<dataEntity>().Where(TableQuery.CombineFilters(
                                                TableQuery.GenerateFilterConditionForDate("Timestamp", QueryComparisons.GreaterThan, DateTime.UtcNow.AddMinutes(-frequencyInMin)),
                                                TableOperators.And,
                                                TableQuery.GenerateFilterCondition("CounterName", QueryComparisons.Equal, PerfCounterName)));

            foreach (dataEntity entity in table.ExecuteQuery(rangeQuery))
            {
                count++;
                sum += entity.Total / entity.Count;
            }

            ReportHelpers.AppendDatatoBlob(StorageAccount, ServiceName + PerfCounterName + string.Format("{0:MMdd}", DateTime.Now) + ".json", new Tuple<string, string>(String.Format("{0:HH:mm}", DateTime.Now), (sum/count).ToString("F")), 24*60 / frequencyInMin, ContainerName);

        }

        private class dataEntity : TableEntity
        {
            public int Count { get; set; }
            public double Total { get; set; }

            public string CounterName { get; set; }

            public string Role { get; set; }

            public string DepolymentId { get; set; }

            public dataEntity() { }

            public dataEntity(string PartitionKey, string RowKey)
            {
                this.PartitionKey = PartitionKey;
                this.RowKey = RowKey;
            }
        }



    }
}


