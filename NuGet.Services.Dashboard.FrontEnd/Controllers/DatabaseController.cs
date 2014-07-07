using DotNet.Highcharts.Enums;
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
using System.Text;

namespace NuGetDashboard.Controllers.Diagnostics
{
    /// <summary>
    /// Provides details about the resource utilization on the server : CPU, memory and DB.
    /// </summary>
    public class DatabaseController : Controller
    {
        public ActionResult Database_Details()
        {
            return View("~/Views/Database/Database_Details.cshtml");
        }
       
        [HttpGet]
        public ActionResult DBRequests()
        {                   
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("DBRequests" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()), "DBRequests",12));
        }

        [HttpGet]
        public ActionResult DBConnections()
        {
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName("DBConnections" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow()), "DBConnections", 12));           
        }

        [HttpGet]
        public ActionResult DBIndexFragmentation()
        {
            List<DatabaseIndex> indexDetails = new List<DatabaseIndex>();
            var content = BlobStorageService.Load("DBIndexFragmentation.json");
            if (content != null)
            {
                indexDetails = new JavaScriptSerializer().Deserialize<List<DatabaseIndex>>(content);
            }
            return PartialView("~/Views/Database/DBIndexFragmentation.cshtml", indexDetails);
        }

        [HttpGet]
        public ActionResult DBSizeDetails()
        {
            List<DatabaseSize> sizeDetails = new List<DatabaseSize>();
            var content = BlobStorageService.Load("DBSize.json");
            if (content != null)
            {
                sizeDetails = new JavaScriptSerializer().Deserialize<List<DatabaseSize>>(content);
            }
            return PartialView("~/Views/Database/DBSizeDetails.cshtml", sizeDetails);
        }
     
        [HttpGet]
        public ActionResult DBRequestsThisWeek()
        {
            string[] blobNames = new string[8];
            for (int i = 0; i < 8; i++)
                blobNames[i] = "DBRequests" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow().AddDays(-i));
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "DBRequests", 50, 600));
        }
        [HttpGet]
        public ActionResult DBConnectionsThisWeek()
        {
            string[] blobNames = new string[8];
            for (int i = 0; i < 8; i++)
                blobNames[i] = "DBConnections" + string.Format("{0:MMdd}", DateTimeUtility.GetPacificTimeNow().AddDays(-i));
            return PartialView("~/Views/Shared/PartialChart.cshtml", ChartingUtilities.GetLineChartFromBlobName(blobNames, "DBConnections", 50, 600));
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
            return PartialView("~/Views/Database/DBEventsSummary.cshtml", listOfEvents);
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
            return PartialView("~/Views/Database/DBRequestsSummary.cshtml", listOfRequests);
        }

        public ActionResult RefreshDatabaseEvent()
        {
            List<DatabaseEvent> listOfEvents = new List<DatabaseEvent>();
            RefreshDB Refresh = new RefreshDB(MvcApplication.DBConnectionString, 1);
            listOfEvents = Refresh.RefreshDatabaseEvent();
            return PartialView("~/Views/Database/DBEventsSummary.cshtml", listOfEvents);
        }

        public ActionResult RefreshDatabaseRequest()
        {
            List<DatabaseRequest> listOfRequests = new List<DatabaseRequest>();
            RefreshDB Refresh = new RefreshDB(MvcApplication.DBConnectionString, 1);
            listOfRequests = Refresh.RefreshDatebaseRequest();

            return PartialView("~/Views/Database/DBRequestsSummary.cshtml", listOfRequests);
        }

    }
}
