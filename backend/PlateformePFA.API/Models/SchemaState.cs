using System.ComponentModel.DataAnnotations;

namespace PlateformePFA.API.Models
{
    /// <summary>
    /// Mirrors the dbo.SchemaState table written at the very end of init.sql.
    /// A row with Component = 'core' and Version >= 1 means the full OLTP
    /// schema has been successfully initialised — used by the /ready health check.
    /// </summary>
    public class SchemaState
    {
        [Key]
        [MaxLength(50)]
        public string Component { get; set; } = string.Empty;

        public int Version { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
