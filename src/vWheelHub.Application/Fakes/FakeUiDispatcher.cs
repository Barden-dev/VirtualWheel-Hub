using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Application.Fakes
{
    /// <summary>
    /// Test and preview fake for IUiDispatcher with call counters, action capture, and cancellation support.
    /// </summary>
    public sealed class FakeUiDispatcher : IUiDispatcher
    {
        private readonly List<Action> _postedActions = new();
        private readonly List<Action> _invokedActions = new();

        public bool AccessResult { get; set; } = true;
        public bool ExecuteImmediately { get; set; } = true;

        public int CheckAccessCallCount { get; private set; }
        public int PostCallCount { get; private set; }
        public int InvokeActionCallCount { get; private set; }
        public int InvokeFuncCallCount { get; private set; }

        public IReadOnlyList<Action> PostedActions => _postedActions;
        public IReadOnlyList<Action> InvokedActions => _invokedActions;

        public bool CheckAccess()
        {
            CheckAccessCallCount++;
            return AccessResult;
        }

        public void Post(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            PostCallCount++;
            _postedActions.Add(action);

            if (ExecuteImmediately)
            {
                action();
            }
        }

        public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(action);
            cancellationToken.ThrowIfCancellationRequested();

            InvokeActionCallCount++;
            _invokedActions.Add(action);

            if (ExecuteImmediately)
            {
                action();
            }

            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(callback);
            cancellationToken.ThrowIfCancellationRequested();

            InvokeFuncCallCount++;

            T result = ExecuteImmediately ? callback() : default!;
            return Task.FromResult(result);
        }

        public void DrainPostedActions()
        {
            foreach (var action in _postedActions.ToArray())
            {
                action();
            }
        }

        public void Reset()
        {
            CheckAccessCallCount = 0;
            PostCallCount = 0;
            InvokeActionCallCount = 0;
            InvokeFuncCallCount = 0;
            _postedActions.Clear();
            _invokedActions.Clear();
        }
    }
}
