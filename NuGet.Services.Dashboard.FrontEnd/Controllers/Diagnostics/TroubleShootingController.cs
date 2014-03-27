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
        /// <summary>
        /// Returns the data for DB Troubleshooting for the given hour.
        /// </summary>
        /// <param name="hour"></param>
        /// <returns></returns>
        public ActionResult DBRequestsSummary(string hour)
        {  
            var listOfEvents = new JavaScriptSerializer().Deserialize<List<DatabaseEvent>>(BlobStorageService.Load("DBDetailed" + hour + "Hour.json"));
            return PartialView("~/Views/TroubleShooting/TroubleShooting_DBRequestsSummary.cshtml", listOfEvents);
        }
        /// <summary>
        /// Returns the detailed report on Elmah for the past N hours.
        /// </summary>
        /// <param name="hour"></param>
        /// <returns></returns>
        public ActionResult ElmahErrorSummary(string hour)
        {
            var listOfEvents = new JavaScriptSerializer().Deserialize<List<ElmahError>>(BlobStorageService.Load("ElmahErrorsDetailed" + hour + "hours.json"));          
            return PartialView("~/Views/TroubleShooting/TroubleShooting_ElmahErrorSummary.cshtml", listOfEvents);

        }
    }
}
