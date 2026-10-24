using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.Partyflin.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Partyflin.Services;

public enum BallotResult
{
    Saved,
    NameTaken,
    PollFull
}

/// <summary>
/// Stockage des votes dans un fichier JSON du dossier de données de Jellyfin.
/// </summary>
public class PollStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly ILogger<PollStore> _logger;
    private List<Poll>? _polls;

    public PollStore(IApplicationPaths applicationPaths, ILogger<PollStore> logger)
    {
        _logger = logger;
        _filePath = Path.Combine(applicationPaths.DataPath, "partyflin", "polls.json");
    }

    public IReadOnlyList<Poll> GetAll()
    {
        lock (_lock)
        {
            return Load().OrderByDescending(p => p.CreatedAt).ToList();
        }
    }

    public Poll? GetById(Guid id)
    {
        lock (_lock)
        {
            return Load().FirstOrDefault(p => p.Id == id);
        }
    }

    public Poll? GetByToken(string token)
    {
        var candidate = Encoding.UTF8.GetBytes(token);
        lock (_lock)
        {
            return Load().FirstOrDefault(p => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(p.Token), candidate));
        }
    }

    public Poll Create(PollEditRequest request)
    {
        lock (_lock)
        {
            var poll = new Poll
            {
                Id = Guid.NewGuid(),
                Token = NewToken(),
                CreatedAt = DateTime.UtcNow
            };
            Apply(poll, request);
            Load().Add(poll);
            Save();
            return poll;
        }
    }

    public Poll? Update(Guid id, PollEditRequest request)
    {
        lock (_lock)
        {
            var poll = Load().FirstOrDefault(p => p.Id == id);
            if (poll is null)
            {
                return null;
            }

            Apply(poll, request);
            Save();
            return poll;
        }
    }

    public bool Delete(Guid id)
    {
        lock (_lock)
        {
            var removed = Load().RemoveAll(p => p.Id == id) > 0;
            if (removed)
            {
                Save();
            }

            return removed;
        }
    }

    /// <summary>
    /// Enregistre (ou remplace) le bulletin d'un votant.
    /// </summary>
    public BallotResult SaveBallot(Guid pollId, string voterId, string voterName, IReadOnlyList<Guid> itemIds, int maxBallots)
    {
        lock (_lock)
        {
            var poll = Load().First(p => p.Id == pollId);
            var isNewVoter = poll.Ballots.All(b => b.VoterId != voterId);
            if (poll.Ballots.Any(b => b.VoterId != voterId && string.Equals(b.VoterName, voterName, StringComparison.OrdinalIgnoreCase)))
            {
                return BallotResult.NameTaken;
            }

            if (isNewVoter && poll.Ballots.Count >= maxBallots)
            {
                return BallotResult.PollFull;
            }

            // Nouvelle liste plutôt que modification en place : les lecteurs hors verrou restent cohérents.
            var ballots = poll.Ballots.Where(b => b.VoterId != voterId).ToList();
            ballots.Add(new Ballot
            {
                VoterId = voterId,
                VoterName = voterName,
                ItemIds = itemIds.ToList(),
                UpdatedAt = DateTime.UtcNow
            });
            poll.Ballots = ballots;
            Save();
            return BallotResult.Saved;
        }
    }

    private static void Apply(Poll poll, PollEditRequest request)
    {
        poll.Title = request.Title.Trim();
        poll.Description = request.Description.Trim();
        poll.ClosesAt = request.ClosesAt?.ToUniversalTime();
        poll.IsClosed = request.IsClosed;
        poll.MaxVotesPerVoter = Math.Max(0, request.MaxVotesPerVoter);
        poll.ResultsVisibility = request.ResultsVisibility;
        poll.Sources = request.Sources
            .Where(s => !string.IsNullOrWhiteSpace(s.Value))
            .GroupBy(s => (s.Type, s.Value))
            .Select(g => g.First())
            .ToList();
        poll.ExcludedItemIds = request.ExcludedItemIds.Distinct().ToList();
    }

    private static string NewToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(12))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private List<Poll> Load()
    {
        if (_polls is not null)
        {
            return _polls;
        }

        try
        {
            _polls = File.Exists(_filePath)
                ? JsonSerializer.Deserialize<List<Poll>>(File.ReadAllText(_filePath), JsonOptions) ?? new List<Poll>()
                : new List<Poll>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Impossible de lire {Path}, démarrage avec une liste vide", _filePath);
            _polls = new List<Poll>();
        }

        return _polls;
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(_polls, JsonOptions));
        File.Move(tempPath, _filePath, true);
    }
}
