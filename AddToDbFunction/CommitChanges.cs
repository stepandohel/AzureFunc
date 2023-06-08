using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddToDbFunction
{
    internal class CommitChanges
    {
        public string gitObjectType { get; set; }
        public string path { get; set; }
        public string isFolder { get; set; }
        public string url { get; set; }
        public string changeType { get; set; }
    }
}
