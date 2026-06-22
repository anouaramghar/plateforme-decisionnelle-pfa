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
        public int EscalatedCases { get; set; }
        public int OverdueCases { get; set; }      // DueDate past, still open
        public int OverdueTasks { get; set; }      // incomplete tasks past DueDate
        public int FailedEmails { get; set; }
    }
}
