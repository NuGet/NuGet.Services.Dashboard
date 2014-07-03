using NuGet.Services.Dashboard.Common;
using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;

namespace NuGetDashboard.Controllers.LiveSiteMonitoring
{
    public class AzureManagementController : Controller
    {
        //
        // GET: /AzureManagement/

        public ActionResult TrafficManager_Index()
        {
            return PartialView("~/Views/AzureManagement/TrafficManager_Index.cshtml");
        }


        [HttpGet]
        public JsonResult TrafficManager_GetEndPointStatus(int endpoint)
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("TrafficManagerStatus.json");
            if (dict != null && dict.Count > 0)
                return Json("<h1>" + dict.Keys.ElementAt(endpoint) + "</h1> </br> </br>" + dict.Values.ElementAt(endpoint), JsonRequestBehavior.AllowGet);
            else
                return Json("N/A");
        }

        [HttpGet]
        public JsonResult GetHourlyInstanceCount()
        {
            //TBD: Need to take the service name from the config.
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("nuget-prod-0-v2galleryInstanceCount" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()) + "HourlyReport.json");
            if (dict != null && dict.Count > 0)
                return Json(dict.Values.ElementAt(dict.Count - 1), JsonRequestBehavior.AllowGet);
            else
                return Json("N/A");
        }  
    }
}
