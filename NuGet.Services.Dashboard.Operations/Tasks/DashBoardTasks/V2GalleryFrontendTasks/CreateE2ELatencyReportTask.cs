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

        internal static string NugetExePath = @"NuGet.exe";
        internal static string PushCommandString = " push ";
        internal static string SourceSwitchString = " -Source ";
        internal static string APIKeySwitchString = " -ApiKey ";
        internal static string SearchAPIBase = "https://api-search.int.nugettest.org/search";
        internal static string ProductionCatalog = "http://nugetdev0.blob.core.windows.net/ng-catalogs/4/index.json";

        public override void ExecuteCommand()
        {

            StopWatches timer = new StopWatches();
            DateTime today = DateTime.Today;
            string day = string.Format("{0:d}", today);
            string version = string.Empty;
            string file = Path.Combine(Environment.CurrentDirectory, TestPackageName + ".nupkg");
            string newPackage = GetNewPackage(file, out version);

            Console.WriteLine("Pushing :{0}", newPackage);
            long UploadTimeElapsed = UploadPackage(timer, newPackage, Source, ApiKey);
            ReportHelpers.AppendDatatoBlob(StorageAccount, ("UploadPackageTimeElapsed" + day + ".json"), new Tuple<string, string>(string.Format("{0:HH:mm}", DateTime.Now), UploadTimeElapsed.ToString()), 48, ContainerName);
            File.Delete("backup"); 

            long DownloadTimeElapsed = -1;
            timer.DownloadTimeElapsed = Stopwatch.StartNew();
            Task<string> result = null;
            result = DownloadPackageFromFeed(timer, TestPackageName, version, out DownloadTimeElapsed);
            Console.WriteLine(result.Status);
            DownloadTimeElapsed = timer.DownloadTimeElapsed.ElapsedMilliseconds;
            ReportHelpers.AppendDatatoBlob(StorageAccount, ("DownloadPackageTimeElapsed" + day + ".json"), new Tuple<string, string>(string.Format("{0:HH:mm}", DateTime.Now), DownloadTimeElapsed.ToString()), 48, ContainerName);

            long SearchTimeElapsed = -1;
            //SearchPackage is called until the uploaded package is seen in the search result
            while (SearchTimeElapsed == -1)
            {
                SearchTimeElapsed = SearchPackage(timer, TestPackageName, version);
            }
            ReportHelpers.AppendDatatoBlob(StorageAccount, ("SearchPackageTimeElapsed" + day + ".json"), new Tuple<string, string>(string.Format("{0:HH:mm}", DateTime.Now), SearchTimeElapsed.ToString()), 48, ContainerName);


            JToken timeStampCatalog;
            int CatalogLag = CatalogPackage(timer, TestPackageName, out timeStampCatalog);
            ReportHelpers.AppendDatatoBlob(StorageAccount, ("CatalogLag" + day + ".json"), new Tuple<string, string>(string.Format("{0:HH:mm}", DateTime.Now), CatalogLag.ToString()), 48, ContainerName);
            ReportHelpers.CreateBlob(StorageAccount, ("LastCatalogTimeStamp.json"), ContainerName, "SqlDateTime", ReportHelpers.ToStream(timeStampCatalog));

            JToken timeStampResolver;
            double ResolverLag = CheckLagBetweenCatalogAndResolverBlobs(out timeStampResolver);                       
            ReportHelpers.AppendDatatoBlob(StorageAccount, ("ResolverLag" + day + ".json"), new Tuple<string, string>(string.Format("{0:HH:mm}", DateTime.Now), ResolverLag.ToString()), 48, ContainerName);
            ReportHelpers.CreateBlob(StorageAccount, ("LastResolverTimeStamp.json"), ContainerName, "SqlDateTime", ReportHelpers.ToStream(timeStampResolver));
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

        private Task<string> DownloadPackageFromFeed(StopWatches timer, string packageId, string version, out long DownloadTimeElapsed, string operation = "Install")
        {
            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            string requestUri = "https://int.nugettest.org/api/v2/" + @"Package/" + packageId + @"/" + version;
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


        private int CatalogPackage(StopWatches timer, string TestPackageName, out JToken commitTimeStamp)
        {
            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            string root = client.GetStringAsync(ProductionCatalog).Result;
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
                    int lag = dbExecutor.Query<Int32>(query).SingleOrDefault();
                    return lag;
                }
            }

            //IEnumerable<JToken> rootItems = indexObj["items"].OrderBy(item => item["commitTimestamp"].ToObject<DateTime>());
            //int pageCount = rootItems.Count();
            //Console.WriteLine(pageCount);
            //Uri pageUri = indexObj["http://nugetprod0.blob.core.windows.net/ng-catalogs/0/page" + (pageCount - 1)].ToObject<Uri>();
            //string page = client.GetStringAsync(pageUri).Result;
            //JObject pageObj = JObject.Parse(root);
            //IEnumerable<JToken> pageItems = pageObj["items"].OrderBy(item => item["commitTimestamp"].ToObject<DateTime>());

        }

        private double CheckLagBetweenCatalogAndResolverBlobs(out JToken timeStampResolver)
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

            TimeSpan span = cursorTimestamp - catalogTimestamp;

            double delta = span.TotalMinutes;
            timeStampResolver = cursorTimestamp;

            //string outputMessage;

            //outputMessage = string.Format("The lag from the package catalog to the resolver blob set is {0} minutes. Threshold is {1}. The resolver blob pipeline may be running too slowly.", delta, thresholdValues.CatalogToResolverBlobLagThresholdInMinutes);
            //if (delta > thresholdValues.CatalogToResolverBlobLagThresholdInMinutes || delta < 0)
            //{
            //    new SendAlertMailTask
            //    {
            //        AlertSubject = "Alert: resolver blob generation lag",
            //        Details = outputMessage,
            //        AlertName = "Alert for CheckLagBetweenCatalogAndResolverBlobs",
            //        Component = "LagBetweenCatalogAndResolverBlobs"
            //    }.ExecuteCommand();
            //}

            //Console.WriteLine(outputMessage);
            return delta;
        }

    }


    //object that will hold all the stop watches for the different tasks
    internal class StopWatches
    {
        internal Stopwatch UploadTimeElapsed;
        public Stopwatch SearchTimeElapsed;
        public Stopwatch DownloadTimeElapsed;
        public Stopwatch CatalogTimeElapsed;
    }
}
