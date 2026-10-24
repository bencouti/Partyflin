using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Jellyfin.Plugin.Partyflin.Models;
using Jellyfin.Plugin.Partyflin.Services;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Partyflin.Api;

/// <summary>
/// Page et API de vote publiques : l'accès se fait uniquement par le jeton secret du lien.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("Partyflin/Vote/{token}")]
public class VoteController : ControllerBase
{
    private const int MaxNameLength = 40;
    private const int MaxBallotsPerPoll = 200;
    private const int MaxBallotBytes = 16 * 1024;

    // La page est servie sur l'origine de Jellyfin : la CSP empêche tout script injecté de s'exécuter
    // (et donc d'accéder à la session d'un administrateur qui ouvrirait le lien).
    private const string PageCsp =
        "default-src 'none'; script-src 'nonce-{0}'; style-src 'unsafe-inline'; img-src 'self' https:; " +
        "connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    private const string ApiCsp = "default-src 'none'; frame-ancestors 'none'";

    private static readonly Lazy<string> VotePageHtml = new(() =>
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(typeof(Plugin).Namespace + ".Web.vote.html")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    private readonly PollStore _store;
    private readonly MovieResolver _resolver;
    private readonly IImageProcessor _imageProcessor;

    public VoteController(PollStore store, MovieResolver resolver, IImageProcessor imageProcessor)
    {
        _store = store;
        _resolver = resolver;
        _imageProcessor = imageProcessor;
    }

    [HttpGet]
    public ActionResult GetPage([FromRoute] string token)
    {
        if (_store.GetByToken(token) is null)
        {
            SetSecurityHeaders(ApiCsp);
            return NotFound("Ce lien de vote n'existe pas ou a été supprimé.");
        }

        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        SetSecurityHeaders(string.Format(CultureInfo.InvariantCulture, PageCsp, nonce));
        Response.Headers.CacheControl = "no-store";
        return Content(VotePageHtml.Value.Replace("<script>", $"<script nonce=\"{nonce}\">", StringComparison.Ordinal), "text/html; charset=utf-8");
    }

    [HttpGet("Data")]
    public ActionResult<VotePageDto> GetData([FromRoute] string token, [FromQuery] string? voterId)
    {
        SetSecurityHeaders(ApiCsp);
        var poll = _store.GetByToken(token);
        if (poll is null)
        {
            return NotFound();
        }

        var movies = _resolver.ResolveCached(poll);
        var ballot = string.IsNullOrEmpty(voterId) ? null : poll.Ballots.FirstOrDefault(b => b.VoterId == voterId);
        var hasVoted = ballot is not null && ballot.ItemIds.Count > 0;
        var showResults = poll.ResultsVisibility switch
        {
            ResultsVisibility.Always => true,
            ResultsVisibility.AfterVoting => hasVoted || !poll.IsOpen,
            _ => !poll.IsOpen
        };

        Response.Headers.CacheControl = "no-store";
        return new VotePageDto
        {
            Title = poll.Title,
            Description = poll.Description,
            ClosesAt = poll.ClosesAt,
            IsOpen = poll.IsOpen,
            MaxVotesPerVoter = poll.MaxVotesPerVoter,
            ResultsVisibility = poll.ResultsVisibility,
            Movies = movies.Select(MovieResolver.ToDto).ToList(),
            MyName = ballot?.VoterName,
            MyVotes = ballot?.ItemIds ?? new List<Guid>(),
            Results = showResults ? BuildResults(poll, movies) : null
        };
    }

    [HttpPost("Ballot")]
    [RequestSizeLimit(MaxBallotBytes)]
    public ActionResult SubmitBallot([FromRoute] string token, [FromBody] BallotRequest request)
    {
        SetSecurityHeaders(ApiCsp);
        var poll = _store.GetByToken(token);
        if (poll is null)
        {
            return NotFound();
        }

        if (!poll.IsOpen)
        {
            return BadRequest("Le vote est clôturé.");
        }

        var voterId = request.VoterId?.Trim() ?? string.Empty;
        var voterName = CleanName(request.VoterName);
        if (voterId.Length is < 8 or > 64)
        {
            return BadRequest("Identifiant de votant invalide.");
        }

        if (voterName.Length == 0)
        {
            return BadRequest("Indiquez votre prénom.");
        }

        var allowed = _resolver.ResolveCached(poll).Select(m => m.Id).ToHashSet();
        var itemIds = (request.ItemIds ?? new List<Guid>()).Distinct().Where(allowed.Contains).ToList();
        if (poll.MaxVotesPerVoter > 0 && itemIds.Count > poll.MaxVotesPerVoter)
        {
            return BadRequest($"Vous pouvez choisir au maximum {poll.MaxVotesPerVoter} film(s).");
        }

        return _store.SaveBallot(poll.Id, voterId, voterName, itemIds, MaxBallotsPerPoll) switch
        {
            BallotResult.NameTaken => Conflict("Ce prénom est déjà utilisé pour ce vote, choisissez-en un autre."),
            BallotResult.PollFull => BadRequest("Le nombre maximum de participants est atteint."),
            _ => NoContent()
        };
    }

    /// <summary>
    /// Affiche d'un film du vote, redimensionnée par Jellyfin. Seuls les films du vote sont accessibles.
    /// </summary>
    [HttpGet("Poster/{itemId}")]
    public async Task<ActionResult> GetPoster([FromRoute] string token, [FromRoute] Guid itemId)
    {
        SetSecurityHeaders(ApiCsp);
        var poll = _store.GetByToken(token);
        if (poll is null)
        {
            return NotFound();
        }

        var movie = _resolver.ResolveCached(poll).FirstOrDefault(m => m.Id == itemId);
        var image = movie?.GetImageInfo(ImageType.Primary, 0);
        if (movie is null || image is null)
        {
            return NotFound();
        }

        // Image distante (chemin issu des métadonnées Jellyfin, jamais de la requête) : Referrer-Policy
        // empêche le jeton du lien de fuiter vers le site tiers.
        if (!image.IsLocalFile)
        {
            return Uri.TryCreate(image.Path, UriKind.Absolute, out var remote) && remote.Scheme == Uri.UriSchemeHttps
                ? Redirect(remote.AbsoluteUri)
                : NotFound();
        }

        var (path, mimeType, _) = await _imageProcessor.ProcessImage(new ImageProcessingOptions
        {
            Item = movie,
            ItemId = movie.Id,
            Image = image,
            ImageIndex = 0,
            MaxWidth = 400,
            MaxHeight = 600,
            Quality = 85,
            SupportedOutputFormats = new[] { ImageFormat.Webp, ImageFormat.Jpg }
        }).ConfigureAwait(false);

        Response.Headers.CacheControl = "public, max-age=86400";
        return PhysicalFile(path, mimeType ?? "image/jpeg");
    }

    internal static ResultsDto BuildResults(Poll poll, IReadOnlyList<Movie> movies)
    {
        var ballots = poll.Ballots.Where(b => b.ItemIds.Count > 0).ToList();
        var entries = movies
            .Select(m =>
            {
                var voters = ballots.Where(b => b.ItemIds.Contains(m.Id)).Select(b => b.VoterName).OrderBy(n => n).ToList();
                return new ResultEntryDto { ItemId = m.Id, Name = m.Name, Year = m.ProductionYear, Votes = voters.Count, Voters = voters };
            })
            .Where(e => e.Votes > 0)
            .OrderByDescending(e => e.Votes)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ResultsDto { VoterCount = ballots.Count, Entries = entries };
    }

    /// <summary>
    /// Retire les caractères de contrôle et de mise en forme (inversion bidirectionnelle, etc.) et limite la longueur.
    /// </summary>
    private static string CleanName(string? name)
    {
        var cleaned = new string((name ?? string.Empty)
            .Where(c => !char.IsControl(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format)
            .ToArray()).Trim();
        return cleaned.Length > MaxNameLength ? cleaned[..MaxNameLength].Trim() : cleaned;
    }

    private void SetSecurityHeaders(string csp)
    {
        Response.Headers.ContentSecurityPolicy = csp;
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.XFrameOptions = "DENY";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
