using System;
using System.Collections.Generic;
using Shokofin.API.Models.AniDB;

namespace Shokofin.API.Models;

public class Episode
{
    /// <summary>
    /// All identifiers related to the episode entry, e.g. the Shoko, AniDB,
    /// TMDB, etc.
    /// </summary>
    public EpisodeIDs IDs { get; set; } = new();

    /// <summary>
    /// The preferred name of the episode based on the selected episode language
    /// settings on the server.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The preferred description of the episode based on the selected episode
    /// language settings on the server.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The duration of the episode.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Indicates the episode is hidden.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Number of files 
    /// </summary>
    /// <value></value>
    public int Size { get; set; }

    /// <summary>
    /// The <see cref="AnidbEpisode"/>, if <see cref="DataSource.AniDB"/> is
    /// included in the data to add.
    /// </summary>
    public AnidbEpisode AniDB { get; set; } = new();

    /// <summary>
    /// File cross-references for the episode.
    /// </summary>
    public List<CrossReference.EpisodeCrossReferenceIDs> CrossReferences { get; set; } = [];

    public class EpisodeIDs : IDs
    {
        public int ParentSeries { get; set; }
    }
}


public enum EpisodeType
{
    /// <summary>
    /// A catch-all type for future extensions when a provider can't use a current episode type, but knows what the future type should be.
    /// </summary>
    Other = 2,

    /// <summary>
    /// The episode type is unknown.
    /// </summary>
    Unknown = Other,

    /// <summary>
    /// A normal episode.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// A special episode.
    /// </summary>
    Special = 3,

    /// <summary>
    /// A trailer.
    /// </summary>
    Trailer = 4,

    /// <summary>
    /// Either an opening-song, or an ending-song.
    /// </summary>
    ThemeSong = 5,

    /// <summary>
    /// Intro, and/or opening-song.
    /// </summary>
    OpeningSong = 6,

    /// <summary>
    /// Outro, end-roll, credits, and/or ending-song.
    /// </summary>
    EndingSong = 7,

    /// <summary>
    /// AniDB parody type. Where else would this be useful?
    /// </summary>
    Parody = 8,

    /// <summary>
    /// A interview tied to the series.
    /// </summary>
    Interview = 9,

    /// <summary>
    /// A DVD or BD extra, e.g. BD-menu or deleted scenes.
    /// </summary>
    Extra = 10,
}

