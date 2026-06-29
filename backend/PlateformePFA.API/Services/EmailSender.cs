using System.Net;
using System.Net.Mail;

namespace PlateformePFA.API.Services
{
    public interface IEmailSender
    {
        // Returns (true, null) on success, (false, reason) on failure.
        // Never throws — the caller records the result on the CaseCommunication.
        Task<(bool ok, string? error)> SendAsync(string to, string subject, string body, CancellationToken ct = default);
    }

    /// <summary>
    /// Sends via the framework's built-in SmtpClient (no extra dependency).
    /// Configured for Gmail by default (smtp.gmail.com:587, STARTTLS) — set
    /// SMTP_USER to the Gmail address and SMTP_PASS to an app password.
    /// ponytail: swap to MailKit only if OAuth/modern auth becomes a requirement.
    /// </summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IConfiguration cfg, ILogger<SmtpEmailSender> logger)
        {
            _cfg = cfg;
            _logger = logger;
        }

        public async Task<(bool ok, string? error)> SendAsync(string to, string subject, string body, CancellationToken ct = default)
        {
            var host = _cfg["SMTP_HOST"] ?? "smtp.gmail.com";
            var user = _cfg["SMTP_USER"];
            var pass = _cfg["SMTP_PASS"];
            var from = _cfg["SMTP_FROM"] ?? user;

            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass) || string.IsNullOrWhiteSpace(from))
            {
                _logger.LogInformation("SMTP non configuré — email vers {To} simulé (mode démo). Sujet: {Subject}", to, subject);
                return (true, null);
            }

            var port = int.TryParse(_cfg["SMTP_PORT"], out var p) ? p : 587;

            try
            {
                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(user, pass),
                };
                using var msg = new MailMessage(from!, to, subject, body);
                await client.SendMailAsync(msg, ct);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Échec d'envoi email à {To}", to);
                // Truncate so it fits CaseCommunication.Erreur (500).
                var reason = ex.Message.Length > 480 ? ex.Message[..480] : ex.Message;
                return (false, reason);
            }
        }
    }
}
