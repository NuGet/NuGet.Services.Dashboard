using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using NuGet.Services.Dashboard.Common;
using NuGetDashboard.Utilities;

namespace NuGetDashboard.Controllers.LiveSiteMonitoring
{
    public class WorkJobController : Controller
    {
        public List<WorkJobInstanceDetails> instanceDetails;
        public ActionResult Index()
        {
            instanceDetails = new JavaScriptSerializer().Deserialize<List<WorkJobInstanceDetails>>(BlobStorageService.Load("Configuration.WorkJobInstances.json"));
            List<Tuple<string, string,string>> jobResults = new List<Tuple<string, string,string>>();            
            foreach(WorkJobInstanceDetails jobDetails in instanceDetails)
            {
               WorkJobInvocation job;
               bool success =  IsLatestSuccessful(jobDetails,out job);
               string lastQueueed = string.Empty;             
                if(job != null)
                {
                    lastQueueed = string.Format("{0} mins ago",Convert.ToInt32(DateTime.Now.Subtract(job.queuedAt).TotalMinutes));
                }
                else
                {
                    lastQueueed = "N/A";
                }
               jobResults.Add(new Tuple<string,string,string>(jobDetails.JobInstanceName,success.ToString(),lastQueueed));
            }

            return PartialView("~/Views/WorkJobs/WorkJobs_Index.cshtml",jobResults);
        }
        private static bool IsLatestSuccessful(WorkJobInstanceDetails jobDetails,out WorkJobInvocation job)
        {          
            NetworkCredential nc = new NetworkCredential(ConfigurationManager.AppSettings["WorkServiceUserName"], ConfigurationManager.AppSettings["WorkServiceAdminKey"]);
            WebRequest request = WebRequest.Create(string.Format("https://api-work-0.nuget.org/work/invocations/instances/{0}?limit=2", jobDetails.JobInstanceName)); //get last 2 instances.
            request.Credentials = nc;
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<List<WorkJobInvocation>>(reader.ReadToEnd());              
                //in the last two instances atleast one should be in Completed state (the latest could be in progress and the last but one could be in completed.)
                if(objects.Any((item => item.status.Equals("Executed") && item.result.Equals("Completed"))))
                {
                    job = objects.Where(item => item.status.Equals("Executed") && item.result.Equals("Completed")).ToList().FirstOrDefault(); 
                    if (DateTime.Now.Subtract(job.queuedAt) > new TimeSpan(0, 2* jobDetails.FrequencyInMinutes,0)) //the time interval from the latest successful job instance cannot be more than twice the frequency.
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    job = objects.FirstOrDefault();
                    return false;
                }       
                
            }
           
        }
        

    }
}
