using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Shokofin.API;
using Shokofin.ExternalIds;
using Shokofin.Utils;

using Info = Shokofin.API.Info;
using SeriesType = Shokofin.API.Models.SeriesType;
using EpisodeType = Shokofin.API.Models.EpisodeType;

namespace Shokofin.Providers;

public class EpisodeProvider: IRemoteMetadataProvider<Episode, EpisodeInfo>, IHasOrder
{
    public string Name => Plugin.MetadataProviderName;

    public int Order => 0;

    private readonly IHttpClientFactory HttpClientFactory;

    private readonly ILogger<EpisodeProvider> Logger;

    private readonly ShokoAPIManager ApiManager;

    public EpisodeProvider(IHttpClientFactory httpClientFactory, ILogger<EpisodeProvider> logger, ShokoAPIManager apiManager)
    {
        HttpClientFactory = httpClientFactory;
        Logger = logger;
        ApiManager = apiManager;
    }

    public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
    {
        var trackerId = Plugin.Instance.Tracker.Add($"Providing info for Episode \"{info.Name}\". (Path=\"{info.Path}\",IsMissingEpisode={info.IsMissingEpisode})");
        try {
            var result = new MetadataResult<Episode>();
            var config = Plugin.Instance.Configuration;

            // Fetch the episode, series and group info (and file info, but that's not really used (yet))
            Info.FileInfo? fileInfo = null;
            Info.EpisodeInfo? episodeInfo = null;
            Info.SeasonInfo? seasonInfo = null;
            Info.ShowInfo? showInfo = null;
            if (info.IsMissingEpisode || string.IsNullOrEmpty(info.Path)) {
                // We're unable to fetch the latest metadata for the virtual episode.
                if (!info.TryGetProviderId(ShokoEpisodeId.Name, out var episodeId))
                    return result;

                episodeInfo = await ApiManager.GetEpisodeInfo(episodeId);
                if (episodeInfo == null)
                    return result;

                seasonInfo = await ApiManager.GetSeasonInfoForEpisode(episodeId);
                if (seasonInfo == null)
                    return result;

                showInfo = await ApiManager.GetShowInfoForSeries(seasonInfo.Id);
                if (showInfo == null || showInfo.SeasonList.Count == 0)
                    return result;
            }
            else {
                (fileInfo, seasonInfo, showInfo) = await ApiManager.GetFileInfoByPath(info.Path);
                episodeInfo = fileInfo?.EpisodeList.FirstOrDefault().Episode;
            }

            // if the episode info is null then the series info and conditionally the group info is also null.
            if (episodeInfo == null || seasonInfo == null || showInfo == null) {
                Logger.LogWarning("Unable to find episode info for path {Path}", info.Path);
                return result;
            }

            result.Item = CreateMetadata(showInfo, seasonInfo, episodeInfo, fileInfo, info.MetadataLanguage, info.MetadataCountryCode);
            Logger.LogInformation("Found episode {EpisodeName} (File={FileId},Episode={EpisodeId},Series={SeriesId},ExtraSeries={ExtraIds},Group={GroupId})", result.Item.Name, fileInfo?.Id, episodeInfo.Id, seasonInfo.Id, seasonInfo.ExtraIds, showInfo?.GroupId);

            result.HasMetadata = true;

            return result;
        }
        catch (Exception ex) {
            if (info.IsMissingEpisode || string.IsNullOrEmpty(info.Path)) {
                if (!info.TryGetProviderId(ShokoEpisodeId.Name, out var episodeId))
                    episodeId = null;
                Logger.LogError(ex, "Threw unexpectedly while refreshing a missing episode; {Message} (Episode={EpisodeId})", ex.Message, episodeId);
            }
            else {
                Logger.LogError(ex, "Threw unexpectedly while refreshing {Path}: {Message}", info.Path, info.IsMissingEpisode);
            }

            return new MetadataResult<Episode>();
        }
        finally {
            Plugin.Instance.Tracker.Remove(trackerId);
        }
    }

    public static Episode CreateMetadata(Info.ShowInfo group, Info.SeasonInfo series, Info.EpisodeInfo episode, Season season, Guid episodeId)
        => CreateMetadata(group, series, episode, null, season.GetPreferredMetadataLanguage(), season.GetPreferredMetadataCountryCode(), season, episodeId);

    public static Episode CreateMetadata(Info.ShowInfo group, Info.SeasonInfo series, Info.EpisodeInfo episode, Info.FileInfo? file, string metadataLanguage, string metadataCountryCode)
        => CreateMetadata(group, series, episode, file, metadataLanguage, metadataCountryCode, null, Guid.Empty);

    private static Episode CreateMetadata(Info.ShowInfo group, Info.SeasonInfo series, Info.EpisodeInfo episode, Info.FileInfo? file, string metadataLanguage, string metadataCountryCode, Season? season, Guid episodeId)
    {
        var config = Plugin.Instance.Configuration;
        string? displayTitle, alternateTitle, description;
        if (file != null && file.EpisodeList.Count > 1) {
            var displayTitles = new List<string?>();
            var alternateTitles = new List<string?>();
            foreach (var (episodeInfo, _, _) in file.EpisodeList) {
                string defaultEpisodeTitle = episodeInfo.DefaultTitle;
                if (
                    // Movies
                    (series.Type == SeriesType.Movie && episodeInfo.Type is EpisodeType.Normal or EpisodeType.Special) ||
                    // All other ignored types.
                    (episodeInfo.Type is EpisodeType.Normal && episodeInfo.EpisodeNumber == 1 && episodeInfo.Titles.FirstOrDefault(title => title.Source is "AniDB" && title.LanguageCode is "en")?.Value is { } mainTitle && Text.IgnoredSubTitles.Contains(mainTitle))
                ) {
                    string defaultSeriesTitle = series.Shoko.Name;
                    var (dTitle, aTitle) = Text.GetMovieTitles(episodeInfo, series, metadataLanguage);
                    displayTitles.Add(dTitle);
                    alternateTitles.Add(aTitle);
                }
                else {
                    var (dTitle, aTitle) = Text.GetEpisodeTitles(episodeInfo, series, metadataLanguage);
                    displayTitles.Add(dTitle);
                    alternateTitles.Add(aTitle);
                }
            }
            displayTitle = Text.JoinText(displayTitles);
            alternateTitle = Text.JoinText(alternateTitles);
            description = Text.GetDescription(file.EpisodeList.Select(tuple => tuple.Episode), metadataLanguage);
        }
        else {
            string defaultEpisodeTitle = episode.DefaultTitle;
            if (
                // Movies
                (series.Type == SeriesType.Movie && episode.Type is EpisodeType.Normal or EpisodeType.Special) ||
                // All other ignored types.
                (episode.Type is EpisodeType.Normal && episode.EpisodeNumber == 1 && episode.Titles.FirstOrDefault(title => title.Source is "AniDB" && title.LanguageCode is "en")?.Value is { } mainTitle && Text.IgnoredSubTitles.Contains(mainTitle))
            ) {
                string defaultSeriesTitle = series.Shoko.Name;
                (displayTitle, alternateTitle) = Text.GetMovieTitles(episode, series, metadataLanguage);
            }
            else {
                (displayTitle, alternateTitle) = Text.GetEpisodeTitles(episode, series, metadataLanguage);
            }
            description = Text.GetDescription(episode, metadataLanguage);
        }

        if (config.MarkSpecialsWhenGrouped) switch (episode.Type) {
            case EpisodeType.Other:
            case EpisodeType.Normal:
                break;
            case EpisodeType.Special: {
                // We're guaranteed to find the index, because otherwise it would've thrown when getting the episode number.
                var index = series.SpecialsList.FindIndex(ep => ep == episode);
                displayTitle = $"S{index + 1} {displayTitle}";
                alternateTitle = $"S{index + 1} {alternateTitle}";
                break;
            }
            case EpisodeType.ThemeSong:
            case EpisodeType.EndingSong:
            case EpisodeType.OpeningSong:
                displayTitle = $"C{episode.EpisodeNumber} {displayTitle}";
                alternateTitle = $"C{episode.EpisodeNumber} {alternateTitle}";
                break;
            case EpisodeType.Trailer:
                displayTitle = $"T{episode.EpisodeNumber} {displayTitle}";
                alternateTitle = $"T{episode.EpisodeNumber} {alternateTitle}";
                break;
            case EpisodeType.Parody:
                displayTitle = $"P{episode.EpisodeNumber} {displayTitle}";
                alternateTitle = $"P{episode.EpisodeNumber} {alternateTitle}";
                break;
            default:
                displayTitle = $"U{episode.EpisodeNumber} {displayTitle}";
                alternateTitle = $"U{episode.EpisodeNumber} {alternateTitle}";
                break;
        }

        var episodeNumber = Ordering.GetEpisodeNumber(group, series, episode);
        var seasonNumber = Ordering.GetSeasonNumber(group, series, episode);
        var (airsBeforeEpisodeNumber, airsBeforeSeasonNumber, airsAfterSeasonNumber, isSpecial) = Ordering.GetSpecialPlacement(group, series, episode);

        Episode result;
        if (season != null) {
            result = new Episode {
                Name = displayTitle ?? $"Episode {episodeNumber}",
                OriginalTitle = alternateTitle ?? "",
                IndexNumber = episodeNumber,
                ParentIndexNumber = isSpecial ? 0 : seasonNumber,
                AirsAfterSeasonNumber = airsAfterSeasonNumber,
                AirsBeforeEpisodeNumber = airsBeforeEpisodeNumber,
                AirsBeforeSeasonNumber = airsBeforeSeasonNumber,
                Id = episodeId,
                IsVirtualItem = true,
                SeasonId = season.Id,
                SeriesId = season.Series.Id,
                Overview = description,
                CommunityRating = episode.OfficialRating.Value > 0 ? episode.OfficialRating.ToFloat(10) : 0,
                PremiereDate = episode.AiredAt,
                SeriesName = season.Series.Name,
                SeriesPresentationUniqueKey = season.SeriesPresentationUniqueKey,
                SeasonName = season.Name,
                ProductionLocations = TagFilter.GetSeasonProductionLocations(series),
                OfficialRating = ContentRating.GetSeasonContentRating(series, metadataCountryCode),
                DateLastSaved = DateTime.UtcNow,
                RunTimeTicks = episode.Runtime.Ticks,
            };
            result.PresentationUniqueKey = result.GetPresentationUniqueKey();
        }
        else {
            result = new Episode {
                Name = displayTitle,
                OriginalTitle = alternateTitle,
                IndexNumber = episodeNumber,
                ParentIndexNumber = isSpecial ? 0 : seasonNumber,
                AirsAfterSeasonNumber = airsAfterSeasonNumber,
                AirsBeforeEpisodeNumber = airsBeforeEpisodeNumber,
                AirsBeforeSeasonNumber = airsBeforeSeasonNumber,
                PremiereDate = episode.AiredAt,
                Overview = description,
                ProductionLocations = TagFilter.GetSeasonProductionLocations(series),
                OfficialRating = ContentRating.GetSeasonContentRating(series, metadataCountryCode),
                CustomRating = group.CustomRating,
                CommunityRating = episode.OfficialRating.Value > 0 ? episode.OfficialRating.ToFloat(10) : 0,
            };
        }

        if (file != null && file.EpisodeList.Count > 1) {
            var episodeNumberEnd = episodeNumber + file.EpisodeList.Count - 1;
            if (episodeNumberEnd != episodeNumber && episode.EpisodeNumber != episodeNumberEnd)
                result.IndexNumberEnd = episodeNumberEnd;
        }

        AddProviderIds(result, episodeId: episode.Id, fileId: file?.Id, seriesId: file?.SeriesId, anidbId: episode.AnidbId, tmdbId: episode.TmdbId, tvdbId: episode.TvdbId);

        return result;
    }

    private static void AddProviderIds(IHasProviderIds item, string episodeId, string? fileId = null, string? seriesId = null, string? anidbId = null, string? tmdbId = null, string? tvdbId = null)
    {
        var config = Plugin.Instance.Configuration;
        item.SetProviderId(ShokoEpisodeId.Name, episodeId);
        if (!string.IsNullOrEmpty(fileId))
            item.SetProviderId(ShokoFileId.Name, fileId);
        if (!string.IsNullOrEmpty(seriesId))
            item.SetProviderId(ShokoSeriesId.Name, seriesId);
        if (config.AddAniDBId && !string.IsNullOrEmpty(anidbId) && anidbId != "0")
            item.SetProviderId("AniDB", anidbId);
        if (config.AddTMDBId &&!string.IsNullOrEmpty(tmdbId) && tmdbId != "0")
            item.SetProviderId(MetadataProvider.Tmdb, tmdbId);
        if (config.AddTvDBId && !string.IsNullOrEmpty(tvdbId) && tvdbId != "0")
            item.SetProviderId(MetadataProvider.Tvdb, tvdbId);
    }

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        => Task.FromResult<IEnumerable<RemoteSearchResult>>([]);

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => HttpClientFactory.CreateClient().GetAsync(url, cancellationToken);
}
