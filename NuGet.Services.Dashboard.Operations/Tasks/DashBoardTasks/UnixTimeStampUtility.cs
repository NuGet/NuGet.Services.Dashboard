using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace NuGetGallery.Operations
{
    /// <summary>
    /// Helper class to convert C# DateTime to UNIX time stamp.
    /// </summary>
    public class UnixTimeStampUtility
    {
        private static readonly DateTime UnixEpoch =
  new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long GetCurrentUnixTimestampMillis()
        {
            return (long)(DateTime.UtcNow - UnixEpoch).TotalMilliseconds;
        }

        public static DateTime DateTimeFromUnixTimestampMillis(long millis)
        {
            return UnixEpoch.AddMilliseconds(millis);
        }

        public static long GetCurrentUnixTimestampSeconds()
        {
            return (long)(DateTime.UtcNow - UnixEpoch).TotalSeconds;
        }

        public static long GetUnixTimestampSeconds(DateTime time)
        {
            return (long)(time - UnixEpoch).TotalSeconds;
        }

        public static long GetLastMonthUnixTimestampSeconds()
        {
            return (long)(DateTime.UtcNow.Subtract(new TimeSpan(30, 0, 0, 0)) - UnixEpoch).TotalSeconds;
        }

        public static long GetLastWeekUnixTimestampSeconds()
        {
            return (long)(DateTime.UtcNow.Subtract(new TimeSpan(7, 0, 0, 0)) - UnixEpoch).TotalSeconds;
        }

        public static long GetSecondsForDays(int noOfDays)
        {
            double total = new TimeSpan(noOfDays, 0, 0, 0).TotalSeconds;
            return (long)total;
        }

        public static DateTime DateTimeFromUnixTimestampSeconds(long seconds)
        {
            return UnixEpoch.AddSeconds(seconds);
        }

        public static int GetDaysInMonth(string month)
        {
            return DateTime.DaysInMonth(DateTime.Now.Year, GetMonthNumber(month));
        }

        public static string GetMonthName(int Month)
        {
            DateTimeFormatInfo dfi = new DateTimeFormatInfo();
            string monthName = dfi.GetAbbreviatedMonthName(Month);           
            return monthName;
        }

        public static int GetMonthNumber(string monthName)
        {            
            int iMonthNo = Convert.ToDateTime("01-" + monthName + "-2011").Month;
            return iMonthNo;
        }
    }
}
