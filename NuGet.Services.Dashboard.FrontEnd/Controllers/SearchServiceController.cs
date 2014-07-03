using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace NuGetDashboard.Controllers.LiveSiteMonitoring
{
    public class SearchServiceController : Controller
    {
        //
        // GET: /SearchService/

        public ActionResult CpuMemUsage()
        {
            return PartialView("~/Views/SearchService/CpuMemUsage.cshtml"); ;
        }

        public ActionResult GetIndexLag()
        {
            return PartialView("~/Views/SearchService/GetIndexLag.cshtml");
        }

        [HttpGet]
        public JsonResult GetCurrentIndexingStatus()
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob("IndexingDiffCount" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()) + "HourlyReport.json");
            if (dict != null && dict.Count > 0)
                return Json(dict.Values.ElementAt(dict.Count - 1), JsonRequestBehavior.AllowGet);
            else
                return Json("N/A");
        }

        [HttpGet]
        public JsonResult GetSearchServiceStatus()
        {
            Dictionary<string, string> dict_cpu = BlobStorageService.GetDictFromBlob("SearchCpuUsage" + string.Format("{0:MMdd}", DateTime.Now) + "HourlyReport.json");
            Dictionary<string, string> dict_mem = BlobStorageService.GetDictFromBlob("SearchMemUsage" + string.Format("{0:MMdd}", DateTime.Now) + "HourlyReport.json");
            StringBuilder sb = new StringBuilder();
            sb.Append("<h1>" + "CPU Seconds" + "</h1> </br>");
            if (dict_cpu != null && dict_cpu.Count > 0)
            {
                sb.Append(dict_cpu.Values.Last() + "</br>");
            }
            else
                sb.Append("N/A");

            sb.Append("<h1>" + "virual memory size" + "</h1> </br>");
            if (dict_mem != null && dict_mem.Count > 0)
            {
                sb.Append(dict_mem.Values.Last() + "</br>");
            }
            else
                sb.Append("N/A");

            return Json(sb.ToString(), JsonRequestBehavior.AllowGet);
           
        }


        public ActionResult CloudServiceInstances()
        {
            return PartialView("~/Views/SearchService/CloudServiceInstances.cshtml");
        }

        [HttpGet]
        public JsonResult GetCloudServiceInstanceStatus(string CloudServiceName)
        {
            Dictionary<string, string> dict = BlobStorageService.GetDictFromBlob(CloudServiceName + "InstanceStatus.json");
            if (dict != null && dict.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("<h1>" + CloudServiceName + "</h1> </br>");
                foreach (KeyValuePair<string, string> kvp in dict)
                {
                    sb.Append(kvp.Key + "-" + kvp.Value + "</br>");
                }
                return Json(sb.ToString(), JsonRequestBehavior.AllowGet);
            }
            else
                return Json("N/A", JsonRequestBehavior.AllowGet);
        }

    }
}
