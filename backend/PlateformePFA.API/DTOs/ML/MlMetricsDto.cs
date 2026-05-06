namespace PlateformePFA.API.DTOs.ML
{
    /// <summary>
    /// Mirrors the JSON returned by the FastAPI service's /metrics endpoint.
    /// Properties stay PascalCase (so ASP.NET serializes them as camelCase to
    /// the frontend, matching every other DTO). The proxy controller handles
    /// the snake_case → PascalCase translation via SnakeCaseLower naming
    /// policy when deserializing the FastAPI response.
    /// </summary>
    public class MlMetricsDto
    {
        public decimal Auc { get; set; }
        public decimal F1 { get; set; }
        public decimal Precision { get; set; }
        public decimal Recall { get; set; }
        public int NSamples { get; set; }
        public string ModelVersion { get; set; } = "unknown";
        public DateTime? TrainedAt { get; set; }

        // "computed" when AUC came from a live evaluation on eval_set.parquet,
        // "metadata" when we fell back to the stored snapshot in metadata.json.
        public string Source { get; set; } = "metadata";
    }
}
