using System.Threading;

namespace reromanlee.Mocker
{
    /// <summary>
    /// Marks an implementor requiring asynchronous initialization before steady-state use.
    /// Each implementor selects its own awaitable contract (such as <c>Task</c>, <c>ValueTask</c>,
    /// <c>UniTask</c>, or Unity 2023.1+ <c>Awaitable</c>).
    /// </summary>
    /// <typeparam name="TAwaitable">Awaitable type returned by <see cref="InitializeAsync"/>.</typeparam>
    public interface IAsyncInitializable<out TAwaitable>
    {
        /// <summary>
        /// Runs asynchronous startup work after synchronous composite construction finishes.
        /// </summary>
        /// <param name="cancellationToken">Token cancelled when caller cancels or composite is disposed.</param>
        /// <returns>Awaitable representing startup completion.</returns>
        TAwaitable InitializeAsync(CancellationToken cancellationToken);
    }
}
