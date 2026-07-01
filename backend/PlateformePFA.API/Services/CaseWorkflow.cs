namespace PlateformePFA.API.Services
{
    // Case states. Constants (not an enum) to match the string columns used
    // across the codebase and to keep them stable in the DB CHECK constraint.
    public static class CaseWorkflowState
    {
        public const string Open           = "Open";
        public const string InProgress     = "InProgress";
        public const string WaitingStudent = "WaitingStudent";
        public const string Monitoring     = "Monitoring";
        public const string Resolved       = "Resolved";
        public const string Closed         = "Closed";

        public static readonly string[] All =
            { Open, InProgress, WaitingStudent, Monitoring, Resolved, Closed };
    }

    /// <summary>
    /// Pure state-machine for intervention cases. No DB, no I/O — so it is
    /// unit-testable in isolation (see CaseWorkflowTests).
    /// </summary>
    public static class CaseWorkflow
    {
        // Escalation is no longer a state: "Critical and past due" is derived
        // from data the case already carries (see InterventionCase.EnRetard).
        private static readonly Dictionary<string, string[]> Allowed = new()
        {
            [CaseWorkflowState.Open]           = new[] { CaseWorkflowState.InProgress },
            [CaseWorkflowState.InProgress]     = new[] { CaseWorkflowState.WaitingStudent, CaseWorkflowState.Monitoring, CaseWorkflowState.Resolved },
            [CaseWorkflowState.WaitingStudent] = new[] { CaseWorkflowState.InProgress, CaseWorkflowState.Monitoring },
            [CaseWorkflowState.Monitoring]     = new[] { CaseWorkflowState.InProgress, CaseWorkflowState.Resolved },
            [CaseWorkflowState.Resolved]       = new[] { CaseWorkflowState.Closed, CaseWorkflowState.InProgress }, // → InProgress = reopen
            [CaseWorkflowState.Closed]         = new[] { CaseWorkflowState.InProgress },                            // reopen only
        };

        public readonly record struct CaseFacts(
            bool HasOwner,
            bool HasOutcome,
            bool HasReason,
            bool MonitoringComplete,
            // True when at least one outreach meeting has been recorded as Held
            // for this case. Required to resolve through the generic endpoint,
            // mirroring the dedicated outreach flow (RecordMeeting sets Resolved
            // only after attendance = Held).
            bool MeetingHeld = false);

        /// <returns>(true, null) if allowed; (false, reason) otherwise.</returns>
        public static (bool ok, string? error) CanTransition(string from, string to, CaseFacts facts)
        {
            if (!Allowed.TryGetValue(from, out var targets) || !targets.Contains(to))
                return (false, $"Transition {from} → {to} non autorisée.");

            // Guards from the plan.
            if (to == CaseWorkflowState.InProgress && !facts.HasOwner)
                return (false, "Un responsable doit être assigné avant de démarrer le cas.");

            if (to == CaseWorkflowState.Resolved && !facts.HasOutcome)
                return (false, "Un résultat et un résumé de résolution sont requis.");

            // Every generic transition into Resolved requires a held meeting.
            // Applying the invariant to the target (rather than one source
            // state) prevents bypasses such as InProgress -> Monitoring -> Resolved.
            if (to == CaseWorkflowState.Resolved && !facts.MeetingHeld)
                return (false, "L'entretien doit avoir été réalisé avant de résoudre ce cas.");

            if (to == CaseWorkflowState.Closed && !facts.MonitoringComplete)
                return (false, "La période de suivi n'est pas terminée ; clôture impossible avant la date de suivi.");

            // Reopening a resolved/closed case must record a reason.
            var isReopen = (from == CaseWorkflowState.Resolved || from == CaseWorkflowState.Closed)
                           && to == CaseWorkflowState.InProgress;
            if (isReopen && !facts.HasReason)
                return (false, "Une raison est requise pour rouvrir le cas.");

            return (true, null);
        }

    }
}
