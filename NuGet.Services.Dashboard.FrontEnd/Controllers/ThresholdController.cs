using Newtonsoft.Json;
using NuGetDashboard.Models;
using NuGetDashboard.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using NuGet.Services.Dashboard.Common;
namespace NuGetDashboard.Controllers
{
    public class ThresholdController : Controller
    {
        //
        // GET: /Threshold/

        public ActionResult Threshold_Details()
        {
            List<ThresholdViewModel> ListofThreshold = new List<ThresholdViewModel>();
            Dictionary<string, string> standardDict = GetDictFromBlob("Standard.AlertThresholds.json");
            Dictionary<string, string> currentDict = GetDictFromBlob("Configuration.AlertThresholds.json");
            foreach (var pair in currentDict)
            {
                if (pair.Key.Contains("Warning"))
                {
                    ListofThreshold.Add(new ThresholdViewModel(pair.Key, "Warning", Int32.Parse(standardDict[pair.Key]), Int32.Parse(pair.Value)));
                }

                else
                {
                    ListofThreshold.Add(new ThresholdViewModel(pair.Key, "Error", Int32.Parse(standardDict[pair.Key]), Int32.Parse(pair.Value)));
                }
            }
            return View("~/Views/Threshold/Threshold_Details.cshtml",ListofThreshold);
        }

        public ActionResult Delete(string id)
        {
            Dictionary<string, string> standardDict = GetDictFromBlob("Standard.AlertThresholds.json");
            Dictionary<string, string> currentDict = GetDictFromBlob("Configuration.AlertThresholds.json");

            standardDict.Remove(id);
            currentDict.Remove(id);

            WriteDictToBlob(standardDict, "Standard.AlertThresholds.json");
            WriteDictToBlob(currentDict, "Configuration.AlertThresholds.json");

            return RedirectToAction("Threshold_Details");
        }

        [HttpGet]
        public ActionResult Edit(string id)
        {
            Dictionary<string, string> standardDict = GetDictFromBlob("Standard.AlertThresholds.json");
            Dictionary<string, string> currentDict = GetDictFromBlob("Configuration.AlertThresholds.json");

            if (id.Contains("Warning"))
            {
                return View(new ThresholdViewModel(id, "Warning", Int32.Parse(standardDict[id]), Int32.Parse(currentDict[id])));
            }
            else
            {
                return View(new ThresholdViewModel(id, "Error", Int32.Parse(standardDict[id]), Int32.Parse(currentDict[id])));
            }
        }

        [HttpPost]

        public ActionResult Edit(ThresholdViewModel threshold)
        {
            
            Dictionary<string, string> standardDict = GetDictFromBlob("Standard.AlertThresholds.json");
            Dictionary<string, string> currentDict = GetDictFromBlob("Configuration.AlertThresholds.json");

            standardDict[threshold.ID] = threshold.standard.ToString();
            currentDict[threshold.ID] = threshold.current.ToString();

            WriteDictToBlob(standardDict, "Standard.AlertThresholds.json");
            WriteDictToBlob(currentDict, "Configuration.AlertThresholds.json");
            return RedirectToAction("Threshold_Details");
        }

        [HttpGet]
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Create(ThresholdViewModel threshold)
        {
            Dictionary<string, string> standardDict = GetDictFromBlob("Standard.AlertThresholds.json");
            Dictionary<string, string> currentDict = GetDictFromBlob("Configuration.AlertThresholds.json");

            standardDict.Add(threshold.ID, threshold.standard.ToString());
            currentDict.Add(threshold.ID, threshold.current.ToString());

            WriteDictToBlob(standardDict, "Standard.AlertThresholds.json");
            WriteDictToBlob(currentDict, "Configuration.AlertThresholds.json");
            return RedirectToAction("Threshold_Details");
        }

        private Dictionary<string, string> GetDictFromBlob(string name)
        {
            string json = BlobStorageService.Load(name);
            Dictionary<string, string> Dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

            return Dict;
        }

        private void WriteDictToBlob(Dictionary<string, string> Dict, string name)
        {
            string json = new JavaScriptSerializer().Serialize(Dict);
            BlobStorageService.CreateBlob(name, "application/json", BlobStorageService.ToStream(json));
        }
    }
}
