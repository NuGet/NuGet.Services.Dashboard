using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class OperationRequest
    {
        public string Operation;
        public int download;
    }

    public class agentRequest
    {
        public string key;
        public int value;

        public agentRequest(string Version, int download)
        {
            this.key = Version;
            this.value = download;
        }
    }

    public class VsRequest
    {
        public string key;
        public string value;

        public VsRequest(string VsVersion, string download)
        {
            this.key = VsVersion;
            this.value = download;
        }
    }
}
