using System;
using System.Collections.Generic;
using Jellyfin.Plugin.GenreCollections.Tasks;
using Jellyfin.Plugin.Template.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using System.Linq; // Added for LINQ operations
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers; // Required for IHasThumbImage
using MediaBrowser.Controller.Security; // Required for IUserManager
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Enums; // Required for ImageType
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging; // Added for logging

namespace Jellyfin.Plugin.GenreCollections;

/// <summary>
/// The main plugin entry point.
/// </summary>
public class GenreCollectionsPlugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage
{
    private readonly ILogger<GenreCollectionsPlugin> _logger;
    private readonly IUserDataManager _userDataManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenreCollectionsPlugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Provides access to application paths used by the plugin.</param>
    /// <param name="xmlSerializer">Handles XML serialization and deserialization for the plugin.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="userDataManager">The user data manager.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="userManager">The user manager.</param>
    public GenreCollectionsPlugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<GenreCollectionsPlugin> logger,
        IUserDataManager userDataManager,
        ILibraryManager libraryManager,
        IUserManager userManager)
       : base(applicationPaths, xmlSerializer)
    {
        _logger = logger;
        _userDataManager = userDataManager;
        _libraryManager = libraryManager;
        _userManager = userManager;
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

    /// <inheritdoc />
    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        if (configuration is PluginConfiguration newConfig)
        {
            _logger.LogInformation("Plugin configuration updated. Applying pinned collections.");

            var users = _userManager.Users;
            if (!users.Any())
            {
                _logger.LogInformation("No users found to apply pinned collections for.");
                base.UpdateConfiguration(configuration);
                return;
            }

            var pinnedCollectionGuids = newConfig.PinnedCollectionIds ?? new List<Guid>();
            _logger.LogDebug("Configured PinnedCollectionIds: {Count}", pinnedCollectionGuids.Count);

            // For simplicity, this initial implementation will only ADD to favorites.
            // A more complete solution would track what this plugin specifically pinned
            // and unpin items that are removed from the configuration.

            foreach (var user in users)
            {
                _logger.LogDebug("Processing user: {Username}", user.Username);
                foreach (var collectionId in pinnedCollectionGuids)
                {
                    try
                    {
                        var collection = _libraryManager.GetItemById(collectionId);
                        if (collection is BoxSet) // Ensure it's a collection
                        {
                            var userData = _userDataManager.GetUserData(user, collection);
                            if (!userData.IsFavorite)
                            {
                                userData.IsFavorite = true;
                                _userDataManager.SaveUserData(user, collection, userData, CancellationToken.None);
                                _logger.LogInformation("Pinned collection '{CollectionName}' ({CollectionId}) for user '{Username}'.", collection.Name, collectionId, user.Username);
                            }
                            else
                            {
                                _logger.LogDebug("Collection '{CollectionName}' ({CollectionId}) already pinned for user '{Username}'.", collection.Name, collectionId, user.Username);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Item with ID {CollectionId} is not a BoxSet or not found, cannot pin.", collectionId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error pinning collection ID {CollectionId} for user {Username}.", collectionId, user.Username);
                    }
                }
            }
            // Note: Unpinning logic is more complex and omitted for this first pass.
            // To correctly unpin, we would need to:
            // 1. Get the *previous* configuration's PinnedCollectionIds.
            // 2. Identify collections that were in the previous list but not in the new list.
            // 3. For those collections, set IsFavorite = false for each user,
            //    *only if this plugin was the one that favorited it* (e.g., by storing a marker in UserItemData).
            //    Alternatively, if the behavior is that this plugin *manages* the pinned state for these configured collections,
            //    then any collection *not* in pinnedCollectionGuids should have IsFavorite set to false if it was previously true
            //    (assuming this plugin is the sole controller of its "pinned via plugin" status).
            // For now, we only add to favorites.
        }
        else
        {
            _logger.LogWarning("Received configuration of unexpected type: {TypeName}", configuration.GetType().Name);
        }

        base.UpdateConfiguration(configuration);
    }

    // Implementation for IHasThumbImage (optional, but good practice if the plugin could have an image)
    /// <inheritdoc />
    public ImageType ThumbImageType => ImageType.Primary;

    /// <inheritdoc />
    public string GetThumbImageFormat() => "png"; // Or whatever format your image is

    /// <inheritdoc />
    public Task<DynamicImageResponse> GetThumbImage(ImageType type, CancellationToken cancellationToken)
    {
        // If you have a thumb image for the plugin, load it here.
        // For example, from an embedded resource.
        var path = GetType().Namespace + ".thumb.png"; // Example path
        var stream = GetType().Assembly.GetManifestResourceStream(path);
        if (stream != null)
        {
            return Task.FromResult(new DynamicImageResponse { Format = ImageFormat.Png, Stream = stream });
        }
        return Task.FromResult<DynamicImageResponse>(null);
    }
}
