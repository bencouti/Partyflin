using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Partyflin.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Partyflin;

/// <summary>
/// Partyflin : liens de vote pour choisir le film d'une soirée entre amis.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Partyflin";

    public override Guid Id => Guid.Parse("ce2da81e-c439-4976-8e42-4382f20e48b1");

    public override string Description => "Créez des liens de vote pour choisir le film de votre soirée entre amis.";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                DisplayName = "Partyflin",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html",
                EnableInMainMenu = true,
                MenuIcon = "how_to_vote"
            }
        };
    }
}
