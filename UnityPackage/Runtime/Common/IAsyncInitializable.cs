using System.Threading;

namespace reromanlee.Mocker
{
    /// <summary>
    /// Implemented by an implementor that needs to do work before it can be used. The awaitable type is
    /// yours to pick, so an implementor can use Task, UniTask or Awaitable to suit the platform it runs on
    /// without forcing that choice on anything else.
    /// </summary>
    /// <typeparam name="TAwaitable">What InitializeAsync returns, such as Task or UniTask.</typeparam>
    public interface IAsyncInitializable<out TAwaitable>
    {
        /// <summary>
        /// Does the work that could not happen in the constructor.
        /// </summary>
        /// <param name="cancellationToken">Cancelled when the composite is disposed.</param>
        /// <returns>Something that can be awaited.</returns>
        TAwaitable InitializeAsync(CancellationToken cancellationToken);
    }
}
