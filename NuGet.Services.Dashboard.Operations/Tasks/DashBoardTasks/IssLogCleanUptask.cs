using System;
using System.IO;

namespace NuGetGallery.Operations.Tasks.DashBoardTasks
{
    [Command("IssCleanUpTask", "clean up old iss log in current dir", AltName = "clean")]
    class IssLogCleanUptask : OpsTask
    {
        public override void ExecuteCommand()
        {
            var dirs = Directory.EnumerateDirectories(Environment.CurrentDirectory, "*.log");
            foreach (string dir in dirs)
            {
                try
                {
                    string date = RemoveLetter(dir.Substring(Environment.CurrentDirectory.Length));
                    DateTime logdate = new DateTime();
                    logdate = DateTime.ParseExact(date, "yyMMddHH", null);
                    if (logdate < DateTime.UtcNow.AddHours(-48)) Directory.Delete(dir, true);
                }
                catch (Exception e)
                {
                    Console.WriteLine("clean up error: {0}", e.Message);
                }
            }
        }
        private string RemoveLetter(string src)
        {
            if (src.Equals("")) return src;
            if (char.IsDigit(src[0])) return src[0] + RemoveLetter(src.Substring(1));
            else return RemoveLetter(src.Substring(1));
        }
    }
}
 

