using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

#nullable enable
namespace Shokofin.ExternalIds;

public class ShokoGroupId : IExternalId
{
    public const string Name = "Shoko Group";

    public bool Supports(IHasProviderIds item)
        => item is Series or BoxSet;

    public string ProviderName
        => Name;

    public string Key
        => Name;

    public ExternalIdMediaType? Type
        => null;

    public virtual string UrlFormatString
        => $"{Plugin.Instance.Configuration.PrettyUrl}/webui/collection/group/{{0}}";
}