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
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.Common;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    [Command("createsupportrequestreporttask", "Creates report for the weekly average pingdom values", AltName = "csrr")]
    public class CreateSupportRequestReportTask : StorageTask
    {
        [Option("TFSUserName", AltName = "user")]
        public string UserName { get; set; }

        [Option("TFSpassword", AltName = "password")]
        public string Password { get; set; }

        [Option("TFSUrl", AltName = "url")]
        public string HostUrl { get; set; }

        [Option("TFSProject", AltName = "project")]
        public string ProjectName { get; set; }


        
        //private NetworkCredential creds;
        private TfsTeamProjectCollection tfs;
        //Get data store that contains all workitems on a particular server
        private WorkItemStore store;
        //Get particular Team Project
        private Project project;

        private string[] supportRequestCategories = new string[] {"Private/confidential data","Other","Wrong version","Not intended to be published","Malicious code"};
             
       
        public override void ExecuteCommand()
        {
            TfsClientCredentials cred = new TfsClientCredentials(new WindowsCredential(new NetworkCredential(UserName, Password)), true);
            cred.AllowInteractive = true;
            tfs = new TfsTeamProjectCollection(TfsTeamProjectCollection.GetFullyQualifiedUriForName(HostUrl), cred);
            //Get data store that contains all workitems on a particular server
            store = tfs.GetService<WorkItemStore>();
            //Get particular Team Project
            project = store.Projects[ProjectName];
            Console.WriteLine("Connected to project: " + project.Name);
            CreatereportForSupportRequestsByCategory();
            CreatereportForSupportRequestSummary();
        }


        private WorkItemCollection GetWorkItemsByState(string state="New")
        {
            WorkItemType workItemType = project.WorkItemTypes["Bug"];
            WorkItemCollection collection = store.Query("SELECT [System.Id],[System.Title],[System.State],[System.CreatedDate] FROM WorkItems WHERE [System.State] = '" + state + "'");
            return collection;
        }

        private int GetWorkItemsByTitle(string searchText="Reason: Other", string noOfDays="30")
        {
            WorkItemType workItemType = project.WorkItemTypes["Bug"];
            WorkItemCollection collection = store.Query("SELECT [System.Id],[System.Title],[System.State],[System.CreatedDate] FROM WorkItems WHERE [System.Title] CONTAINS '" + searchText + "'" + "AND [System.CreatedDate] >= " + "@" + "Today-" + noOfDays);
            return collection.Count;
        }

        private int GetWorkItemsForLastNDays(string noOfDays="30")
        {
            WorkItemType workItemType = project.WorkItemTypes["Bug"];
            WorkItemCollection collection = store.Query("SELECT [System.Id],[System.Title],[System.State],[System.CreatedDate] FROM WorkItems WHERE [System.CreatedDate] >= " + "@" + "Today-" + noOfDays );
            return collection.Count;
        }

        private int GetResolutionRateForLastNDays(string noOfDays = "30")
        {
            WorkItemType workItemType = project.WorkItemTypes["Bug"];
            WorkItemCollection collection = store.Query("SELECT [System.Id],[System.Title],[System.State],[System.CreatedDate] FROM WorkItems WHERE [System.CreatedDate] >= " + "@" + "Today-" + noOfDays);

            int totalativeDays = 0;
            foreach (WorkItem item in collection)
            {
                if (item.State == "Done")
                {
                    totalativeDays += item.ChangedDate.Subtract(item.CreatedDate).Days;
                }
                else
                    totalativeDays += DateTime.Now.Subtract(item.CreatedDate).Days;
            }

            return (totalativeDays / collection.Count);
        }

        private void CreatereportForSupportRequestsByCategory()
        {
            List<Tuple<string, string>> summary = new List<Tuple<string, string>>();
            foreach (string category in supportRequestCategories)
            {
                summary.Add(new Tuple<string,string>(category, GetWorkItemsByTitle(category).ToString()));
            }

            JArray reportObject = ReportHelpers.GetJson(summary);
            ReportHelpers.CreateBlob(StorageAccount, "SupportRequestsByCategoryReport.json", "dashboard", "application/json", ReportHelpers.ToStream(reportObject));
        }

        private void CreatereportForSupportRequestSummary()
        {
            List<Tuple<string, string>> summary = new List<Tuple<string, string>>();
            summary.Add(new Tuple<string, string>("No. of open requests ", GetWorkItemsForLastNDays("30").ToString()));
            summary.Add(new Tuple<string,string>("No. of requests in last 7 days",GetWorkItemsForLastNDays("7").ToString()));
            summary.Add(new Tuple<string, string>("Average reslution time for last 7 days", GetResolutionRateForLastNDays("7").ToString()));
            summary.Add(new Tuple<string,string>("No. of requests in last 30 days",GetWorkItemsForLastNDays("30").ToString()));
            summary.Add(new Tuple<string, string>("Average reslution time for last 30 days", GetResolutionRateForLastNDays("30").ToString()));            
            JArray reportObject = ReportHelpers.GetJson(summary);
            ReportHelpers.CreateBlob(StorageAccount, "SupportRequestSummaryReport.json", "dashboard", "application/json", ReportHelpers.ToStream(reportObject));
        }                    

    }
}
