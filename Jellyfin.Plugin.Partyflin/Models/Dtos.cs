using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.Partyflin.Models;

public class PollEditRequest
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime? ClosesAt { get; set; }

    public bool IsClosed { get; set; }

    public int MaxVotesPerVoter { get; set; } = 3;

    public ResultsVisibility ResultsVisibility { get; set; } = ResultsVisibility.AfterVoting;

    public List<PollSource> Sources { get; set; } = new();

    public List<Guid> ExcludedItemIds { get; set; } = new();
}

public class PreviewRequest
{
    public List<PollSource> Sources { get; set; } = new();
}

public class PollSummaryDto
{
    public Guid Id { get; set; }

    public string Token { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? ClosesAt { get; set; }

    public bool IsClosed { get; set; }

    public bool IsOpen { get; set; }

    public int MaxVotesPerVoter { get; set; }

    public ResultsVisibility ResultsVisibility { get; set; }

    public List<PollSource> Sources { get; set; } = new();

    public List<Guid> ExcludedItemIds { get; set; } = new();

    public int MovieCount { get; set; }

    public int VoterCount { get; set; }
}

public class SearchResultDto
{
    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;
}

public class MovieDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? Year { get; set; }

    public string Overview { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }

    public IReadOnlyList<string> Genres { get; set; } = Array.Empty<string>();

    public float? CommunityRating { get; set; }

    public string OfficialRating { get; set; } = string.Empty;

    public bool HasPoster { get; set; }
}

public class ResultEntryDto
{
    public Guid ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? Year { get; set; }

    public int Votes { get; set; }

    public List<string> Voters { get; set; } = new();
}

public class ResultsDto
{
    public int VoterCount { get; set; }

    public List<ResultEntryDto> Entries { get; set; } = new();
}

public class VotePageDto
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime? ClosesAt { get; set; }

    public bool IsOpen { get; set; }

    public int MaxVotesPerVoter { get; set; }

    public ResultsVisibility ResultsVisibility { get; set; }

    public List<MovieDto> Movies { get; set; } = new();

    public string? MyName { get; set; }

    public List<Guid> MyVotes { get; set; } = new();

    /// <summary>Null si les résultats ne sont pas (encore) visibles pour ce votant.</summary>
    public ResultsDto? Results { get; set; }
}

public class BallotRequest
{
    public string VoterId { get; set; } = string.Empty;

    public string VoterName { get; set; } = string.Empty;

    public List<Guid> ItemIds { get; set; } = new();
}
