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

using Neo4j.Driver;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents a single Neo4j unit of work that wraps the <see cref="IAsyncSession"/> and <see cref="IAsyncTransaction"/> returned by <see cref="Neo4jGraphTransactionOpener"/>.
/// </summary>
/// <remarks>
/// Commit and rollback drive the underlying transaction; dispose closes the underlying session and rolls back the transaction first if neither commit nor rollback ran. Sync paths bridge to async via <c>GetAwaiter().GetResult()</c>. Every terminal call (commit, rollback, dispose) clears the opener's ambient slot exactly once so that <see cref="IGraphTransactionOpener.Open"/> can be invoked again afterwards. The terminal is one-shot: a second commit or rollback after the first is a no-op.
/// </remarks>
internal sealed class Neo4jGraphTransaction : IGraphTransaction
{
    private readonly IAsyncSession session;
    private readonly IAsyncTransaction transaction;
    private readonly Neo4jGraphTransactionOpener opener;
    private bool closed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4jGraphTransaction"/> class.
    /// </summary>
    /// <param name="session">The Neo4j async session that the opener obtained from <see cref="IDriver.AsyncSession()"/>.</param>
    /// <param name="transaction">The async transaction begun on <paramref name="session"/>.</param>
    /// <param name="opener">The opener that produced this transaction; receives the ambient-clear callback on terminal.</param>
    internal Neo4jGraphTransaction(IAsyncSession session, IAsyncTransaction transaction, Neo4jGraphTransactionOpener opener)
    {
        this.session = session;
        this.transaction = transaction;
        this.opener = opener;
    }

    /// <summary>
    /// Gets the underlying async transaction that <see cref="Neo4jCypherExecutor"/> routes ambient queries through.
    /// </summary>
    /// <value>The wrapped <see cref="IAsyncTransaction"/>.</value>
    internal IAsyncTransaction Transaction => transaction;

    /// <inheritdoc/>
    public void Commit() => CommitAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken token = default)
    {
        if (closed) return;

        try
        {
            await transaction.CommitAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new GraphTransactionException("Failed to commit the active graph transaction against the Neo4j endpoint.", exception);
        }
        finally
        {
            await CloseAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public void Rollback() => RollbackAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken token = default)
    {
        if (closed) return;

        try
        {
            await transaction.RollbackAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new GraphTransactionException("Failed to roll back the active graph transaction against the Neo4j endpoint.", exception);
        }
        finally
        {
            await CloseAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (closed) return;

        try
        {
            await transaction.RollbackAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort rollback during dispose; the underlying session disposal still runs below.
        }

        await CloseAsync().ConfigureAwait(false);
    }

    private async ValueTask CloseAsync()
    {
        if (closed) return;
        closed = true;

        try
        {
            await session.CloseAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort session close; the ambient slot must still be cleared below so the opener accepts a new Open call.
        }

        opener.ClearActive();
    }
}
