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

namespace NuGetGallery.Operations
{
    [Command("CreateE2ELatencyReportTask", "Logs time take to upload package to various services", AltName = "ce2elrt")]
    public class CreateE2ELatencyReportTask : StorageTask
    {
        [Option("Source", AltName = "src")]
        public string Source { get; set; }

        [Option("ApiKey", AltName = "akey")]
        public string ApiKey { get; set; }

        internal static string NugetExePath = @"NuGet.exe";
        internal static string PushCommandString = " push ";
        internal static string SourceSwitchString = " -Source ";
        internal static string APIKeySwitchString = " -ApiKey ";

        public override void ExecuteCommand()
        {
            string today=DateTime.Today.ToString("d");
            DateTime now = DateTime.Now;
            string file = Path.Combine(Environment.CurrentDirectory, "DashboardUploadTestOriginal.nupkg");
            string newPackage = GetNewPackage(file);
            Console.WriteLine("Pushing :{0}", newPackage);
            int exitCode = 0;
            long timeElapsed=UploadPackage(newPackage, Source, ApiKey, out exitCode);

           
            //File.Copy(file, Path.Combine(Environment.CurrentDirectory, "DashboardUploadTestOriginal.nupkg"));
            //File.Delete(newPackage);
            File.Delete("backup");
            UploadPackageDetails entry = new UploadPackageDetails(now, timeElapsed, exitCode);
            var json = new JavaScriptSerializer().Serialize(entry);
            Console.WriteLine(DateTime.Today.ToString("d"));
            ReportHelpers.AppendDatatoBlob(StorageAccount, ("UploadPackageTimeElapsed" + today + ".json"), new Tuple<string, string>(string.Format("{0:HH-mm}", DateTime.Now), timeElapsed.ToString()), 48, ContainerName);
        }

        

        public static string GetNewPackage(string fileName)
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

                string newVersion = (++versionMajor).ToString()+"."+packageVersionMinor+"."+packageVersionBuild;
                string nuspecContent = File.ReadAllText(extractedfile);
                string updatedContent = nuspecContent.Replace(packageVersionMajor + "." + packageVersionMinor + "."+packageVersionBuild, newVersion);
                StreamWriter sw = new StreamWriter(newFile);
                sw.Write(updatedContent);
                sw.Flush();
                sw.Close();

                zipFile.RemoveEntry(entry);
                File.Replace(newFile, extractedfile, "backup", true);
                zipFile.AddFile(extractedfile, ".");
                //string newfileName =Path.Combine(Environment.CurrentDirectory, packageId + ".nupkg");
                zipFile.Save(fileName);
                Console.WriteLine("Here");
                //File.Delete(Path.Combine(Environment.CurrentDirectory, "DashboardUploadTestOriginal.nupkg"));
                //File.Copy(newfileName, fileName);
                //File.Delete(Path.Combine(Environment.CurrentDirectory, "DashboardUploadTest.nupkg"));
                return fileName;
            }
        }

        public static long InvokeNugetProcess(string arguments, out string standardError, out string standardOutput,out int exitCode, string WorkingDir = null)
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
            Stopwatch timeElapsed = Stopwatch.StartNew();
            timeElapsed.Start();
            standardError = nugetProcess.StandardError.ReadToEnd();
            standardOutput = nugetProcess.StandardOutput.ReadToEnd();
            //     Console.WriteLine(standardError);
            //      Console.WriteLine(standardOutput);
            nugetProcess.WaitForExit();
            timeElapsed.Stop();
            if (nugetProcess.ExitCode == 0)
            {
                exitCode = nugetProcess.ExitCode;
                return timeElapsed.ElapsedMilliseconds;// I changed the return types to long from int 
            }

            else
            {
                exitCode = nugetProcess.ExitCode;
                return -1;
            }
        }

        /// <summary>
        /// Uploads the given package to the specified source and returns the exit code.
        /// </summary>
        /// <param name="packageFullPath"></param>
        /// <param name="sourceName"></param>
        /// <returns></returns>
        public static long UploadPackage(string packageFullPath, string sourceName, string ApiKey, out int exitCode)
        {
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            return InvokeNugetProcess(string.Join(string.Empty, new string[] { PushCommandString, @"""" + packageFullPath + @"""", APIKeySwitchString, ApiKey, SourceSwitchString, sourceName }), out standardError, out standardOutput, out exitCode);
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

    }
}
