using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Globalization;

namespace NuGetDashboard.Utilities
{
    /// <summary>
    /// Utility class to convert C# DateTime to UNIX time stamp. This is required for pingdom APIs.
    /// </summary>
    public class DateTimeUtility
    {
        private static readonly DateTime UnixEpoch =
    new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime GetPacificTime()
        {
            //Hacky way to find Pacific time from UTC. Need to fix it using TimeZone and DateTimeOffSet.
            DateTime time = DateTime.Now;
                if(DateTime.Now.Equals(DateTime.UtcNow))
                    time = time.AddHours(-7);
            return time;
        }

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

        public static long GetLastMonthUnixTimestampSeconds()
        {
            return (long)(DateTime.UtcNow.Subtract(new TimeSpan(30, 0, 0, 0)) - UnixEpoch).TotalSeconds;
        }

        public static double GetSecondsForDays(int noOfDays)
        {
            double total = new TimeSpan(noOfDays, 0, 0, 0).TotalSeconds;
            return total;
        }

        public static DateTime DateTimeFromUnixTimestampSeconds(long seconds)
        {
            return UnixEpoch.AddSeconds(seconds);
        }

        public static string GetLastMonthName()
        {
            DateTimeFormatInfo dfi = new DateTimeFormatInfo();
            return dfi.GetMonthName(DateTime.Now.Month - 1);
        }

        public static int GetDaysInMonth(string month)
        {
            return DateTime.DaysInMonth(DateTime.Now.Year, GetMonthNumber(month));
        }

        public static int GetMonthNumber(string monthName)
        {
            int iMonthNo = Convert.ToDateTime("01-" + monthName + "-2011").Month;
            return iMonthNo;
        }
    }
}