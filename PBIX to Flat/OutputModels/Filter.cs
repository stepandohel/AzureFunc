namespace PBIX_to_Flat.OutputModels
{
    public class Filter
    {
        public string? ReportIdentifier { get; set; }
        public string? FilterLevel { get; set; }
        public string? PageId { get; set; }
        public string? PageName { get; set; }
        public string? VisualId { get; set; }
        public string? VisualType { get; set; }
        public string? TableName { get; set; }
        public string? Column { get; set; }
        public string? FilterValues { get; set; }
    }
}
