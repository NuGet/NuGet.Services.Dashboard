using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetDashboard.Models
{
    public class ThresholdViewModel
    {
        public string ID { get; set; }
        public string Type { get; set; }

        public int standard { get; set; }

        public int current { get; set; }

        public ThresholdViewModel(string ID, string Type, int standard, int current)
        {
            this.ID = ID;
            this.Type = Type;
            this.standard = standard;
            this.current = current;
        }

        public ThresholdViewModel() { }
    }
}