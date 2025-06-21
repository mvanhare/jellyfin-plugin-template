using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.GenreCollections.Tasks
{
    /// <summary>
    /// The scheduled task that creates and manages genre collections.
    /// </summary>
    public class GenreCollectionCreationTask : IScheduledTask
    {
        private readonly ILogger<GenreCollectionCreationTask> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly ICollectionManager _collectionManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenreCollectionCreationTask"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="libraryManager">The library manager.</param>
        /// <param name="collectionManager">The collection manager.</param>
        public GenreCollectionCreationTask(ILogger<GenreCollectionCreationTask> logger, ILibraryManager libraryManager, ICollectionManager collectionManager)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _collectionManager = collectionManager;
        }

        /// <inheritdoc />
        public string Name => "Create Genre Collections";

        /// <inheritdoc />
        public string Key => "GenreCollectionsCreation";

        /// <inheritdoc />
        public string Description => "Scans the movie library and creates collections based on genres.";

        /// <inheritdoc />
        public string Category => "Library";

        /// <summary>
        /// The main execution logic of the task.
        /// </summary>
        /// <param name="progress">The progress reporter.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Genre Collections task started.");
            progress.Report(0);

            // Get all movies from the library.
            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                IsVirtualItem = false,
                Recursive = true
            };

            var allMovies = _libraryManager.GetItemList(query).OfType<Movie>().ToList();
            _logger.LogInformation("Found {Count} movies to process.", allMovies.Count);

            if (allMovies.Count == 0)
            {
                progress.Report(100);
                _logger.LogInformation("No movies found. Genre Collections task finished.");
                return;
            }

            // Iterate over each movie
            for (int i = 0; i < allMovies.Count; i++)
            {
                var movie = allMovies[i];
                cancellationToken.ThrowIfCancellationRequested();

                if (movie.Genres is null)
                {
                    _logger.LogDebug("Movie '{Name}' has no genres, skipping.", movie.Name);
                    continue;
                }

                // For each genre on the movie, find or create a collection
                foreach (var genreName in movie.Genres)
                {
                    if (string.IsNullOrWhiteSpace(genreName))
                    {
                        continue;
                    }

                    var collectionName = genreName.Trim();

                    // Find existing collection using a query
                    var queryResultItems = _libraryManager.QueryItems(new InternalItemsQuery
                    {
                        Name = collectionName,
                        IncludeItemTypes = new[] { BaseItemKind.BoxSet }
                    }).Items;

                    BoxSet? existingCollection = null;
                    if (queryResultItems.Count > 0)
                    {
                        existingCollection = queryResultItems[0] as BoxSet;
                    }

                    BoxSet? collection;
                    if (existingCollection is null)
                    {
                        _logger.LogInformation("Creating new collection: '{Name}'", collectionName);
                        collection = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
                        {
                            Name = collectionName
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        collection = existingCollection;
                    }

                    if (collection is null)
                    {
                        _logger.LogWarning("Failed to create or find collection for genre '{Genre}'.", genreName);
                        continue;
                    }

                    // Add the movie to the collection if it's not already there.
                    // We check the collection's direct children to see if the movie is already linked.
                    if (collection.LinkedChildren.All(c => c.ItemId != movie.Id))
                    {
                        _logger.LogDebug("Adding movie '{MovieName}' to collection '{CollectionName}'.", movie.Name, collectionName);
                        await _collectionManager.AddToCollectionAsync(collection.Id, new[] { movie.Id }).ConfigureAwait(false);
                    }
                }

                // Update progress
                progress.Report(((double)(i + 1) / allMovies.Count) * 100);
            }

            progress.Report(100);
            _logger.LogInformation("Genre Collections task finished successfully.");
        }

        /// <summary>
        /// Defines the default schedule for the task.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="TaskTriggerInfo"/>.</returns>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(24).Ticks
                }
            };
        }
    }
}
