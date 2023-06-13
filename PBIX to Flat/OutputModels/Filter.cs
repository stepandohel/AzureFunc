using System.Numerics;

namespace PBIX_to_Flat.OutputModels
{
    public class Filter
    {
        public int id { get; set; }
        public string? report_id { get; set; }
        public string? filter_level { get; set; }
        public string? page_id { get; set; }
        public string? page_name { get; set; }
        public string? visual_id { get; set; }
        public string? visual_type { get; set; }
        public string? table_name { get; set; }
        public string? column { get; set; }
        public string? filter_values { get; set; }
        public DateTime modified_date { get; set; }
        public string? path { get; set; }
        public string? is_deleted { get; set; }
        public DateTime? deleted_time { get; set; }
    }
}
