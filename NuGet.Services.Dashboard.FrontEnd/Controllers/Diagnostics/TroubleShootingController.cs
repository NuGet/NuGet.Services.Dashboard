using Newtonsoft.Json.Linq;
using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetDashboard.Controllers.Diagnostics
{
    /// <summary>
    /// Gets troubleshooting details like ElmahLog detailed summary and DB detailed summary.
    /// </summary>
    public class TroubleShootingController : Controller
    {      
        public ActionResult Index()
        {
            return View();
        }
        public ActionResult Details()
        {
            return PartialView("~/Views/TroubleShooting/TroubleShooting_Details.cshtml");
        }
        public ActionResult DBRequestsSummary()
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            return PartialView("~/Views/TroubleShooting/TroubleShooting_DBRequestsSummary.cshtml", BlobStorageService.GetDictFromBlob("DbRequesstSummaryReport.json"));
        }
        public ActionResult ElmahErrorSummary(string hour)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            List<string> errors = new List<string>();
            BlobStorageService.GetJsonDataFromBlob("ElmahErrorsDetailed" + hour + "hours.json", out errors);
            Dictionary<string, string> criticalErrors = BlobStorageService.GetDictFromBlob("Configuration.ElmahCriticalErrors.json");

            List<Tuple<string, string, string, string,int>> errorrows = new List<Tuple<string, string, string, string,int>>();
            foreach(string error in errors)
            {  
                string[] values = error.Split(new String[] { @"""" + "," },StringSplitOptions.RemoveEmptyEntries);
                int severity = 1;
                if(criticalErrors.ContainsKey(values[0].Replace(@"["+ @"""","")))
                {
                    severity = 0;
                }

                errorrows.Add(new Tuple<string, string, string, string,int>(values[0].Replace(@"["+ @"""",""), values[1].Replace(@"["+ @"""",""), values[2].Replace(@"["+ @"""",""), values[3].Replace(@"["+ @"""",""),severity));
            }

            return PartialView("~/Views/TroubleShooting/TroubleShooting_ElmahErrorSummary.cshtml", errorrows);
        }      

    }
}
