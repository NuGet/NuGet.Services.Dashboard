using AnglicanGeek.DbExecutor;
using Ionic.Zip;
using Newtonsoft.Json.Linq;
using NuGet;
using NuGet.Services.Dashboard.Common;
using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;


namespace NuGetGallery.Operations
{
    [Command("CreateE2ELatencyReportTask", "Logs time take to upload package to various services", AltName = "ce2elrt")]
    public class CreateE2ELatencyReportTask : DatabaseAndStorageTask
    {
        [Option("Source", AltName = "src")]
        public string Source { get; set; }

        [Option("ApiKey", AltName = "akey")]
        public string ApiKey { get; set; }

        [Option("PackageName", AltName = "package")]
        public string TestPackageName { get; set; }

        [Option("CatalogUrl", AltName = "cau")]
        public string CatalogUrl { get; set; }

        [Option("ResolverBlobsBaseUrl", AltName = "reu")]
        public string ResolverBlobsBaseUrl { get; set; }

        [Option("SearchAPIBase", AltName = "searchAPIbase")]
        public string SearchAPIBase { get; set; }

        [Option("DownloadAPIBase", AltName = "downloadAPIbase")]
        public string DownloadAPIBase { get; set; }

        internal static string NugetExePath = @"NuGet.exe";
        internal static string PushCommandString = " push ";
        internal static string SourceSwitchString = " -Source ";
        internal static string APIKeySwitchString = " -ApiKey ";

        public override void ExecuteCommand()
        {

            StopWatches timer = new StopWatches();
            DateTime today = DateTime.Today;
            string day = string.Format("{0:yyyy-MM-dd}", today);
            string version = string.Empty;
            string file = Path.Combine(Environment.CurrentDirectory, TestPackageName + ".nupkg");
            string newPackage = GetNewPackage(file, out version);

            //upload
            Console.WriteLine("Pushing :{0}", newPackage);
            long UploadTimeElapsed = UploadPackage(timer, newPackage, Source, ApiKey);
            ReportHelpers.AppendDatatoBlob(StorageAccount, ("UploadPackageTimeElapsed" + day + ".json"), new Tuple<string, string>(string.Format("{0:HH:mm}", DateTime.Now), UploadTimeElapsed.ToString()), 48, ContainerName);
            File.Delete("backup"); 

            //download
            long DownloadTimeElapsed = -1;
            Task<string> result = null;
            result = DownloadPackageFromFeed(timer, TestPackageName, version, out DownloadTimeElapsed);
            Console.WriteLine(result.Status);
            DownloadTimeElapsed = timer.DownloadTimeElapsed.ElapsedMilliseconds;
            ReportHelpers.AppendDatatoBlob(StorageAccount, ("DownloadPackageTimeElapsed" + day + ".json"), new Tuple<string, string>(string.Format("{0:HH:mm}", DateTime.Now), DownloadTimeElapsed.ToString()), 48, ContainerName);

            //search
            long SearchTimeElapsed = -1;
            //SearchPackage is called until the uploaded package is seen in the search result
            while (SearchTimeElapsed == -1)
            {
                SearchTimeElapsed = SearchPackage(timer, TestPackageName, version);
            }

            ReportHelpers.AppendDatatoBlob(StorageAccount, ("SearchPackageTimeElapsed" + day + ".json"), new Tuple<string, string>(string.Format("{0:HH:mm}", DateTime.Now), SearchTimeElapsed.ToString()), 48, ContainerName);

            //catalog lag
            JToken timeStampCatalog;
            int CatalogLag = DBToCatalogLag(timer, TestPackageName, out timeStampCatalog);
            ReportHelpers.AppendDatatoBlob(StorageAccount, ("CatalogLag" + day + ".json"), new Tuple<string, string>(string.Format("{0:HH:mm}", DateTime.Now), CatalogLag.ToString()), 48, ContainerName);
            ReportHelpers.CreateBlob(StorageAccount, ("LastCatalogTimeStamp.json"), ContainerName, "SqlDateTime", ReportHelpers.ToStream(timeStampCatalog));

            //resolver lag
            JToken timeStampResolver;
            double ResolverLag = CatalogToResolverLag(out timeStampResolver);                       
            ReportHelpers.AppendDatatoBlob(StorageAccount, ("ResolverLag" + day + ".json"), new Tuple<string, string>(string.Format("{0:HH:mm}", DateTime.Now), ResolverLag.ToString()), 48, ContainerName);
            ReportHelpers.CreateBlob(StorageAccount, ("LastResolverTimeStamp.json"), ContainerName, "SqlDateTime", ReportHelpers.ToStream(timeStampResolver));
            SendAlerts(UploadTimeElapsed, DownloadTimeElapsed, SearchTimeElapsed, CatalogLag, ResolverLag);
        }

        private void SendAlerts(long Upload, long Download, long Search, int Catalog, double ResolverBlobs)
        {
            AlertThresholds thresholdValues = new JavaScriptSerializer().Deserialize<AlertThresholds>(ReportHelpers.Load(StorageAccount, "Configuration.AlertThresholds.json", ContainerName));

            if (Upload> thresholdValues.UploadPackageThreshold)
            {
                CreateAlert(Upload.ToString(), thresholdValues.UploadPackageThreshold.ToString(), "Upload", "Upload API");
            }

            if (Download>thresholdValues.DownloadPackageThreshold)
            {
                CreateAlert(Download.ToString(), thresholdValues.DownloadPackageThreshold.ToString(), "Download", "Download API");
            }

            if (Search>thresholdValues.SearchPackageThreshold)
            {
                CreateAlert(Search.ToString(), thresholdValues.SearchPackageThreshold.ToString(), "Search", "Search API");
            }

            if (Catalog>thresholdValues.CatalogLagThreshold)
            {
                CreateAlert(Catalog.ToString(), thresholdValues.CatalogLagThreshold.ToString(), "Catalog Lag", "Database to Catalog");
            }

            if (ResolverBlobs>thresholdValues.ResolverLagThreshold)
            {
                CreateAlert(ResolverBlobs.ToString(), thresholdValues.ResolverLagThreshold.ToString(), "Resolver Lag", "Catalog to Resolver Blobs");
            }
        }

        private void CreateAlert(string value, string threshold, string scenarioName, string component)
        {
            new SendAlertMailTask
            {
                AlertSubject = "Error: E2E Latency Task Alert for "+scenarioName+" scenario",
                Details = string.Format("Error Threshold: {0}, Actual Value: {1} ", threshold, value),
                AlertName = "Error: Latency Task Alert",
                Component = component,
                Level = "Error"
            }.ExecuteCommand();
        }

        //changes the version of the package to be uploaded (+1) and returns the fileName 
        private string GetNewPackage(string fileName, out string version)
        {
            ZipPackage package = new ZipPackage(fileName);
            string packageId = package.Id;
            string packageVersionMajor = package.Version.Version.Major.ToString();
            string packageVersionMinor = package.Version.Version.Minor.ToString();
            string packageVersionBuild = package.Version.Version.Build.ToString();
            int versionMajor = Convert.ToInt32(packageVersionMajor);
            int versionMinor = Convert.ToInt32(packageVersionMinor);
            int versionBuild = Convert.ToInt32(packageVersionBuild);
            string nuspecFilePath = packageId + ".nuspec";
            using (ZipFile zipFile = new ZipFile(fileName))
            {
                ZipEntry entry = zipFile.Entries.Where(item => item.FileName.Contains(".nuspec")).ToList()[0];
                entry.Extract(Environment.CurrentDirectory, ExtractExistingFileAction.OverwriteSilently);
                string extractedfile = Path.Combine(Environment.CurrentDirectory, nuspecFilePath);
                string newFile = "Updated" + nuspecFilePath;
                string newVersion = (++versionMajor).ToString() + "." + packageVersionMinor + "." + packageVersionBuild;
                string nuspecContent = File.ReadAllText(extractedfile);
                string updatedContent = nuspecContent.Replace(packageVersionMajor + "." + packageVersionMinor + "." + packageVersionBuild, newVersion);
                StreamWriter sw = new StreamWriter(newFile);
                sw.Write(updatedContent);
                sw.Flush();
                sw.Close();
                zipFile.RemoveEntry(entry);
                File.Replace(newFile, extractedfile, "backup", true);
                zipFile.AddFile(extractedfile, ".");
                zipFile.Save(fileName);
                Console.WriteLine("Here");
                version = newVersion;
                return fileName;
            }
        }

        //pushes the package to the source specified (Source) and returns the time elapsed in milliseconds to upload the package, or -1 if upload fails
        private long InvokeNugetProcess(StopWatches timer, string arguments, out string standardError, out string standardOutput, string WorkingDir = null)
        {
            Process nugetProcess = new Process();
            string pathToNugetExe = Path.Combine(Environment.CurrentDirectory, NugetExePath);
            ProcessStartInfo nugetProcessStartInfo = new ProcessStartInfo(pathToNugetExe);
            nugetProcessStartInfo.Arguments = arguments;
            nugetProcessStartInfo.RedirectStandardError = true;
            nugetProcessStartInfo.RedirectStandardOutput = true;
            nugetProcessStartInfo.RedirectStandardInput = true;
            nugetProcessStartInfo.UseShellExecute = false;
            nugetProcessStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            nugetProcessStartInfo.CreateNoWindow = true;
            nugetProcess.StartInfo = nugetProcessStartInfo;
            nugetProcess.StartInfo.WorkingDirectory = WorkingDir;
            timer.UploadTimeElapsed = Stopwatch.StartNew();
            nugetProcess.Start();
            timer.UploadTimeElapsed.Start();
            standardError = nugetProcess.StandardError.ReadToEnd();
            Console.WriteLine(standardError);
            standardOutput = nugetProcess.StandardOutput.ReadToEnd();
            Console.WriteLine(standardOutput);
            nugetProcess.WaitForExit();
            timer.UploadTimeElapsed.Stop();
            timer.DownloadTimeElapsed = Stopwatch.StartNew();
            timer.SearchTimeElapsed = Stopwatch.StartNew();
            if (nugetProcess.ExitCode == 0)
            {
                return timer.UploadTimeElapsed.ElapsedMilliseconds;
            }

            else
            {

                return -1;
            }
        }

        // Uploads the given package to the specified source and returns time elapsed to upload, or -1 if upload fails.
        private long UploadPackage(StopWatches timer, string packageFullPath, string sourceName, string ApiKey)
        {
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            return InvokeNugetProcess(timer, string.Join(string.Empty, new string[] { PushCommandString, @"""" + packageFullPath + @"""", APIKeySwitchString, ApiKey, SourceSwitchString, sourceName }), out standardError, out standardOutput);
        }

        //searches for the package (id, version) specified, returns -1 if not found and is invoked again by the calling code, until the package is found
        private long SearchPackage(StopWatches timer, string TestPackageName, string Version)
        {
            string uri = SearchAPIBase + "/query?q=%27" + TestPackageName + "%27&luceneQuery=false";
            Uri searchAPI = new Uri(uri);
            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            string JSONResult = client.GetStringAsync(searchAPI).Result;
            SearchResult result = new SearchResult();
            result = new JavaScriptSerializer().Deserialize<SearchResult>(JSONResult);
            foreach (var document in result.data)
            {
                if (document.Version == Version && document.PackageRegistration.Id == TestPackageName)
                {
                    Console.WriteLine(document.PackageRegistration.Id + " " + document.Version);
                    timer.SearchTimeElapsed.Stop();
                    return timer.SearchTimeElapsed.ElapsedMilliseconds;
                }
            }

            return -1;
        }

        //downloads the package, given id and version
        private Task<string> DownloadPackageFromFeed(StopWatches timer, string packageId, string version, out long DownloadTimeElapsed, string operation = "Install")
        {
            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            string requestUri = DownloadAPIBase + @"Package/" + packageId + @"/" + version;
            bool flag = false;
            CancellationTokenSource cts = new CancellationTokenSource();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("user-agent", "TestAgent");
            request.Headers.Add("NuGet-Operation", operation);
            Task<HttpResponseMessage> responseTask = client.SendAsync(request);
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
            responseTask.ContinueWith((rt) =>
             {
                 HttpResponseMessage responseMessage = rt.Result;
                 if (responseMessage.StatusCode == HttpStatusCode.OK)
                 {
                     try
                     {
                         string filename;
                         ContentDispositionHeaderValue contentDisposition = responseMessage.Content.Headers.ContentDisposition;
                         if (contentDisposition != null)
                         {
                             filename = contentDisposition.FileName;
                         }
                         else
                         {
                             filename = packageId; // if file name not present set the package Id for the file name. 
                         }
                         FileStream fileStream = File.Create(filename);
                         Task contentTask = responseMessage.Content.CopyToAsync(fileStream);

                         contentTask.ContinueWith((ct) =>
                         {
                             try
                             {
                                 fileStream.Close();
                                 tcs.SetResult(filename);

                                 timer.DownloadTimeElapsed.Stop();
                                 flag = true;
                                 Console.WriteLine(ct.Status);
                                 return;

                             }
                             catch (Exception e)
                             {
                                 tcs.SetException(e);
                                 flag = false;
                             }
                         });


                     }
                     catch (Exception e)
                     {
                         tcs.SetException(e);
                         flag = false;
                     }
                 }
                 else
                 {
                     string msg = string.Format("Http StatusCode: {0}", responseMessage.StatusCode);
                     tcs.SetException(new ApplicationException(msg));
                     flag = false;
                 }
             });

            if (flag == true)
            {
                DownloadTimeElapsed = timer.DownloadTimeElapsed.ElapsedMilliseconds;
            }

            else
            {
                DownloadTimeElapsed = -1;
            }

            return tcs.Task;
        }

        //calculates the number of packages added to DB after the catalog was last modified
        private int DBToCatalogLag(StopWatches timer, string TestPackageName, out JToken commitTimeStamp)
        {
            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            string root = client.GetStringAsync(CatalogUrl).Result;
            JObject indexObj = JObject.Parse(root);
            JToken context = null;
            indexObj.TryGetValue("@context", out context);
            commitTimeStamp = null;
            indexObj.TryGetValue("commitTimestamp", out commitTimeStamp);
            SqlDateTime timeStamp = commitTimeStamp.ToObject<SqlDateTime>();
            using (var sqlConnection = new SqlConnection(ConnectionString.ConnectionString))
            {
                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    sqlConnection.Open();
                    string query = string.Format("Select count(*) from Packages where Created> '{0}'", timeStamp);
                    int lag = dbExecutor.Query<int>(query).SingleOrDefault();
                    return lag;
                }
            }
        }

        //calculates the lag between the catalog and resolver in minutes
        private double CatalogToResolverLag(out JToken timeStampResolver)
        {
            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            Task<string> cursorStringTask = client.GetStringAsync(new Uri(ResolverBlobsBaseUrl + "meta/cursor.json"));
            string cursorString = cursorStringTask.Result;  // Not async!
            JObject cursorJson = JObject.Parse(cursorString);
            DateTime cursorTimestamp = cursorJson["http://schema.nuget.org/collectors/resolver#cursor"]["@value"].ToObject<DateTime>();
            Task<string> catalogIndexStringTask = client.GetStringAsync(CatalogUrl);
            string catalogIndexString = catalogIndexStringTask.Result;
            JObject catalogIndex = JObject.Parse(catalogIndexString);
            DateTime catalogTimestamp = catalogIndex["commitTimestamp"].ToObject<DateTime>();
            TimeSpan span = catalogTimestamp - cursorTimestamp;
            double delta = span.TotalMinutes;
            timeStampResolver = cursorTimestamp;
            return delta;
        }
    }

    //object that will hold all the stop watches for the different tasks
    internal class StopWatches
    {
        internal Stopwatch UploadTimeElapsed;
        internal Stopwatch SearchTimeElapsed;
        internal Stopwatch DownloadTimeElapsed;
    }
}
