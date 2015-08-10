using System;
using System.Configuration;
using System.Security.Principal;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGetDashboard.Utilities
{
    public class TableStorageService
    {
        private static string _connectionString = ConfigurationManager.AppSettings["StorageConnection"];
        private static string _statusPageTable = ConfigurationManager.AppSettings["StorageTableStatusPage"];

        public static bool WriteStatusPageMessage(string environment, DateTime when, string contents, string who)
        {
            try
            {
                var storageAccount = CloudStorageAccount.Parse(_connectionString);
                var tableClient = storageAccount.CreateCloudTableClient();
                var table = tableClient.GetTableReference(_statusPageTable);
                table.CreateIfNotExists();

                var statusPageMessage = new DynamicTableEntity(environment, when.Ticks.ToString());
                statusPageMessage.Properties.Add("When", new EntityProperty(when));
                statusPageMessage.Properties.Add("Who", new EntityProperty(who));
                statusPageMessage.Properties.Add("Environment", new EntityProperty(environment));
                statusPageMessage.Properties.Add("Contents", new EntityProperty(contents));
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