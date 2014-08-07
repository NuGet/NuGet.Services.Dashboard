using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class LatencyAverageDetails
    {
        public string ScenarioName;
        public double AvgTimeTakenInMilliSeconds;
        public double HighestTime;
        public double LowestTime;

        public LatencyAverageDetails()
        {

        }

        public LatencyAverageDetails(string scenarioName, double avgTime, double highestTime, double lowestTime)
        {
            this.ScenarioName = scenarioName;
            this.AvgTimeTakenInMilliSeconds = avgTime;
            this.HighestTime = highestTime;
            this.LowestTime = lowestTime;
        }
    }
}