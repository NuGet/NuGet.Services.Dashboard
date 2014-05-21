using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Text.RegularExpressions;

namespace NuGetGallery.Operations
{
    public static class Util
    {
        public const byte OnlineState = 0;
        private static readonly Regex BackupNameFormat = new Regex(@"^(?<name>.+)_(?<timestamp>\d{4}[A-Za-z]{3}\d{2}_\d{4})Z$",RegexOptions.IgnoreCase); // Backup_2013Apr12_1452Z
        public static string GetDbName(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            return connectionStringBuilder.InitialCatalog;
        }
        public static string GetMasterConnectionString(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
            return connectionStringBuilder.ToString();
        }

        public static DateTime GetDateTimeFromTimestamp(string timestamp)
        {
            DateTime result;
            if (DateTime.TryParseExact(timestamp, "yyyyMMddHHmmss", CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
            {
                return result;
            }
            else if (DateTime.TryParseExact(timestamp, "yyyyMMMdd_HHmmZ", CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
            {
                return result;
            }
            return DateTime.MinValue;
        }

        public static Db GetLastBackup(SqlExecutor dbExecutor, string backupNamePrefix)
        {
            var allBackups = dbExecutor.Query<Db>(
                "SELECT name, state FROM sys.databases WHERE name LIKE '" + backupNamePrefix + "%' AND state = @state",
                new { state = OnlineState });        
            var orderedBackups = from db in allBackups
                                 let t = ParseNewTimestamp(BackupNameFormat.Match(db.Name).Groups["timestamp"].Value)
                                 where t != null
                                 orderby t descending
                                 select db;

            return orderedBackups.FirstOrDefault();
        }

        public static DateTime GetLastBackupTime(SqlExecutor dbExecutor, string backupNamePrefix)
        {
            var lastBackup = GetLastBackup(dbExecutor, backupNamePrefix);

            if (lastBackup == null)
                return DateTime.MinValue;

            var timestamp = lastBackup.Name.Substring(backupNamePrefix.Length);

            return GetDateTimeFromTimestamp(timestamp);
        }

        private static DateTimeOffset ParseNewTimestamp(string timestamp)
        {
            return new DateTimeOffset(
                DateTime.ParseExact(timestamp, "yyyyMMMdd_HHmm", CultureInfo.CurrentCulture),
                TimeSpan.Zero);
        }
    }

    public class Db
    {
        public string Name { get; set; }
        public byte State { get; set; }
    }
}
