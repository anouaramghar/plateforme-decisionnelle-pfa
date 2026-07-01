namespace PlateformePFA.API.DTOs.Dashboard
{
    // Intervention KPIs for the Admin/Responsable dashboards. Each number maps
    // to a filtered list the frontend links to ("dashboards link to actionable
    // work"): e.g. UnassignedCases → /cases?etat=Open&owner=none.
    public class InterventionDashboardDto
    {
        public int TriageQueue { get; set; }      // un-triaged signals waiting
        public int OpenCases { get; set; }         // not Resolved/Closed
        public int UnassignedCases { get; set; }   // open with no owner
        public int EscalatedCases { get; set; }    // derived: Critical + past due, still open
        public int OverdueCases { get; set; }      // DueDate past, still open
        public int FailedEmails { get; set; }

        // ── Outreach funnel ────────────────────────────────────────────────────
        // Counts at each stage of the action loop + derived percentages. Powers
        // the Responsable funnel cards.
        public int NeedsContact { get; set; }              // cases in Open
        public int EmailPrepared { get; set; }             // cases in InProgress
        public int MeetingsScheduled { get; set; }         // cases in WaitingStudent
        public int MeetingsHeld { get; set; }              // cases with MeetingAttendance=Held
        public int DuplicateCasesPrevented { get; set; }   // DuplicateCasePrevented audit rows
        public decimal ContactedEligiblePercent { get; set; }    // contacted / eligible high-risk students
        public decimal EmailDeliverySuccessPercent { get; set; } // Sent / (Sent + Failed)
        public decimal MeetingHeldPercent { get; set; }          // Held / scheduled meetings
        public decimal MedianHoursRiskToEmail { get; set; }      // alert→first Sent email median
    }
}
