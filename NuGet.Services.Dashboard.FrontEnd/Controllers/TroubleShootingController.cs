using Newtonsoft.Json.Linq;
using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NuGet.Services.Dashboard.Common;
using System.Web.Script.Serialization;
using System.Configuration;

namespace NuGetDashboard.Controllers.Diagnostics
{
    /// <summary>
    /// Gets troubleshooting details like ElmahLog detailed summary and DB detailed summary.
    /// </summary>
    public class TroubleShootingController : Controller
    {
        public ActionResult TroubleShooting_Details()
        {
            return View("~/Views/TroubleShooting/TroubleShooting_Details.cshtml");
        }
        
       
    }
}
