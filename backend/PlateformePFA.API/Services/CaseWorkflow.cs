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
        public const string Escalated      = "Escalated";
        public const string Resolved       = "Resolved";
        public const string Closed         = "Closed";

        public static readonly string[] All =
            { Open, InProgress, WaitingStudent, Monitoring, Escalated, Resolved, Closed };
    }

    /// <summary>
    /// Pure state-machine for intervention cases. No DB, no I/O — so it is
    /// unit-testable in isolation (see CaseWorkflowTests).
    /// </summary>
    public static class CaseWorkflow
    {
        private static readonly Dictionary<string, string[]> Allowed = new()
        {
            [CaseWorkflowState.Open]           = new[] { CaseWorkflowState.InProgress },
            [CaseWorkflowState.InProgress]     = new[] { CaseWorkflowState.WaitingStudent, CaseWorkflowState.Monitoring, CaseWorkflowState.Escalated, CaseWorkflowState.Resolved },
            [CaseWorkflowState.WaitingStudent] = new[] { CaseWorkflowState.InProgress, CaseWorkflowState.Monitoring, CaseWorkflowState.Escalated },
            [CaseWorkflowState.Monitoring]     = new[] { CaseWorkflowState.InProgress, CaseWorkflowState.Resolved },
            [CaseWorkflowState.Escalated]      = new[] { CaseWorkflowState.InProgress, CaseWorkflowState.Resolved },
            [CaseWorkflowState.Resolved]       = new[] { CaseWorkflowState.Closed, CaseWorkflowState.InProgress }, // → InProgress = reopen
            [CaseWorkflowState.Closed]         = new[] { CaseWorkflowState.InProgress },                            // reopen only
        };

        public readonly record struct CaseFacts(bool HasOwner, bool HasOutcome, bool HasReason, bool MonitoringComplete);

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

            if (to == CaseWorkflowState.Closed && !facts.MonitoringComplete)
                return (false, "La période de suivi n'est pas terminée ; clôture impossible avant la date de suivi.");

            // Reopening a resolved/closed case must record a reason.
            var isReopen = (from == CaseWorkflowState.Resolved || from == CaseWorkflowState.Closed)
                           && to == CaseWorkflowState.InProgress;
            if (isReopen && !facts.HasReason)
                return (false, "Une raison est requise pour rouvrir le cas.");

            return (true, null);
        }

        /// <summary>
        /// True when a case must auto-escalate: Critical priority, past its due
        /// date, and not already in a terminal/escalated state.
        /// </summary>
        public static bool IsCriticalOverdue(string etat, string priorite, DateTime? dueDate, DateTime now)
        {
            if (priorite != "Critical") return false;
            if (etat == CaseWorkflowState.Resolved
                || etat == CaseWorkflowState.Closed
                || etat == CaseWorkflowState.Escalated) return false;
            return dueDate.HasValue && dueDate.Value < now;
        }
    }
}
