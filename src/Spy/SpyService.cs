using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using MigrationToolkit.Shared;
using MigrationToolkit.Shared.Models;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace MigrationToolkit.Spy
{
    /// <summary>
    /// ISpyService implementation. Walks the UWP visual tree, captures snapshots,
    /// executes UI actions, and reports navigation state.
    /// All visual tree access is dispatched to the UI thread.
    /// </summary>
    public class SpyService : ISpyService
    {
        private const string SnapshotFolder = "Snapshots";

        private readonly CoreDispatcher _dispatcher;
        private readonly UWPMapper _mapper;
        private readonly ActionExecutor _executor;

        public SpyService(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _mapper = new UWPMapper();
            _executor = new ActionExecutor(dispatcher);
        }

        public async Task<List<AbstractControl>> GetTreeAsync(int depth = 50)
        {
            var result = new List<AbstractControl>();

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var root = Window.Current?.Content;
                if (root == null)
                {
                    Debug.WriteLine("SpyService.GetTreeAsync: Window.Current.Content is null");
                    return;
                }

                var mapped = _mapper.Map(root, 0, depth);
                if (mapped != null)
                    result.Add(mapped);
            });

            return result;
        }

        public async Task<ScreenSnapshot> SaveSnapshotAsync(string name, string phase)
        {
            var controls = await GetTreeAsync(8);
            var pageName = await GetCurrentPageNameAsync();

            var snapshot = new ScreenSnapshot
            {
                Name = name,
                Phase = phase,
                PageName = pageName,
                Timestamp = DateTime.UtcNow,
                Controls = controls
            };

            // Save to LocalFolder/Snapshots/{phase}_{name}.json
            var localFolder = ApplicationData.Current.LocalFolder;
            var snapshotsFolder = await localFolder.CreateFolderAsync(
                SnapshotFolder, CreationCollisionOption.OpenIfExists);

            var fileName = $"{phase}_{name}.json";
            var file = await snapshotsFolder.CreateFileAsync(
                fileName, CreationCollisionOption.ReplaceExisting);

            var json = JsonSerializer.Serialize(snapshot, JsonOptions.Default);
            await FileIO.WriteTextAsync(file, json);

            Debug.WriteLine($"SpyService: snapshot saved as {fileName}");
            return snapshot;
        }

        public async Task<string[]> ListSnapshotsAsync()
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var snapshotsFolder = await localFolder.GetFolderAsync(SnapshotFolder);
                var files = await snapshotsFolder.GetFilesAsync();

                var names = new List<string>();
                foreach (var file in files)
                {
                    if (file.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        names.Add(file.Name);
                }
                return names.ToArray();
            }
            catch (System.IO.FileNotFoundException)
            {
                return Array.Empty<string>();
            }
        }

        public async Task<ScreenSnapshot?> GetSnapshotAsync(string fileName)
        {
            try
            {
                // Ensure .json extension
                if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    fileName += ".json";

                var localFolder = ApplicationData.Current.LocalFolder;
                var snapshotsFolder = await localFolder.GetFolderAsync(SnapshotFolder);
                var file = await snapshotsFolder.GetFileAsync(fileName);
                var json = await FileIO.ReadTextAsync(file);

                return JsonSerializer.Deserialize<ScreenSnapshot>(json, JsonOptions.Default);
            }
            catch (System.IO.FileNotFoundException)
            {
                return null;
            }
        }

        public async Task<ActionResult> DoActionAsync(ActionCommand command)
        {
            try
            {
                var control = await _executor.FindControlAsync(command.Id);
                await _executor.ExecuteAsync(control, command);

                // Re-map control to get updated state (R-SPY-09)
                AbstractControl? controlAfter = null;
                await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    controlAfter = _mapper.Map(control, 0, 0); // depth 0 = this control only
                });

                return new ActionResult
                {
                    Success = true,
                    ControlAfter = controlAfter
                };
            }
            catch (Exception ex)
            {
                return new ActionResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<NavigationInfo> GetNavigationAsync()
        {
            var info = new NavigationInfo();

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var root = Window.Current?.Content;
                if (root == null) return;

                // Find the root Frame
                var frame = FindFrame(root);
                if (frame != null)
                {
                    info.CurrentPage = frame.Content?.GetType().Name ?? "Unknown";
                    info.BackStackDepth = frame.BackStackDepth;
                }
                else
                {
                    info.CurrentPage = root.GetType().Name;
                }

                // Try to find available routes from NavigationView
                var navView = FindElement<NavigationView>(root);
                if (navView != null)
                {
                    var routes = new List<string>();
                    foreach (var item in navView.MenuItems)
                    {
                        if (item is NavigationViewItem nvi)
                        {
                            var tag = nvi.Tag?.ToString();
                            var content = nvi.Content?.ToString();
                            routes.Add(tag ?? content ?? nvi.GetType().Name);
                        }
                    }
                    info.AvailableRoutes = routes.ToArray();
                }
            });

            return info;
        }

        #region Helpers

        private async Task<string?> GetCurrentPageNameAsync()
        {
            string? pageName = null;
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var root = Window.Current?.Content;
                if (root == null) return;

                var frame = FindFrame(root);
                if (frame?.Content != null)
                    pageName = frame.Content.GetType().Name;
            });
            return pageName;
        }

        /// <summary>
        /// Find the deepest Frame in the visual tree (for Shell apps, the actual content frame
        /// is nested inside Shell renderers).
        /// </summary>
        private Frame? FindFrame(DependencyObject root)
        {
            Frame? deepestFrame = null;

            void Walk(DependencyObject element)
            {
                if (element is Frame f)
                    deepestFrame = f;

                var count = VisualTreeHelper.GetChildrenCount(element);
                for (int i = 0; i < count; i++)
                    Walk(VisualTreeHelper.GetChild(element, i));
            }

            Walk(root);
            return deepestFrame;
        }

        /// <summary>
        /// Find the first element of type T in the visual tree.
        /// </summary>
        private T? FindElement<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T match) return match;

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var found = FindElement<T>(VisualTreeHelper.GetChild(root, i));
                if (found != null) return found;
            }

            return null;
        }

        #endregion
    }
}
