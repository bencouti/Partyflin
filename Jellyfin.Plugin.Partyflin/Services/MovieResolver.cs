using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.Partyflin.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Partyflin.Services;

/// <summary>
/// Transforme les sources d'un vote en liste de films, et fournit la recherche pour l'administration.
/// </summary>
public class MovieResolver
{
    private const int SearchLimit = 30;

    private readonly ILibraryManager _libraryManager;

    public MovieResolver(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    public IReadOnlyList<Movie> Resolve(IEnumerable<PollSource> sources, IEnumerable<Guid>? excluded = null)
    {
        var excludedSet = excluded?.ToHashSet() ?? new HashSet<Guid>();
        var movies = new Dictionary<Guid, Movie>();

        foreach (var source in sources)
        {
            foreach (var movie in ResolveSource(source))
            {
                if (!excludedSet.Contains(movie.Id))
                {
                    movies.TryAdd(movie.Id, movie);
                }
            }
        }

        return movies.Values
            .OrderBy(m => m.SortName ?? m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<SearchResultDto> Search(PollSourceType type, string? term)
    {
        term = term?.Trim() ?? string.Empty;

        switch (type)
        {
            case PollSourceType.Genre:
                return AllMovies()
                    .SelectMany(m => m.Genres ?? Array.Empty<string>())
                    .GroupBy(g => g, StringComparer.OrdinalIgnoreCase)
                    .Where(g => Matches(g.Key, term))
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new SearchResultDto { Value = g.Key, Label = g.Key, Detail = FilmCount(g.Count()) })
                    .ToList();

            case PollSourceType.Library:
                return _libraryManager.GetVirtualFolders()
                    .Where(f => f.CollectionType == CollectionTypeOptions.movies && Matches(f.Name, term))
                    .Select(f => new SearchResultDto { Value = f.ItemId, Label = f.Name ?? string.Empty, Detail = "Bibliothèque" })
                    .ToList();

            case PollSourceType.Playlist:
                return SearchItems(BaseItemKind.Playlist, term)
                    .OfType<Playlist>()
                    .Select(p => new SearchResultDto { Value = p.Id.ToString("N"), Label = p.Name, Detail = FilmCount(MoviesIn(p).Count()) })
                    .ToList();

            case PollSourceType.Collection:
                return SearchItems(BaseItemKind.BoxSet, term)
                    .OfType<BoxSet>()
                    .Select(b => new SearchResultDto { Value = b.Id.ToString("N"), Label = b.Name, Detail = FilmCount(MoviesIn(b).Count()) })
                    .ToList();

            case PollSourceType.Movie:
                return SearchItems(BaseItemKind.Movie, term)
                    .OfType<Movie>()
                    .Select(m => new SearchResultDto { Value = m.Id.ToString("N"), Label = m.Name, Detail = m.ProductionYear?.ToString() ?? string.Empty })
                    .ToList();

            default:
                return Array.Empty<SearchResultDto>();
        }
    }

    public static MovieDto ToDto(Movie movie)
    {
        return new MovieDto
        {
            Id = movie.Id,
            Name = movie.Name,
            Year = movie.ProductionYear,
            Overview = movie.Overview ?? string.Empty,
            RuntimeMinutes = movie.RunTimeTicks is long ticks ? (int)TimeSpan.FromTicks(ticks).TotalMinutes : null,
            Genres = movie.Genres ?? Array.Empty<string>(),
            CommunityRating = movie.CommunityRating,
            OfficialRating = movie.OfficialRating ?? string.Empty,
            HasPoster = movie.HasImage(ImageType.Primary)
        };
    }

    private IEnumerable<Movie> ResolveSource(PollSource source)
    {
        switch (source.Type)
        {
            case PollSourceType.Genre:
                return _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Movie },
                    Recursive = true,
                    Genres = new[] { source.Value }
                }).OfType<Movie>();

            case PollSourceType.Library:
                return Guid.TryParse(source.Value, out var libraryId)
                    ? _libraryManager.GetItemList(new InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { BaseItemKind.Movie },
                        Recursive = true,
                        AncestorIds = new[] { libraryId }
                    }).OfType<Movie>()
                    : Enumerable.Empty<Movie>();

            case PollSourceType.Playlist:
            case PollSourceType.Collection:
            case PollSourceType.Movie:
                if (!Guid.TryParse(source.Value, out var itemId))
                {
                    return Enumerable.Empty<Movie>();
                }

                return _libraryManager.GetItemById(itemId) switch
                {
                    Movie movie => new[] { movie },
                    Folder folder => MoviesIn(folder),
                    _ => Enumerable.Empty<Movie>()
                };

            default:
                return Enumerable.Empty<Movie>();
        }
    }

    /// <summary>
    /// Films contenus dans une playlist ou une collection (les collections imbriquées sont parcourues).
    /// </summary>
    private static IEnumerable<Movie> MoviesIn(Folder folder, int depth = 0)
    {
        foreach (var child in folder.GetLinkedChildren())
        {
            if (child is Movie movie)
            {
                yield return movie;
            }
            else if (child is BoxSet boxSet && depth < 3)
            {
                foreach (var nested in MoviesIn(boxSet, depth + 1))
                {
                    yield return nested;
                }
            }
        }
    }

    private IEnumerable<Movie> AllMovies()
    {
        return _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie },
            Recursive = true
        }).OfType<Movie>();
    }

    private IReadOnlyList<BaseItem> SearchItems(BaseItemKind kind, string term)
    {
        return _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { kind },
            Recursive = true,
            SearchTerm = string.IsNullOrEmpty(term) ? null : term,
            Limit = SearchLimit,
            OrderBy = new[] { (ItemSortBy.SortName, SortOrder.Ascending) }
        });
    }

    private static bool Matches(string? value, string term)
    {
        return term.Length == 0 || (value?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string FilmCount(int count) => count > 1 ? $"{count} films" : $"{count} film";
}
