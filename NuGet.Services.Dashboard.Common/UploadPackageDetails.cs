using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class UploadPackageDetails
    {
        public DateTime now;
        public long timeElapsed;
        public int exitCode;

        public UploadPackageDetails()
        {

        }

        public UploadPackageDetails(DateTime _now, long _timeElapsed, int _exitCode)
        {
            this.now = _now;
            this.timeElapsed = _timeElapsed;
            this.exitCode = _exitCode;
        }
    }
}
