using Newtonsoft.Json.Linq;
using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NuGet.Services.Dashboard.Common;
using Newtonsoft.Json.Linq;
using System.Web.Script.Serialization;

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
        public ActionResult DBRequestsSummary(string hour)
        {  
            var listOfEvents = new JavaScriptSerializer().Deserialize<List<DatabaseEvent>>(BlobStorageService.Load("DBDetailed" + hour + "Hour.json"));

            //Dictionary<string, string> dict = new Dictionary<string, string>();
            return PartialView("~/Views/TroubleShooting/TroubleShooting_DBRequestsSummary.cshtml", listOfEvents);
        }
        public ActionResult ElmahErrorSummary(string hour)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            List<string> errors = new List<string>();
            BlobStorageService.GetJsonDataFromBlob("ElmahErrorsDetailed" + hour + "hours.json", out errors);
            Dictionary<string, string> criticalErrors = BlobStorageService.GetDictFromBlob("Configuration.ElmahCriticalErrors.json");

            List<Tuple<string, string, string, string>> errorrows = new List<Tuple<string, string, string, string>>();
            foreach (string error in errors)
            {
                string[] values = error.Split(new String[] { @"""" + "," }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = values[i].Replace("[" , "");
                    values[i] = values[i].Replace(@"""", "");
                    values[i] = values[i].Trim();
                }
                string severity = "1";
                if (criticalErrors.Keys.Any(item => values[0].Contains(item)))
                {
                    severity = "0";
                }
                errorrows.Add(new Tuple<string, string, string, string>(values[0], values[1], values[2], severity));
            }
            return PartialView("~/Views/TroubleShooting/TroubleShooting_ElmahErrorSummary.cshtml", errorrows);

        }
    }
}
