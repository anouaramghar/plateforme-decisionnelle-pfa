using System;
using System.Collections.Generic;

namespace PlateformePFA.API.DTOs.ML
{
    public class ModelMetricsDto
    {
        public decimal? Auc { get; set; }
        public decimal? F1 { get; set; }
        public decimal? Precision { get; set; }
        public decimal? Recall { get; set; }
        public decimal? Mae { get; set; }
        public decimal? R2 { get; set; }
        public int NSamples { get; set; }
        public string ModelVersion { get; set; } = "unknown";
        public DateTime? TrainedAt { get; set; }
        public string Source { get; set; } = "metadata";
        public string DataSource { get; set; } = "unknown";
        public string SplitStrategy { get; set; } = "unknown";
        public List<double> TrainPeriods { get; set; } = new();
        public List<double> TestPeriods { get; set; } = new();
        public int NStudentsTrain { get; set; }
        public int NStudentsTest { get; set; }
    }

    public class MlMetricsDto
    {
        public ModelMetricsDto Risk { get; set; } = new();
        public ModelMetricsDto Forecast { get; set; } = new();

        // Root properties for backward compatibility
        public decimal Auc { get; set; }
        public decimal F1 { get; set; }
        public decimal Precision { get; set; }
        public decimal Recall { get; set; }
        public int NSamples { get; set; }
        public string ModelVersion { get; set; } = "unknown";
        public DateTime? TrainedAt { get; set; }
        public string Source { get; set; } = "metadata";
        public string DataSource { get; set; } = "unknown";
        public string SplitStrategy { get; set; } = "unknown";
    }
}
