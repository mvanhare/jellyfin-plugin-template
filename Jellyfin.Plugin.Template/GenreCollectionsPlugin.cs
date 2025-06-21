using System;
using System.Collections.Generic;
using Jellyfin.Plugin.GenreCollections.Tasks;
using Jellyfin.Plugin.Template.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.GenreCollections;

/// <summary>
/// The main plugin entry point.
/// </summary>
public class GenreCollectionsPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenreCollectionsPlugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Provides access to application paths used by the plugin.</param>
    /// <param name="xmlSerializer">Handles XML serialization and deserialization for the plugin.</param>
    public GenreCollectionsPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
       : base(applicationPaths, xmlSerializer)
    {
    }

    /// <inheritdoc />
    public override string Name => "Genre Collections";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("6e56f1bf-9f3a-48dc-bf85-632ae00dc014"); // <-- IMPORTANT: Generate a new GUID!

    /// <inheritdoc />
    public override string Description => "Automatically creates collections for movie genres.";

    /// <summary>
    /// Gets the web pages provided by the plugin.
    /// </summary>
    /// <returns>A collection of <see cref="PluginPageInfo"/> objects representing the web pages.</returns>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
          new PluginPageInfo
          {
              Name = Name,
              EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html",
          }
        };
    }

    /// <summary>
    /// Configures services for the plugin.
    /// </summary>
    /// <param name="serviceCollection">The service collection to configure.</param>
    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IScheduledTask, GenreCollectionCreationTask>();
    }
}
