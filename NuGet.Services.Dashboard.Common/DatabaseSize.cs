using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class DatabaseSize
    {
        public string DBName;
        public int SizeInMb;
        public string Edition;
        public Int64 MaxSizeInMb;
        public DatabaseSize()
        {

        }

        public DatabaseSize(string dbName,int sizeInMb, Int64 maxSize, string edition)
        {
            this.DBName = dbName;
            this.SizeInMb = sizeInMb;
            this.MaxSizeInMb = maxSize;
            this.Edition = edition;
        }
    }
}
