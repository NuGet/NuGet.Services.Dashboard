using DotNet.Highcharts.Enums;
using DotNet.Highcharts.Helpers;
using DotNet.Highcharts.Options;
using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetDashboard.Controllers.Diagnostics
{
    /// <summary>
    /// Provides details about the resource utilization on the server : CPU, memory and DB.
    /// </summary>
    public class ResourceMonitoringController : Controller
    {
        public ActionResult Index()
        {
            return PartialView("~/Views/ResourceMonitoring/ResourceMonitoring_Index.cshtml");
        }
        
        [HttpGet]
        public ActionResult Now()
        {
            ViewBag.ControllerName = "ResourceMonitoring";
            return PartialView("~/Views/ResourceMonitoring/ResourceMonitoring_Now.cshtml");
        }     

        [HttpGet]
        public ActionResult Details()
        {
            return PartialView("~/Views/ResourceMonitoring/ResourceMonitoring_Details.cshtml");
        }

        [HttpGet]
        public ActionResult Instance0CPU()
        {
            return GetChart("Instance0CPU");
        }

        [HttpGet]
        public ActionResult Instance1CPU()
        {
            return GetChart("Instance1CPU");
        }

        [HttpGet]
        public ActionResult Instance2CPU()
        {
            return GetChart("Instance2CPU");
        }

        [HttpGet]
        public ActionResult Instance0Memory()
        {
           return GetChart("Instance0Memory");

        }
        [HttpGet]
        public ActionResult Instance1Memory()
        {
           return GetChart("Instance1Memory");
        }

        [HttpGet]
        public ActionResult Instance2Memory()
        {
            return GetChart("Instance2Memory");
        }

        [HttpGet]
        public ActionResult DBCPUTime()
        {            
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("DBCPUTime","DBCPUTimeInSeconds"));
        }

        [HttpGet]
        public ActionResult DBRequests()
        {
            return GetChart("DBRequests");          
        }

        [HttpGet]
        public ActionResult DBConnections()
        {
            return GetChart("DBConnections");
        }

        [HttpGet]
        public JsonResult GetCPUStatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("Instance0CPU.json");
            return Json(dict.Values.ElementAt(dict.Count-1), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetMemoryStatus()
        {
            //return Json(BlobStorageService.GetValueFromBlob("MemoryStatus.json", "Memory"), JsonRequestBehavior.AllowGet);
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("Instance0Memory.json");
            return Json(dict.Values.ElementAt(dict.Count-1), JsonRequestBehavior.AllowGet);
        }

        private ActionResult GetChart(string blobName)
        {   
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobName,blobName));
        }

    }
}
