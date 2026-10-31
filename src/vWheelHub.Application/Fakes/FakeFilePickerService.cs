using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Application.Fakes
{
    /// <summary>
    /// Test and preview fake for IFilePickerService with configurable file return, option capture, and cancellation support.
    /// </summary>
    public sealed class FakeFilePickerService : IFilePickerService
    {
        private readonly List<FilePickerOpenOptions> _recordedOpenOptions = new();
        private readonly List<FilePickerSaveOptions> _recordedSaveOptions = new();

        public PickedFile? FileToReturnOnOpen { get; set; }
        public PickedFile? FileToReturnOnSave { get; set; }

        public Func<FilePickerOpenOptions, PickedFile?>? OpenCallback { get; set; }
        public Func<FilePickerSaveOptions, PickedFile?>? SaveCallback { get; set; }

        public int OpenFileCallCount { get; private set; }
        public int SaveFileCallCount { get; private set; }

        public IReadOnlyList<FilePickerOpenOptions> RecordedOpenOptions => _recordedOpenOptions;
        public IReadOnlyList<FilePickerSaveOptions> RecordedSaveOptions => _recordedSaveOptions;

        public Task<PickedFile?> OpenFileAsync(FilePickerOpenOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            ct.ThrowIfCancellationRequested();

            OpenFileCallCount++;
            _recordedOpenOptions.Add(options);

            PickedFile? result = OpenCallback != null
                ? OpenCallback(options)
                : FileToReturnOnOpen;

            return Task.FromResult(result);
        }

        public Task<PickedFile?> SaveFileAsync(FilePickerSaveOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            ct.ThrowIfCancellationRequested();

            SaveFileCallCount++;
            _recordedSaveOptions.Add(options);

            PickedFile? result = SaveCallback != null
                ? SaveCallback(options)
                : FileToReturnOnSave;

            return Task.FromResult(result);
        }

        public Task<PickedFile?> OpenFileAsync(FilePickerOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            return OpenFileAsync(options.ToOpenOptions(), ct);
        }

        public Task<PickedFile?> SaveFileAsync(FilePickerOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            return SaveFileAsync(options.ToSaveOptions(), ct);
        }

        public void Reset()
        {
            OpenFileCallCount = 0;
            SaveFileCallCount = 0;
            _recordedOpenOptions.Clear();
            _recordedSaveOptions.Clear();
            FileToReturnOnOpen = null;
            FileToReturnOnSave = null;
            OpenCallback = null;
            SaveCallback = null;
        }
    }
}
