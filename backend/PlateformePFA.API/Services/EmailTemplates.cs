using System.Collections.ObjectModel;

namespace PlateformePFA.API.Services
{
    public record EmailTemplate(string Id, string Nom, string Sujet, string Corps);

    /// <summary>
    /// Fixed French email templates. ponytail: hardcoded, not a DB-backed editor —
    /// a template-editing UI is speculative for the demo. Placeholders {{Prenom}},
    /// {{Nom}}, {{Matricule}} are substituted at send time; the rendered text is
    /// then stored on the CaseCommunication, so editing a template here never
    /// rewrites already-sent history.
    /// </summary>
    public static class EmailTemplates
    {
        public static readonly ReadOnlyDictionary<string, EmailTemplate> All =
            new(new Dictionary<string, EmailTemplate>
            {
                ["meeting_invitation"] = new("meeting_invitation", "Invitation à un entretien",
                    "Invitation à un entretien — ENIAD",
                    "Bonjour {{Prenom}} {{Nom}},\n\nNous souhaitons vous rencontrer pour faire le point sur votre parcours. Merci de vous présenter au bureau de la scolarité.\n\nCordialement,\nL'équipe pédagogique ENIAD"),

                ["absence_warning"] = new("absence_warning", "Avertissement d'absences",
                    "Avertissement — absences répétées",
                    "Bonjour {{Prenom}} {{Nom}},\n\nNous avons constaté un nombre élevé d'absences dans votre suivi. Merci de régulariser votre situation au plus vite.\n\nCordialement,\nL'équipe pédagogique ENIAD"),

                ["academic_warning"] = new("academic_warning", "Avertissement académique",
                    "Avertissement — résultats académiques",
                    "Bonjour {{Prenom}} {{Nom}},\n\nVos résultats récents nous préoccupent. Un accompagnement vous est proposé pour vous aider à progresser.\n\nCordialement,\nL'équipe pédagogique ENIAD"),

                ["intervention_followup"] = new("intervention_followup", "Suivi d'intervention",
                    "Suivi de votre accompagnement — ENIAD",
                    "Bonjour {{Prenom}} {{Nom}},\n\nNous faisons suite à notre échange concernant votre accompagnement. N'hésitez pas à nous contacter pour toute question.\n\nCordialement,\nL'équipe pédagogique ENIAD"),

                ["case_resolution"] = new("case_resolution", "Clôture de l'accompagnement",
                    "Clôture de votre accompagnement — ENIAD",
                    "Bonjour {{Prenom}} {{Nom}},\n\nVotre dossier d'accompagnement a été clôturé. Nous restons à votre disposition si besoin.\n\nCordialement,\nL'équipe pédagogique ENIAD"),
            });

        public static (string sujet, string corps) Render(EmailTemplate t, string prenom, string nom, string matricule)
        {
            string Fill(string s) => s
                .Replace("{{Prenom}}", prenom)
                .Replace("{{Nom}}", nom)
                .Replace("{{Matricule}}", matricule);
            return (Fill(t.Sujet), Fill(t.Corps));
        }
    }
}
