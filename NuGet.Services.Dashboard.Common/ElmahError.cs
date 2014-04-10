using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class ElmahError
    {
        public string Error;
        public int Occurecnes;
        public DateTime FirstReported;
        public DateTime LastReported;
        public string Link;
        public string Detail;
        public int Severity;

        public ElmahError(string error,int occurences,DateTime firstReported,DateTime lastReported, string link, string detail,int severity)
        {
            this.Error = error;
            this.Occurecnes = occurences;
            this.FirstReported = firstReported;
            this.LastReported = lastReported;
            this.Link = link;
            this.Detail = detail;
            this.Severity = severity;

        }
        public ElmahError()
        {

        }

    }
}
