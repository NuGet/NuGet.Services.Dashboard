using System;
using System.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGetDashboard.Utilities
{
    public class TableStorageService
    {
        private static readonly string ConnectionString = ConfigurationManager.AppSettings["StorageConnection"];
        private static readonly string StatusPageTable = ConfigurationManager.AppSettings["StorageTableStatusPage"];

        public static bool WriteStatusPageMessage(string environment, DateTime when, string contents, string statusOverride, string who)
        {
            try
            {
                var storageAccount = CloudStorageAccount.Parse(ConnectionString);
                var tableClient = storageAccount.CreateCloudTableClient();
                var table = tableClient.GetTableReference(StatusPageTable);
                table.CreateIfNotExists();

                var statusPageMessage = new DynamicTableEntity(environment, when.Ticks.ToString());
                statusPageMessage.Properties.Add("When", new EntityProperty(when));
                statusPageMessage.Properties.Add("Who", new EntityProperty(who));
                statusPageMessage.Properties.Add("Environment", new EntityProperty(environment));
                statusPageMessage.Properties.Add("Contents", new EntityProperty(contents));
                statusPageMessage.Properties.Add("StatusOverride", new EntityProperty(statusOverride));
                table.Execute(TableOperation.Insert(statusPageMessage));

                return true;
            }
            catch (StorageException)
            {
                return false;
            }
        }
    }
}