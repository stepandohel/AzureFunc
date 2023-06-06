using System;

namespace AddToDbFunction
{
    internal class ReportObject
    {
        public string reportName { get; set; }
        public byte[] reportBody { get; set; }
        public DateTime modified_date { get; set; }
        public string reportURL { get; set; }
        public bool is_Changed { get; set; }
        public bool is_New { get; set; }

    }
}
