using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Core;
using AppFilePickerOpenOptions = SimRacingHub.Application.Contracts.FilePickerOpenOptions;
using AppFilePickerSaveOptions = SimRacingHub.Application.Contracts.FilePickerSaveOptions;
using AvaloniaOpenOptions = Avalonia.Platform.Storage.FilePickerOpenOptions;
using AvaloniaSaveOptions = Avalonia.Platform.Storage.FilePickerSaveOptions;

namespace SimRacingHub.Avalonia.Adapters
{
    /// <summary>
    /// Avalonia implementation of <see cref="IFilePickerService"/> using <see cref="IStorageProvider"/>
    /// and returning neutral <see cref="PickedFile"/> representations backed by storage streams.
    /// Does not assume that all returned items are local file paths.
    /// </summary>
    public sealed class AvaloniaFilePickerService : IFilePickerService
    {
        private readonly IUiDispatcher _dispatcher;
        private readonly Func<IStorageProvider?>? _storageProviderResolver;

        /// <summary>
        /// Optional delegate to intercept OpenFileAsync calls for testing or headless automation.
        /// Return <see cref="PickedFile"/> to simulate selection, or null to simulate user cancellation.
        /// </summary>
        public Func<AppFilePickerOpenOptions, Task<PickedFile?>>? OpenFileHandler { get; set; }

        /// <summary>
        /// Optional delegate to intercept SaveFileAsync calls for testing or headless automation.
        /// Return <see cref="PickedFile"/> to simulate selection, or null to simulate user cancellation.
        /// </summary>
        public Func<AppFilePickerSaveOptions, Task<PickedFile?>>? SaveFileHandler { get; set; }

        public AvaloniaFilePickerService(
            IUiDispatcher? dispatcher = null,
            Func<IStorageProvider?>? storageProviderResolver = null)
        {
            _dispatcher = dispatcher ?? new AvaloniaUiDispatcher();
            _storageProviderResolver = storageProviderResolver;
        }

        public async Task<PickedFile?> OpenFileAsync(AppFilePickerOpenOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            ct.ThrowIfCancellationRequested();

            if (OpenFileHandler != null)
            {
                return await OpenFileHandler(options).ConfigureAwait(false);
            }

            if (_dispatcher.CheckAccess())
            {
                return await OpenFileCoreAsync(options, ct).ConfigureAwait(false);
            }
            else
            {
                var task = await _dispatcher.InvokeAsync(() => OpenFileCoreAsync(options, ct), ct).ConfigureAwait(false);
                return await task.ConfigureAwait(false);
            }
        }

        private async Task<PickedFile?> OpenFileCoreAsync(AppFilePickerOpenOptions options, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var storageProvider = _storageProviderResolver?.Invoke() ?? ResolveStorageProvider();
            if (storageProvider == null || !storageProvider.CanOpen)
            {
                AppLogger.Instance.LogWarning("StorageProvider is not available or does not support opening files.");
                return null;
            }

            var avaloniaOptions = new AvaloniaOpenOptions
            {
                Title = options.Title,
                AllowMultiple = false,
                SuggestedFileName = options.SuggestedFileName,
                FileTypeFilter = ConvertFilters(options.Filters)
            };

            var files = await storageProvider.OpenFilePickerAsync(avaloniaOptions).ConfigureAwait(false);
            if (files == null || files.Count == 0)
            {
                return null;
            }

            var file = files[0];
            return WrapStorageFile(file);
        }

        public async Task<PickedFile?> SaveFileAsync(AppFilePickerSaveOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            ct.ThrowIfCancellationRequested();

            if (SaveFileHandler != null)
            {
                return await SaveFileHandler(options).ConfigureAwait(false);
            }

            if (_dispatcher.CheckAccess())
            {
                return await SaveFileCoreAsync(options, ct).ConfigureAwait(false);
            }
            else
            {
                var task = await _dispatcher.InvokeAsync(() => SaveFileCoreAsync(options, ct), ct).ConfigureAwait(false);
                return await task.ConfigureAwait(false);
            }
        }

        private async Task<PickedFile?> SaveFileCoreAsync(AppFilePickerSaveOptions options, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var storageProvider = _storageProviderResolver?.Invoke() ?? ResolveStorageProvider();
            if (storageProvider == null || !storageProvider.CanSave)
            {
                AppLogger.Instance.LogWarning("StorageProvider is not available or does not support saving files.");
                return null;
            }

            var avaloniaOptions = new AvaloniaSaveOptions
            {
                Title = options.Title,
                SuggestedFileName = options.SuggestedFileName,
                DefaultExtension = options.DefaultExtension?.TrimStart('.'),
                FileTypeChoices = ConvertFilters(options.Filters)
            };

            var file = await storageProvider.SaveFilePickerAsync(avaloniaOptions).ConfigureAwait(false);
            if (file == null)
            {
                return null;
            }

            return WrapStorageFile(file);
        }

        public Task<PickedFile?> OpenFileAsync(FilePickerOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            var openOptions = new AppFilePickerOpenOptions(
                title: options.Title,
                defaultExtension: options.DefaultExtension,
                filters: options.Filters,
                suggestedFileName: options.SuggestedFileName);
            return OpenFileAsync(openOptions, ct);
        }

        public Task<PickedFile?> SaveFileAsync(FilePickerOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            var saveOptions = new AppFilePickerSaveOptions(
                title: options.Title,
                defaultExtension: options.DefaultExtension,
                filters: options.Filters,
                suggestedFileName: options.SuggestedFileName);
            return SaveFileAsync(saveOptions, ct);
        }

        public static PickedFile WrapStorageFile(IStorageFile file)
        {
            ArgumentNullException.ThrowIfNull(file);

            string? localPath = null;
            try
            {
                localPath = file.TryGetLocalPath();
            }
            catch
            {
                // In non-local file systems (e.g. cloud/sandboxed storage), local path might be unavailable
            }

            return new PickedFile
            {
                FileName = file.Name,
                LocalFilePath = localPath,
                OpenReadAsync = () => file.OpenReadAsync(),
                OpenWriteAsync = () => file.OpenWriteAsync()
            };
        }

        public static List<FilePickerFileType>? ConvertFilters(IReadOnlyList<FileFilterItem>? filters)
        {
            if (filters == null || filters.Count == 0)
            {
                return null;
            }

            var result = new List<FilePickerFileType>(filters.Count);
            foreach (var item in filters)
            {
                var patterns = item.Extensions?.Count > 0
                    ? item.Extensions.Select(ext => ext.StartsWith("*.") ? ext : (ext.StartsWith(".") ? $"*{ext}" : $"*.{ext}")).ToList()
                    : new List<string> { "*.*" };

                result.Add(new FilePickerFileType(item.Name)
                {
                    Patterns = patterns
                });
            }

            return result;
        }

        private static IStorageProvider? ResolveStorageProvider()
        {
            try
            {
                if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var activeWindow = desktop.Windows.FirstOrDefault(w => w.IsActive && w.IsVisible) ?? desktop.MainWindow;
                    if (activeWindow?.StorageProvider != null)
                    {
                        return activeWindow.StorageProvider;
                    }

                    if (desktop.MainWindow?.StorageProvider != null)
                    {
                        return desktop.MainWindow.StorageProvider;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogWarning($"Failed to resolve TopLevel StorageProvider: {ex.Message}");
            }

            return null;
        }
    }
}
