using Newtonsoft.Json.Linq;
using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NuGet.Services.Dashboard.Common;
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
        /// <summary>
        /// Returns the data for DB Troubleshooting for the given hour.
        /// </summary>
        /// <param name="hour"></param>
        /// <returns></returns>
        public ActionResult DBEventsSummary(string hour)
        {
            var content = BlobStorageService.Load("DBDetailed" + hour + "Hour.json");
            List<DatabaseEvent> listOfEvents = new List<DatabaseEvent>();
            if (content != null)
            {
                listOfEvents = new JavaScriptSerializer().Deserialize<List<DatabaseEvent>>(content);
            }
            return PartialView("~/Views/TroubleShooting/TroubleShooting_DBEventsSummary.cshtml", listOfEvents);
        }

        /// <summary>
        /// Returns the active requests taken during the last snapshot
        /// </summary>
        /// <param name="hour"></param>
        /// <returns></returns>
        public ActionResult DBRequestsSummary()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("DBRequestDetails" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()) + ".json");
            List<DatabaseRequest> listOfRequests = new List<DatabaseRequest>();
            if (dict != null && dict.Count > 0)
            {
                listOfRequests = new JavaScriptSerializer().Deserialize<List<DatabaseRequest>>(dict.Values.ElementAt(dict.Count - 1));
            }
            return PartialView("~/Views/TroubleShooting/TroubleShooting_DBRequestsSummary.cshtml", listOfRequests);
        }
        /// <summary>
        /// Returns the detailed report on Elmah for the past N hours.
        /// </summary>
        /// <param name="hour"></param>
        /// <returns></returns>
        public ActionResult ElmahErrorSummary(string hour)
        {
            var content = BlobStorageService.Load("ElmahErrorsDetailed" + hour + "hours.json");
            List<ElmahError> listOfEvents= new List<ElmahError>();
            if (content != null)
            {
                listOfEvents = new JavaScriptSerializer().Deserialize<List<ElmahError>>(content);
            }
            return PartialView("~/Views/TroubleShooting/TroubleShooting_ElmahErrorSummary.cshtml", listOfEvents);
        }
    }
}
