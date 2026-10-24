using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.Partyflin.Models;
using Jellyfin.Plugin.Partyflin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Partyflin.Api;

/// <summary>
/// Gestion des votes, réservée aux administrateurs du serveur.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("Partyflin")]
public class PollsAdminController : ControllerBase
{
    private readonly PollStore _store;
    private readonly MovieResolver _resolver;

    public PollsAdminController(PollStore store, MovieResolver resolver)
    {
        _store = store;
        _resolver = resolver;
    }

    [HttpGet("Polls")]
    public ActionResult<IEnumerable<PollSummaryDto>> GetPolls()
    {
        return Ok(_store.GetAll().Select(ToSummary));
    }

    [HttpGet("Polls/{id}")]
    public ActionResult<PollSummaryDto> GetPoll([FromRoute] Guid id)
    {
        var poll = _store.GetById(id);
        return poll is null ? NotFound() : ToSummary(poll);
    }

    [HttpPost("Polls")]
    public ActionResult<PollSummaryDto> CreatePoll([FromBody] PollEditRequest request)
    {
        var error = Validate(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        return ToSummary(_store.Create(request));
    }

    [HttpPut("Polls/{id}")]
    public ActionResult<PollSummaryDto> UpdatePoll([FromRoute] Guid id, [FromBody] PollEditRequest request)
    {
        var error = Validate(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        var poll = _store.Update(id, request);
        return poll is null ? NotFound() : ToSummary(poll);
    }

    [HttpDelete("Polls/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult DeletePoll([FromRoute] Guid id)
    {
        return _store.Delete(id) ? NoContent() : NotFound();
    }

    [HttpGet("Polls/{id}/Results")]
    public ActionResult<ResultsDto> GetResults([FromRoute] Guid id)
    {
        var poll = _store.GetById(id);
        if (poll is null)
        {
            return NotFound();
        }

        return VoteController.BuildResults(poll, _resolver.Resolve(poll.Sources, poll.ExcludedItemIds));
    }

    /// <summary>
    /// Films obtenus à partir d'une liste de sources (aperçu avant enregistrement).
    /// </summary>
    [HttpPost("Preview")]
    public ActionResult<IEnumerable<MovieDto>> Preview([FromBody] PreviewRequest request)
    {
        return Ok(_resolver.Resolve(request.Sources).Select(MovieResolver.ToDto));
    }

    [HttpGet("Search")]
    public ActionResult<IEnumerable<SearchResultDto>> Search([FromQuery] PollSourceType type, [FromQuery] string? term)
    {
        return Ok(_resolver.Search(type, term));
    }

    private static string? Validate(PollEditRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "Le titre est obligatoire.";
        }

        if (request.Sources.Count == 0)
        {
            return "Ajoutez au moins une source de films.";
        }

        return null;
    }

    private PollSummaryDto ToSummary(Poll poll)
    {
        return new PollSummaryDto
        {
            Id = poll.Id,
            Token = poll.Token,
            Title = poll.Title,
            Description = poll.Description,
            CreatedAt = poll.CreatedAt,
            ClosesAt = poll.ClosesAt,
            IsClosed = poll.IsClosed,
            IsOpen = poll.IsOpen,
            MaxVotesPerVoter = poll.MaxVotesPerVoter,
            ResultsVisibility = poll.ResultsVisibility,
            Sources = poll.Sources,
            ExcludedItemIds = poll.ExcludedItemIds,
            MovieCount = _resolver.Resolve(poll.Sources, poll.ExcludedItemIds).Count,
            VoterCount = poll.Ballots.Count(b => b.ItemIds.Count > 0)
        };
    }
}
