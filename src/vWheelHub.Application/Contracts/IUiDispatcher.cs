using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Threading dispatcher abstraction for UI operations.
    /// Ensures background workers safely marshal updates to the UI thread.
    /// </summary>
    public interface IUiDispatcher
    {
        /// <summary>
        /// Checks whether the current thread is the UI dispatcher thread.
        /// </summary>
        bool CheckAccess();

        /// <summary>
        /// Posts an asynchronous action to the dispatcher queue without awaiting its completion.
        /// </summary>
        void Post(Action action);

        /// <summary>
        /// Executes an asynchronous action on the UI thread and awaits its completion.
        /// </summary>
        Task InvokeAsync(Action action, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an asynchronous function on the UI thread and returns its result.
        /// </summary>
        Task<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default);
    }
}
