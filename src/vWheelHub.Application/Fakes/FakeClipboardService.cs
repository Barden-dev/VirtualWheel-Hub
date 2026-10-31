using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Application.Fakes
{
    /// <summary>
    /// Test and preview fake for IClipboardService with in-memory storage, history tracking, and cancellation support.
    /// </summary>
    public sealed class FakeClipboardService : IClipboardService
    {
        private readonly List<string> _recordedSetTexts = new();

        public string? CurrentText { get; set; }
        public bool ThrowOnAccess { get; set; }

        public int GetTextCallCount { get; private set; }
        public int SetTextCallCount { get; private set; }

        public IReadOnlyList<string> RecordedSetTexts => _recordedSetTexts;

        public Task<string?> GetTextAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ThrowOnAccess)
                throw new InvalidOperationException("Simulated clipboard access failure.");

            GetTextCallCount++;
            return Task.FromResult(CurrentText);
        }

        public Task SetTextAsync(string text, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(text);
            ct.ThrowIfCancellationRequested();
            if (ThrowOnAccess)
                throw new InvalidOperationException("Simulated clipboard access failure.");

            SetTextCallCount++;
            CurrentText = text;
            _recordedSetTexts.Add(text);

            return Task.CompletedTask;
        }

        public void Reset()
        {
            GetTextCallCount = 0;
            SetTextCallCount = 0;
            CurrentText = null;
            ThrowOnAccess = false;
            _recordedSetTexts.Clear();
        }
    }
}
