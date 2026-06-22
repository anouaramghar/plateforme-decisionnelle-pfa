namespace PlateformePFA.API.DTOs.Etudiants
{
    // One row of a student's merged activity timeline (signals + case events),
    // for the Student 360 workspace.
    public class TimelineItemDto
    {
        public DateTime Date { get; set; }
        public string Source { get; set; } = string.Empty; // "Signal" | "Case"
        public string Action { get; set; } = string.Empty; // SignalRaised | Created | StateChanged | ...
        public string? Label { get; set; }
        public int? RefId { get; set; } // alert id (Signal) or case id (Case)
    }
}
