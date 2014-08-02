using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Dashboard.Common
{
    public class SearchDocument
    {
        public string Version;
        public PackageRegistration PackageRegistration;
        public SearchDocument()
        {

        }

        public SearchDocument(PackageRegistration _registration, string _version)
        {
            this.PackageRegistration = _registration;
            this.Version = _version;
        }
    }

    public class PackageRegistration
    {
        public string Id;
        public PackageRegistration()
        {

        }

        public PackageRegistration(string Id)
        {
            this.Id = Id;
        }
    }

    public class SearchResult:IEnumerator,IEnumerable
    {
        public List<SearchDocument> data = new List<SearchDocument>();
        int position = -1;
        public SearchResult()
        {

        }

        public IEnumerator GetEnumerator()
        {
            return (IEnumerator)this;
        }

        //IEnumerator
        public bool MoveNext()
        {
            if (position < data.Count)
            {
                position++;
                return true;
            }
            else
            {
                return false;
            }
        }

        //IEnumerable
        public void Reset()
        { position = -1; }

        //IEnumerable
        public object Current
        {
            get { return data[position]; }
        }

    }

 
}
