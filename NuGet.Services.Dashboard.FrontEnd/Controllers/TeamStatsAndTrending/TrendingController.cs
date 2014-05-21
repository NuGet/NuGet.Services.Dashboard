using DotNet.Highcharts;
using DotNet.Highcharts.Helpers;
using DotNet.Highcharts.Options;
using NuGet.Services.Dashboard.Common;
using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;

namespace NuGetDashboard.Controllers.Trending
{
    public class TrendingController : Controller
    {        
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Details()
        {
            return PartialView("~/Views/Trending/Trending_Details.cshtml");
        }

        [HttpGet]
        public ActionResult PackagesThiSWeek()
        {
            string[] blobNames = new string[4];
            for (int i = 0; i < 4; i++)
                blobNames[i] = "Uploads" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow().AddDays(-i)) + "HourlyReport";
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "UploadsPerHour", 24, 800));
        }

        [HttpGet]
        public ActionResult UsersThiSWeek()
        {
            string[] blobNames = new string[4];
            for (int i = 0; i < 4; i++)
                blobNames[i] = "Users" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow().AddDays(-i)) + "HourlyReport";
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "UsersPerHour", 24, 800));
        }

        [HttpGet]
        public JsonResult GetHourlyPackagetatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("Uploads" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()) + "HourlyReport.json");
            if (dict != null && dict.Count > 0)
            {
                //find the sum of values of each hour from today's report.
                int sum = 0;
                foreach (KeyValuePair<string, string> pair in dict)
                {
                    int count = Convert.ToInt32(pair.Value);
                    sum = sum + count;
                }
                return Json(sum.ToString(), JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json("N/A");
            }
        }

        [HttpGet]
        public JsonResult GetHourlyUsertatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("Users" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()) + "HourlyReport.json");
            if (dict != null && dict.Count > 0)
            {
                //find the sum of values of each hour from today's report.
                int sum = 0;
                foreach (KeyValuePair<string, string> pair in dict)
                {
                    int count = Convert.ToInt32(pair.Value);
                    sum = sum + count;
                }
                return Json(sum.ToString(), JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json("N/A");
            }
        }
        
        [HttpGet]
        public ActionResult VsTrend()
        {
            Dictionary<string, string> content = BlobStorageService.GetDictFromBlob("VsTrend" + "120Day.json");
            return PartialView("~/Views/Trending/VsTrend.cshtml", content);
        }

        [HttpGet]
        public ActionResult OptTrend()
        {
            return PartialView("~/Views/Trending/OptTrend.cshtml");
        }
        
        
        
        [HttpGet]
        public ActionResult OperationTrend()
        {
            int hour = 30;
            string[] Operation = new JavaScriptSerializer().Deserialize<string[]>(BlobStorageService.Load("OperationType.json"));

            List<string> blobNames = new List<string>();
            foreach (string opt in Operation)
            {
                blobNames.Add(opt + hour + "Day");
            }
           return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames.ToArray(), "OperationForLast"+ hour +"Day",24,500));
        }

        [HttpGet]
        public ActionResult RestoreTrend()
        {
            int hour = 30;
            string blobName = "Restore" + hour + "Day";
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobName, "RestoreForLast" + hour + "Day", 24, 500));
        }


    }
}
