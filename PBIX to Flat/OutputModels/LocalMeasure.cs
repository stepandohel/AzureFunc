namespace PBIX_to_Flat.OutputModels
{
    public class LocalMeasure
    {
        public string? ReportIdentifier { get; set; }
        public string? TableName { get; set; }
        public string? MeasureName { get; set; }
        public string? DAXDefinition { get; set; }
    }
}
