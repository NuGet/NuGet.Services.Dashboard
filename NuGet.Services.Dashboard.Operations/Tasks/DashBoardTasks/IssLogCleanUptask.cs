using System;
using System.IO;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    [Command("IssCleanUpTask", "clean up old iss log in current dir", AltName = "clean")]
    class IssLogCleanUptask : OpsTask
    {
        public override void ExecuteCommand()
        {
            try
            {
                var dirs = Directory.EnumerateDirectories(Environment.CurrentDirectory, "*.log");
                foreach(string dir in dirs)
                {
                    string date = dir.Substring(Environment.CurrentDirectory.Length+5, 8);
                    DateTime logdate = new DateTime();
                    logdate = DateTime.ParseExact(date, "yyMMddHH", null);
                    if (logdate < DateTime.UtcNow.AddHours(-48)) Directory.Delete(dir, true);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("clean up error: {0}", e.Message);
            }
        }
    }
}
