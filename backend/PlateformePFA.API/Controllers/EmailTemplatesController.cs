using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlateformePFA.API.Services;

namespace PlateformePFA.API.Controllers
{
    // Read-only catalogue of the fixed email templates, so the frontend can
    // show a picker and preview before staff confirm a send.
    [Authorize(Roles = "Admin,Responsable")]
    [Route("api/email-templates")]
    [ApiController]
    public class EmailTemplatesController : ControllerBase
    {
        [HttpGet]
        public ActionResult<IEnumerable<EmailTemplate>> GetTemplates()
            => EmailTemplates.All.Values.ToList();
    }
}
