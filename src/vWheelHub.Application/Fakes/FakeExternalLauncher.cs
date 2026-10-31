using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Application.Fakes
{
    /// <summary>
    /// Test and preview fake for IExternalLauncher with invocation tracking and cancellation support.
    /// </summary>
    public sealed class FakeExternalLauncher : IExternalLauncher
    {
        private readonly List<Uri> _recordedUris = new();
        private readonly List<string> _recordedDirectories = new();

        public bool OpenUriResult { get; set; } = true;
        public bool OpenDirectoryResult { get; set; } = true;

        public int OpenUriCallCount { get; private set; }
        public int OpenDirectoryCallCount { get; private set; }

        public IReadOnlyList<Uri> RecordedUris => _recordedUris;
        public IReadOnlyList<string> RecordedDirectories => _recordedDirectories;

        public Task<bool> OpenUriAsync(Uri uri, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ct.ThrowIfCancellationRequested();

            OpenUriCallCount++;
            _recordedUris.Add(uri);

            return Task.FromResult(OpenUriResult);
        }

        public Task<bool> OpenDirectoryAsync(string directoryPath, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(directoryPath);
            ct.ThrowIfCancellationRequested();

            OpenDirectoryCallCount++;
            _recordedDirectories.Add(directoryPath);

            return Task.FromResult(OpenDirectoryResult);
        }

        public void Reset()
        {
            OpenUriCallCount = 0;
            OpenDirectoryCallCount = 0;
            _recordedUris.Clear();
            _recordedDirectories.Clear();
            OpenUriResult = true;
            OpenDirectoryResult = true;
        }
    }
}
