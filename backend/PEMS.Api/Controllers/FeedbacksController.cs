using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Feedbacks.Commands.SubmitVisitFeedback;
using PEMS.Application.Feedbacks.Queries.GetMyHostFeedback;
using PEMS.Application.Feedbacks.Queries.GetPendingFeedbackNotifications;
using PEMS.Application.Feedbacks.Queries.GetVisitFeedbackTargets;
using PEMS.Application.Feedbacks.Queries.GetVisitorFeedback;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FeedbacksController : ControllerBase
    {
        private readonly IMediator _mediator;
        public FeedbacksController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<IActionResult> GetFeedbacks([FromQuery] PEMS.Application.Feedbacks.Queries.SearchAndFilterFeedback.SearchAndFilterFeedbackQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("visit-summary")]
        public async Task<IActionResult> GetVisitSummary([FromQuery] PEMS.Application.Feedbacks.Queries.ViewFeedbackSummary.ViewFeedbackSummaryQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        // ── Feedback rule mới (v10): Visitor đánh giá chung; Host đánh giá participant + logistics ──

        // Feedback screen data: real targets the current user may rate on this campus instance.
        [HttpGet("visit-instances/{visitInstanceId}/targets")]
        public async Task<IActionResult> GetVisitFeedbackTargets(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetVisitFeedbackTargetsQuery(visitInstanceId), cancellationToken);
            return Ok(result);
        }

        // Batch submit: rating 1..5 required per item, comment optional. Duplicate target → 409.
        [HttpPost("visit-instances/{visitInstanceId}")]
        public async Task<IActionResult> SubmitVisitFeedback(
            ulong visitInstanceId, [FromBody] SubmitVisitFeedbackBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new SubmitVisitFeedbackCommand(visitInstanceId, body.Items ?? new List<SubmitVisitFeedbackItem>()),
                cancellationToken);
            return Ok(result);
        }

        public sealed class SubmitVisitFeedbackBody
        {
            public List<SubmitVisitFeedbackItem>? Items { get; set; }
        }

        // Dynamic "Bạn hãy đánh giá đoàn" reminders (bell) + submitted flags for the visit list.
        // Nothing is written to `notifications`, so reminders never duplicate.
        [HttpGet("my-pending")]
        public async Task<IActionResult> GetMyPendingFeedback(CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetPendingFeedbackNotificationsQuery(), cancellationToken);
            return Ok(result);
        }

        // Backs the "Host feedback về bạn" notification modal (OPEN_HOST_FEEDBACK_MODAL). Always
        // resolves to the caller's own feedback server-side — never accepts a targetUserId.
        [HttpGet("my-host-feedback/{visitInstanceId}")]
        public async Task<IActionResult> GetMyHostFeedback(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetMyHostFeedbackQuery(visitInstanceId), cancellationToken);
            return Ok(result);
        }

        // Backs the "Visitor đã gửi đánh giá" notification modal (OPEN_VISITOR_FEEDBACK_MODAL).
        // Only the current Host of the instance or a Staff Leader of its campus may view (checked
        // server-side in the handler) — same audience the notification itself was sent to.
        [HttpGet("visitor-feedback/{visitInstanceId}")]
        public async Task<IActionResult> GetVisitorFeedback(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetVisitorFeedbackQuery(visitInstanceId), cancellationToken);
            return Ok(result);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetFeedbackDetail(ulong id)
        {
            // Dummy implementation for now to prevent 404
            return Ok(new { });
        }

        [HttpGet("visit-summary/{visitRequestId}")]
        public async Task<IActionResult> GetVisitSummaryDetail(ulong visitRequestId)
        {
            // Dummy implementation for now to prevent 404
            return Ok(new { });
        }

        [HttpGet("visit-summary/{visitRequestId}/instances/{visitInstanceId}")]
        public async Task<IActionResult> GetVisitInstanceSummaryDetail(ulong visitRequestId, ulong visitInstanceId)
        {
            // Dummy implementation for now to prevent 404
            return Ok(new { });
        }

    }
}
