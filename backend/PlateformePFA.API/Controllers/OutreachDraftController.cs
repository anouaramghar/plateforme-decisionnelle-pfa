using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlateformePFA.API.Services;

namespace PlateformePFA.API.Controllers
{
    [Authorize(Roles = "Admin,Responsable")]
    [Route("api/outreach")]
    [ApiController]
    public class OutreachDraftController : ControllerBase
    {
        private readonly ILogger<OutreachDraftController> _logger;

        public OutreachDraftController(ILogger<OutreachDraftController> logger)
        {
            _logger = logger;
        }

        public record OutreachDraftInput(
            string FirstName,
            string ConcernSummary,
            string ScheduledFor,
            string Location);

        public record OutreachDraftOutput(string Subject, string Body);

        [HttpPost("draft")]
        public ActionResult<OutreachDraftOutput> GenerateDraft([FromBody] OutreachDraftInput input)
        {
            if (string.IsNullOrWhiteSpace(input.FirstName))
                return BadRequest(new { message = "Le prénom est requis." });

            var template = EmailTemplates.All["meeting_outreach"];
            var dt = DateTime.Parse(input.ScheduledFor, null, DateTimeStyles.RoundtripKind);
            var date = dt.ToString("dd/MM/yyyy");
            var time = dt.ToString("HH:mm");
            var (subject, body) = EmailTemplates.RenderMeeting(
                template, input.FirstName, "", date, time, input.Location);

            _logger.LogInformation("Draft généré pour {FirstName}", input.FirstName);
            return Ok(new OutreachDraftOutput(subject, body));
        }
    }
}
