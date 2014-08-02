using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Configuration;
using NuGet;
using System.IO.Compression;
using Ionic.Zip;
using System.Web.Script.Serialization;
using NuGetGallery.Operations.Common;
using System.Globalization;
using NuGet.Services.Dashboard.Common;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace NuGetGallery.Operations
{
    [Command("CreateE2ELatencyReportTask", "Logs time take to upload package to various services", AltName = "ce2elrt")]
    public class CreateE2ELatencyReportTask : StorageTask
    {
        [Option("Source", AltName = "src")]
        public string Source { get; set; }

        [Option("ApiKey", AltName = "akey")]
        public string ApiKey { get; set; }

        [Option("PackageName", AltName = "package")]
        public string TestPackageName { get; set; }

        internal static string NugetExePath = @"NuGet.exe";
        internal static string PushCommandString = " push ";
        internal static string SourceSwitchString = " -Source ";
        internal static string APIKeySwitchString = " -ApiKey ";
        internal static string SearchAPIBase = "https://api-search.int.nugettest.org/search";
        public override void ExecuteCommand()
        {
            StopWatches timer = new StopWatches();
            string today = DateTime.Today.ToString("d");
            DateTime now = DateTime.Now;
            string version = string.Empty;
            string file = Path.Combine(Environment.CurrentDirectory, TestPackageName+".nupkg");
            string newPackage = GetNewPackage(file, out version);
            Console.WriteLine("Pushing :{0}", newPackage);

            long UploadTimeElapsed = UploadPackage(newPackage, Source, ApiKey, timer);
            long SearchTimeElapsed=-1;
            while (SearchTimeElapsed==-1)
            {
                SearchTimeElapsed = SearchPackage(timer, TestPackageName, version);
            }

            File.Delete("backup");
            Console.WriteLine(DateTime.Today.ToString("d"));
            ReportHelpers.AppendDatatoBlob(StorageAccount, ("UploadPackageTimeElapsed" + today + ".json"), new Tuple<string, string>(string.Format("{0:HH:mm}", DateTime.Now), UploadTimeElapsed.ToString()), 48, ContainerName);
            ReportHelpers.AppendDatatoBlob(StorageAccount, ("SearchPackageTimeElapsed" + today + ".json"), new Tuple<string, string>(string.Format("{0:HH:mm}", DateTime.Now), SearchTimeElapsed.ToString()), 48, ContainerName);
        }

        public static string GetNewPackage(string fileName, out string version)
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

        public static long InvokeNugetProcess(StopWatches timer, string arguments, out string standardError, out string standardOutput, string WorkingDir = null)
        {
            Process nugetProcess = new Process();
            string pathToNugetExe = Path.Combine(Environment.CurrentDirectory, NugetExePath);
            //  Console.WriteLine("The NuGet.exe command to be executed is: " + pathToNugetExe + " " + arguments);

            // During the actual test run, a script will copy the latest NuGet.exe and overwrite the existing one
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
            nugetProcess.Start();
            timer.UploadTimeElapsed = Stopwatch.StartNew();
            timer.UploadTimeElapsed.Start();
            standardError = nugetProcess.StandardError.ReadToEnd();
            standardOutput = nugetProcess.StandardOutput.ReadToEnd();
            nugetProcess.WaitForExit();
            timer.UploadTimeElapsed.Stop();

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

        /// <summary>
        /// Uploads the given package to the specified source and returns the exit code.
        /// </summary>
        /// <param name="packageFullPath"></param>
        /// <param name="sourceName"></param>
        /// <returns></returns>
        public static long UploadPackage(string packageFullPath, string sourceName, string ApiKey, StopWatches timer)
        {
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            return InvokeNugetProcess(timer, string.Join(string.Empty, new string[] { PushCommandString, @"""" + packageFullPath + @"""", APIKeySwitchString, ApiKey, SourceSwitchString, sourceName }), out standardError, out standardOutput);
        }

        /// <summary>
        /// Uploads the given package to the specified source and returns the exit code.
        /// </summary>
        /// <param name="packageFullPath"></param>
        /// <param name="sourceName"></param>
        /// <returns></returns>
        //public static long UploadPackage(string packageFullPath, string sourceName, out string standardOutput, out string standardError,out int exitCode)
        //{
        //    return InvokeNugetProcess(string.Join(string.Empty, new string[] { PushCommandString, @"""" + packageFullPath + @"""", SourceSwitchString, sourceName }), out standardError, out standardOutput, out exitCode);
        //}

        public static long SearchPackage(StopWatches timer, string TestPackageName, string Version)
        {
            string uri = SearchAPIBase + "/query?q=%27" + TestPackageName + "%27&luceneQuery=false";
            Uri searchAPI = new Uri(uri);
            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            string JSONResult = client.GetStringAsync(searchAPI).Result;
            //JObject dataObj = JObject.Parse(JSONResult);
            SearchResult result = new SearchResult();
            result = new JavaScriptSerializer().Deserialize<SearchResult>(JSONResult);
            //PackageRegistration registration = new PackageRegistration(TestPackageName);
            //SearchDocument document = new SearchDocument(registration, Version);

            foreach (var document in result.data)
            {
                if (document.Version==Version && document.PackageRegistration.Id==TestPackageName)
                {
                    Console.WriteLine(document.PackageRegistration.Id + " " + document.Version);
                    timer.SearchTimeElapsed.Stop();
                    return timer.SearchTimeElapsed.ElapsedMilliseconds;

                }

            }

            return -1;
        }
    }

    public class StopWatches
    {
        public Stopwatch UploadTimeElapsed;
        public Stopwatch SearchTimeElapsed;
        public Stopwatch CatalogTimeElapsed;
    }
}
