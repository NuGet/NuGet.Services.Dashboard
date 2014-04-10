using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class DatabaseRequest
    {
        public string text;
        public DateTime start_time;      
        public string Status;
        public string Command;
        public string Wait_Type;
        public int wait_time;
    }
}
