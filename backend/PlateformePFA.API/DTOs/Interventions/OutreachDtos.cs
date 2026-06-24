using System.ComponentModel.DataAnnotations;

namespace PlateformePFA.API.DTOs.Interventions
{
    // Create or edit the single editable outreach email draft.
    public sealed class SaveOutreachDraftDto
    {
        [Required, MaxLength(300)] public string Subject { get; set; } = string.Empty;
        [Required, MaxLength(4000)] public string Body { get; set; } = string.Empty;
    }

    // Schedule the meeting and send the (already reviewed) draft exactly once.
    public sealed class ScheduleAndSendDto
    {
        public DateTime ScheduledFor { get; set; }
        [Required, MaxLength(200)] public string Location { get; set; } = string.Empty;
    }

    // Record the meeting outcome. Held requires HeldAt + Outcome + Summary.
    public sealed class RecordMeetingDto
    {
        [Required] public string Attendance { get; set; } = string.Empty; // Held | Absent | Cancelled
        public DateTime? HeldAt { get; set; }
        [MaxLength(40)] public string? Outcome { get; set; }
        [MaxLength(1000)] public string? Summary { get; set; }
    }
}
