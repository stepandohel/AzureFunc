namespace PBIX_to_Flat.OutputModels
{
    public class Visual
    {
        public string? report_id { get; set; }
        public string? page_id { get; set; }
        public string? page_name { get; set; }
        public string? visual_id { get; set; }
        public string? visual_type { get; set; }
        public string? table_name { get; set; }
        public string? object_name { get; set; }
        public int id { get; set; }
        public DateTime modified_date { get; set; }
    }
}
