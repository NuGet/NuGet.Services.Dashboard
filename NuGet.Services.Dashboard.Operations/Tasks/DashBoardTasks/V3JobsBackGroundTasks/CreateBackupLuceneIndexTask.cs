using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks.V3JobsBackGroundTasks
{
    [Command("CreateBackupLuceneIndexTask", "Creates a back up of Lucene Index and checks the health of all back-ups", AltName = "cblit")]
    public class CreateBackupLuceneIndexTask : OpsTask
    {
        [Option("StorageName", AltName = "sn")]
        public string StorageName { get; set; }

        [Option("StorageKey", AltName = "sk")]
        public string StorageKey { get; set; }

        [Option("SourceContainer", AltName = "sc")]
        public string SourceContainerName { get; set; }

        [Option("CatalogRootUrl", AltName = "cru")]
        public string CatalogRootUrl { get; set; }

        [Option("RegistrationCursorUrl", AltName = "rcu")]
        public string RegistrationCursorUrl { get; set; }

        [Option("NGExecutableFullPath", AltName = "ng")]
        public string NGExecutableFullPath { get; set; }

        string destContainerName = string.Empty;

        public override void ExecuteCommand()
        {
            destContainerName = "v3-lucene0-automatedbackup-" + String.Format(DateTime.Now.ToString("MMddhh"));
            UpdateSourceIndex();
            CreateBackUpIndex();
            CheckBackedUpIndex();
        }

        private void CreateBackUpIndex()
        {
            CreateBackUpContainer();
            string argument = "copylucene -srcDirectoryType Azure -srcStorageAccountName " + StorageName + " -srcStorageKeyValue " + StorageKey + " -srcStorageContainer " +  SourceContainerName + " -destDirectoryType Azure -destStorageAccountName " + StorageName + " -destStorageKeyValue " + StorageKey + " -destStorageContainer " + destContainerName ;
            string standardError = string.Empty;
            string standardOutput = string.Empty;            
            int exitCode = InvokeNgProcess(argument, out standardError, out standardOutput);
            if (exitCode != 0)
                new SendAlertMailTask
                {
                    AlertSubject = string.Format("Unable to create automated backup of Lucene Index {0}", SourceContainerName),
                    Details = String.Format("Ng process output from CopyLucene Job: {1}, Ng process error : {2}", SourceContainerName, standardOutput, standardError),
                    AlertName = string.Format("Unable to create automated backup of Lucene Index {0}", SourceContainerName),
                    Component = "V3LuceneIndex AutomatedBackup",
                    Level = "Error"
                }.ExecuteCommand();
        }

        private void  CreateBackUpContainer()
        {
            //Maintain 5 backups at any point of time.  
            Microsoft.WindowsAzure.Storage.Auth.StorageCredentials scr = new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials(StorageName,StorageKey);
            CloudStorageAccount csa = new CloudStorageAccount(scr, false);
            CloudBlobClient blobClient = csa.CreateCloudBlobClient();
            IEnumerable<CloudBlobContainer> backupContainers = blobClient.ListContainers("v3-lucene0-automatedbackup-");
            if( backupContainers.Count() >= 5)
            {
                backupContainers.OrderBy(p => p.Properties.LastModified).ToList()[0].DeleteIfExistsAsync();
            }
            CloudBlobContainer destContainer = blobClient.GetContainerReference(destContainerName);
            //Delete and recreate.This is to make sure that rerunning creates a new container from scratch.
            destContainer.DeleteIfExists();
            destContainer.Create();            
        }

        private void UpdateSourceIndex()
        {
            //Update the source index before backing it up.
            string argument = @" catalog2lucene -source " + CatalogRootUrl + " -luceneDirectoryType azure " + " -luceneStorageAccountName " + StorageName + " -luceneStorageKeyValue " + StorageKey + " -luceneStorageContainer " + SourceContainerName + " -registration " + RegistrationCursorUrl + " -verbose true -interval 30";
            string standardError = string.Empty;
            string standardOutput = string.Empty;
            int exitCode = InvokeNgProcess(argument,out standardError,out standardOutput);
            if(exitCode != 0)
                new SendAlertMailTask
                {
                    AlertSubject = string.Format("Catalog2Lucene job against {0} Lucene Index for backups failed",SourceContainerName),
                    Details = String.Format("Unable to update the index @ {0} before taking backups. Ng process output: {1}, Ng process error : {2}",SourceContainerName,standardOutput,standardError),
                    AlertName = string.Format("Catalog2Lucene job against {0} Lucene Index for backups failed", SourceContainerName),
                    Component = "V3LuceneIndex AutomatedBackup",
                    Level = "Error"
                }.ExecuteCommand();
        }

        private void CheckBackedUpIndex()
        {
            string argument = "checklucene -luceneDirectoryType azure -luceneStorageAccountName " + StorageName + " -luceneStorageKeyValue " + StorageKey + " -luceneStorageContainer " + destContainerName;
            string standardError = string.Empty;
            string standardOutput = string.Empty;
            int exitCode = InvokeNgProcess(argument, out standardError, out standardOutput);
            if (exitCode != 0)
                new SendAlertMailTask
                {
                    AlertSubject = string.Format("Automated back up for lucene index @ {0} not in good state", destContainerName),
                    Details = String.Format("Ng process output from check lucene: {1}, Ng process error : {2}", SourceContainerName, standardOutput, standardError),
                    AlertName = string.Format("Unable to create automated backup of Lucene Index {0}", SourceContainerName),
                    Component = "V3LuceneIndex AutomatedBackup",
                    Level = "Error"
                }.ExecuteCommand();
        }

        //pushes the package to the source specified (Source) and returns the time elapsed in milliseconds to upload the package, or -1 if upload fails
        private int InvokeNgProcess(string arguments,out string standardError, out string standardOutput)
        {
            Process ngProcess = new Process();
            string pathToNugetExe = Path.Combine(NGExecutableFullPath);
            ProcessStartInfo ngProcessStartInfo = new ProcessStartInfo(pathToNugetExe);
            ngProcessStartInfo.Arguments = arguments;
            ngProcessStartInfo.RedirectStandardError = true;
            ngProcessStartInfo.RedirectStandardOutput = true;
            ngProcessStartInfo.RedirectStandardInput = true;
            ngProcessStartInfo.UseShellExecute = false;
            ngProcessStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            ngProcessStartInfo.CreateNoWindow = true;
            ngProcess.StartInfo = ngProcessStartInfo;
            ngProcess.Start();
            standardError = ngProcess.StandardError.ReadToEnd();
            Console.WriteLine(standardError);
            standardOutput = ngProcess.StandardOutput.ReadToEnd();
            Console.WriteLine(standardOutput);
            ngProcess.WaitForExit();            
            return ngProcess.ExitCode;
        }
    }
}
