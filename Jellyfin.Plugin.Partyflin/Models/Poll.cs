using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Partyflin.Models;

/// <summary>
/// Origine des films proposés au vote. Les sources d'un vote sont cumulées.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PollSourceType
{
    /// <summary>Tous les films d'un genre (Value = nom du genre).</summary>
    Genre,

    /// <summary>Tous les films d'une bibliothèque (Value = id de la bibliothèque).</summary>
    Library,

    /// <summary>Les films d'une playlist créée manuellement (Value = id de la playlist).</summary>
    Playlist,

    /// <summary>Les films d'une collection (Value = id de la collection).</summary>
    Collection,

    /// <summary>Un film individuel (Value = id du film).</summary>
    Movie
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResultsVisibility
{
    /// <summary>Les résultats sont visibles par tous à tout moment.</summary>
    Always,

    /// <summary>Les résultats sont visibles une fois qu'on a voté.</summary>
    AfterVoting,

    /// <summary>Les résultats ne sont visibles qu'à la clôture du vote.</summary>
    AfterClose
}

public class PollSource
{
    public PollSourceType Type { get; set; }

    public string Value { get; set; } = string.Empty;

    /// <summary>Libellé affiché dans l'administration.</summary>
    public string Label { get; set; } = string.Empty;
}

public class Ballot
{
    public string VoterId { get; set; } = string.Empty;

    public string VoterName { get; set; } = string.Empty;

    public List<Guid> ItemIds { get; set; } = new();

    public DateTime UpdatedAt { get; set; }
}

public class Poll
{
    public Guid Id { get; set; }

    /// <summary>Jeton secret présent dans le lien de vote.</summary>
    public string Token { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>Clôture automatique (UTC). Null = pas de date limite.</summary>
    public DateTime? ClosesAt { get; set; }

    public bool IsClosed { get; set; }

    /// <summary>Nombre maximum de films choisis par votant. 0 = illimité.</summary>
    public int MaxVotesPerVoter { get; set; } = 3;

    public ResultsVisibility ResultsVisibility { get; set; } = ResultsVisibility.AfterVoting;

    public List<PollSource> Sources { get; set; } = new();

    /// <summary>Films retirés manuellement de la liste issue des sources.</summary>
    public List<Guid> ExcludedItemIds { get; set; } = new();

    public List<Ballot> Ballots { get; set; } = new();

    [JsonIgnore]
    public bool IsOpen => !IsClosed && (ClosesAt is null || ClosesAt > DateTime.UtcNow);
}
