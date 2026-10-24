using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Partyflin.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// URL publique du serveur (ex. https://films.example.com) utilisée pour générer les liens de vote.
    /// Vide = adresse utilisée par l'administrateur dans son navigateur.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}
