using DotNet.Highcharts.Enums;
using DotNet.Highcharts.Helpers;
using DotNet.Highcharts.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetDashboard.Utilities
{
    /// <summary>
    /// Helper methods to get various chart objects - pie chart, bar chart and line chart for the given data.
    /// </summary>
    public class ChartingUtilities
    {
        public static DotNet.Highcharts.Highcharts GetBarChart(List<string> xValues, List<Object> yValues, string YAxisTitle, string chartTitle)
        {
            DotNet.Highcharts.Highcharts chart = new DotNet.Highcharts.Highcharts(chartTitle)
            .InitChart(new DotNet.Highcharts.Options.Chart { DefaultSeriesType = ChartTypes.Column })
            .SetPlotOptions(new PlotOptions
            {
                Column = new PlotOptionsColumn
                {
                    Stacking = Stackings.Normal,
                }
            });

            chart.SetXAxis(new XAxis
            {
                Categories = xValues.ToArray(),

            });
            chart.SetSeries(new DotNet.Highcharts.Options.Series
            {
                Data = new Data(yValues.ToArray()),
                Name = YAxisTitle
            });

            chart.SetTitle(new DotNet.Highcharts.Options.Title { Text = chartTitle });
            return chart;
        }

        public static DotNet.Highcharts.Highcharts GetLineChart(List<DotNet.Highcharts.Options.Series> seriesSet, List<string> xValues, string title,int dimensions=300)
        {
            DotNet.Highcharts.Highcharts chart = new DotNet.Highcharts.Highcharts(title);
            chart.InitChart(new Chart
            {
                Height = dimensions,
                Width = dimensions
            });

            chart.SetXAxis(new XAxis
            {
                Categories = xValues.ToArray()

            });
            chart.SetSeries(seriesSet.ToArray());
            chart.SetTitle(new DotNet.Highcharts.Options.Title { Text = title });
            return chart;
        }

        public static DotNet.Highcharts.Highcharts GetAreaChart(List<DotNet.Highcharts.Options.Series> seriesSet, List<string> xValues, string title)
        {
            DotNet.Highcharts.Highcharts chart = new DotNet.Highcharts.Highcharts(title);
            chart.InitChart(new Chart
            {
                Height = 300,
                Width = 300,
                DefaultSeriesType = ChartTypes.Area
            });

            chart.SetXAxis(new XAxis
            {
                Categories = xValues.ToArray()

            });
            chart.SetSeries(seriesSet.ToArray());
            chart.SetTitle(new DotNet.Highcharts.Options.Title { Text = title });
            return chart;
        }

        public static DotNet.Highcharts.Highcharts GetPieChart(Series seriesSet, string title)
        {
            DotNet.Highcharts.Highcharts chart = new DotNet.Highcharts.Highcharts(title);
            chart.InitChart(new Chart
            {
                Height = 250,
                Width = 250,
                DefaultSeriesType = ChartTypes.Pie
            });          
            chart.SetSeries(seriesSet);
            chart.SetTitle(new DotNet.Highcharts.Options.Title { Text = title });
            return chart;
        }

        public static DotNet.Highcharts.Highcharts GetLineChartFromBlobName(string blobName,string title =null,int dataPointCount=6, int chartDimensions=300)
        {            
            return GetLineChartFromBlobName(new string[] { blobName }, title, dataPointCount, chartDimensions);
        }

        /// <summary>
        /// Given the json blob name, this helper will return a line chart for the data in it.
        /// </summary>
        /// <param name="blobNames"></param>
        /// <param name="title">The title for the chart. If unspecified, the blobName will be used.</param>
        /// <param name="dataPointCount">The number of datapoints to show in the graph.</param>
        /// <param name="chartDimensions">The width/height of the chart</param>
        /// <returns></returns>
        public static DotNet.Highcharts.Highcharts GetLineChartFromBlobName(string[] blobNames, string title = null, int dataPointCount = 6, int chartDimensions = 300)
        {
            if (string.IsNullOrEmpty(title))
                title = blobNames[0];
            List<DotNet.Highcharts.Options.Series> seriesSet = new List<DotNet.Highcharts.Options.Series>();
            List<string> xValues = new List<string>();
            foreach (string blobName in blobNames)
            {  
               
                List<Object> yValues = new List<Object>();
                BlobStorageService.GetJsonDataFromBlob(blobName + ".json", out xValues, out yValues);
                //Retrive only the last N data points (To do : need to update this to retrieve the data for last N hours intead of N data points).
                if (xValues.Count > dataPointCount)
                {
                    xValues.RemoveRange(0, xValues.Count - dataPointCount);
                    yValues.RemoveRange(0, yValues.Count - dataPointCount);
                }

                seriesSet.Add(new DotNet.Highcharts.Options.Series
                {
                    Data = new Data(yValues.ToArray()),
                    Name = blobName
                });
            }

            DotNet.Highcharts.Highcharts chart = ChartingUtilities.GetLineChart(seriesSet, xValues, title, chartDimensions);
            return chart;
        }
    }
}