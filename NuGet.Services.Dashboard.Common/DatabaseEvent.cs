using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{    public class DatabaseEvent
    {
        public DateTime start_time;
        public DateTime end_time;
        public string event_type;
        public int event_count;
        public string description;
    }
}
