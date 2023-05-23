namespace PBIX_to_Flat.OutputModels
{
    public class LocalMeasure
    {
        public string? report_id { get; set; }
        public string? table_name { get; set; }
        public string? measure_name { get; set; }
        public string? DAX_definition { get; set; }
        public int id { get; set; }
    }
}
