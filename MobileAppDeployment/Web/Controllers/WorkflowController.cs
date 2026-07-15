using Microsoft.AspNetCore.Mvc;
using MobileAppDeployment.Core.Domain.Entities;
using MobileAppDeployment.Core.Interfaces.Infrastructure;

namespace MobileAppDeployment.Web.Controllers;

/// <summary>
/// MVC controller for workflow progress polling actions.
/// </summary>
/// <remarks>
/// Routes are pinned under <c>/AppDeployment/</c> to preserve existing public URLs.
/// </remarks>
[Route("AppDeployment")]
public class WorkflowController : Controller
{
    private readonly IWorkflowOrchestrationService _workflowOrchestration;

    /// <summary>
    /// Creates a new workflow controller instance.
    /// </summary>
    public WorkflowController(IWorkflowOrchestrationService workflowOrchestration)
    {
        _workflowOrchestration = workflowOrchestration;
    }

    /// <summary>
    /// Shows live progress while the base workflow is being triggered (with retries).
    /// </summary>
    [HttpGet]
    public IActionResult WorkflowProgress(string jobId)
    {
        WorkflowJobState? job = _workflowOrchestration.GetJob(jobId);
        if (job is null)
        {
            return NotFound();
        }

        return View("~/Views/AppDeployment/WorkflowProgress.cshtml", new WorkflowProgressViewModel
        {
            JobId = job.JobId,
            ClientName = job.ClientName,
            SavedMessage = job.SavedMessage
        });
    }

    /// <summary>
    /// JSON endpoint polled by the workflow progress UI.
    /// </summary>
    [HttpGet]
    public IActionResult WorkflowStatus(string jobId)
    {
        WorkflowJobState? job = _workflowOrchestration.GetJob(jobId);
        if (job is null)
        {
            return NotFound();
        }

        return Json(new
        {
            jobId = job.JobId,
            status = job.Status.ToString(),
            percentComplete = job.PercentComplete,
            currentMessage = job.CurrentMessage,
            attempt = job.Attempt,
            maxRetries = job.MaxRetries,
            errorMessage = job.ErrorMessage,
            isTerminal = job.IsTerminal,
            clientName = job.ClientName
        });
    }
}
