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
    /// <summary>
    /// Provides uptime details for the Gallery ( data retrieved from pingdom).
    /// </summary>
    public class UpTimeController : Controller
    {
        public ActionResult Index()
        {
            return PartialView("~/Views/UpTime/UpTime_Index.cshtml");
        }       
     
        [HttpGet]
        public ActionResult Now()
        {
            List<PingdomStatusViewModel> checks = GetStatusInternal();
            return PartialView("~/Views/UpTime/UpTime_Now.cshtml", checks);
        }

        public ActionResult Details()
        {
            return PartialView("~/Views/UpTime/UpTime_Details.cshtml");
        }
      
        [HttpGet]
        public ActionResult ThisWeek()
        {
            //Returns the chart for Average response for the last week
            string[] checkNames = new string[] { "feed.raw.packages.list", "package.restore.download", "package.restore.lookup","feed.top.30.by.downloads" };
            List<DotNet.Highcharts.Options.Series> seriesSet = new List<DotNet.Highcharts.Options.Series>();
            List<string> xValues = new List<string>();
            List<Object> yValues = new List<Object>();
            foreach (string check in checkNames)
            {
                //Get the response values from pre-created blobs for each check.
                BlobStorageService.GetJsonDataFromBlob(check + "WeeklyReport.json", out xValues, out yValues);
                seriesSet.Add(new DotNet.Highcharts.Options.Series
                {
                    Data = new Data(yValues.ToArray()),
                    Name = check
                });
            }
            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetLineChart(seriesSet, xValues,"AvgResponseTime",600);
            return PartialView("~/Views/Shared/PartialChart.cshtml", chart);          
        }

        [HttpGet]
        public ActionResult GetPackageRestoreUptime()
        {
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("package.restore.downloadHourlyReport", "PackageRestoreResponseInMilleSec"));
        }             

        [HttpGet]
        public JsonResult GetStatus()
        {
            List<PingdomStatusViewModel> checks = GetStatusInternal();
            return Json(checks, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult PackageRestoreThisWeek()
        {
            string[] blobNames = new string[8];
            for (int i = 0; i < 8; i++)
                blobNames[i] = "package.restore.download" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTime().AddDays(-i)) + "DetailedReport";
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "PackageRestoreThisWeek", 24, 800));
        }
        

        #region PrivateMethods

        private List<PingdomStatusViewModel> GetStatusInternal()
        {

            List<PingdomStatusViewModel> checks = new List<PingdomStatusViewModel>();
            NetworkCredential nc = new NetworkCredential(ConfigurationManager.AppSettings["PingdomUserName"], ConfigurationManager.AppSettings["PingdomPassword"]);            
            WebRequest request = WebRequest.Create("https://api.pingdom.com/api/2.0/checks");
            request.Credentials = nc;
            request.Headers.Add(ConfigurationManager.AppSettings["PingdomAppKey"]);            
            request.PreAuthenticate = true;
            request.Method = "GET";
            WebResponse respose = request.GetResponse();
            using (var reader = new StreamReader(respose.GetResponseStream()))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var objects = js.Deserialize<dynamic>(reader.ReadToEnd());
                foreach (var o in objects["checks"])
                {
                    if (o["name"].ToString().Contains("curated"))
                        continue;
                    checks.Add(new PingdomStatusViewModel(o["id"], o["name"], o["status"], o["lastresponsetime"]));
                }
            }
            return checks;
        }     

        #endregion
    }
}
