using DotNet.Highcharts.Enums;
using DotNet.Highcharts.Helpers;
using DotNet.Highcharts.Options;
using NuGetDashboard.Models;
using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;


namespace NuGetDashboard.Controllers.LiveSiteMonitoring
{
    public class TrafficManagerController : Controller
    {
        //
        // GET: /TrafficManager/

        public ActionResult Index()
        {
            return PartialView("~/Views/UpTime/TrafficManager_Index.cshtml");
        }
        

        [HttpGet]
        public JsonResult GetEndPointStatus(int endpoint)
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("TrafficManagerStatus.json");
            if (dict != null && dict.Count > 0)
                return Json("<h1>" +dict.Keys.ElementAt(endpoint) +"</h1> </br> </br>" + dict.Values.ElementAt(endpoint), JsonRequestBehavior.AllowGet);
            else
                return Json("N/A");
        }
    }
}
