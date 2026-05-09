// MIT License

// Copyright (c) 2020 ONIXLabs

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents a decorator over an <see cref="IGraphTransaction"/> that keeps the owning context's identity map in sync with the underlying transaction's outcome.
/// </summary>
/// <remarks>
/// Without this hook, the in-memory identity map would still report nodes whose underlying writes had been reverted at the database, and <see cref="INodeSet{T}.FindById"/> would return stale references. The decorator resets the tracker whenever the underlying transaction is rolled back, exactly once. The terminal calls behave as follows:
/// <list type="bullet">
/// <item><description><see cref="Commit"/> / <see cref="CommitAsync"/> on success: the tracker is preserved because the writes were committed and the in-memory state matches the store.</description></item>
/// <item><description><see cref="Rollback"/> / <see cref="RollbackAsync"/> on success: the tracker is reset.</description></item>
/// <item><description><see cref="Dispose"/> / <see cref="DisposeAsync"/> without a prior successful commit or rollback: the tracker is reset because the inner transaction performs a best-effort rollback at dispose. This covers dispose-only, dispose-after-failed-commit, dispose-after-failed-rollback, and dispose-after-successful-rollback (idempotent — the rollback's own reset already cleared it).</description></item>
/// </list>
/// </remarks>
/// <param name="inner">The provider-supplied <see cref="IGraphTransaction"/> that performs the actual transactional work.</param>
/// <param name="tracker">The owning context's <see cref="IChangeTracker"/> that the decorator keeps in sync with the underlying transaction's outcome.</param>
internal sealed class RollbackAwareGraphTransaction(IGraphTransaction inner, IChangeTracker tracker) : IGraphTransaction
{
    private bool resetOnDispose = true;

    /// <inheritdoc/>
    public void Commit()
    {
        inner.Commit();
        resetOnDispose = false;
    }

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken token = default)
    {
        await inner.CommitAsync(token).ConfigureAwait(false);
        resetOnDispose = false;
    }

    /// <inheritdoc/>
    public void Rollback()
    {
        inner.Rollback();
        tracker.Reset();
        resetOnDispose = false;
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken token = default)
    {
        await inner.RollbackAsync(token).ConfigureAwait(false);
        tracker.Reset();
        resetOnDispose = false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        inner.Dispose();
        if (resetOnDispose)
            tracker.Reset();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync().ConfigureAwait(false);
        if (resetOnDispose)
            tracker.Reset();
    }
}
