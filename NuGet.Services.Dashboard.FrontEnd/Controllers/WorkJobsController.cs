using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using NuGet.Services.Dashboard.Common;
using NuGetDashboard.Utilities;
using System.Text;

namespace NuGetDashboard.Controllers.LiveSiteMonitoring
{
    public class WorkJobsController : Controller
    {
        public ActionResult WorkJobs_Detail()
        {
            List<WorkInstanceDetail> jobDetail = new List<WorkInstanceDetail>();
            List<WorkServiceAdmin> key = new List<WorkServiceAdmin>();
            var content = BlobStorageService.Load("WorkJobDetail.json");
            var admin = BlobStorageService.Load("WorkServiceAdminKey.json");
            if (content != null && key != null)
            {
                jobDetail = new JavaScriptSerializer().Deserialize<List<WorkInstanceDetail>>(content);
                key = new JavaScriptSerializer().Deserialize<List<WorkServiceAdmin>>(admin);
            }
            
            ViewBag.work = jobDetail;
            ViewBag.admin0 = key[0].username;
            ViewBag.admin1 = key[1].username;
            ViewBag.key0 = key[0].key;
            ViewBag.key1 = key[1].key;
            return View("~/Views/WorkJobs/WorkJobs_detail.cshtml");
        }
    }
}
