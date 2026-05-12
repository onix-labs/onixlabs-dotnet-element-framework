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

using Microsoft.Extensions.Logging;
using Npgsql;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents a single Apache AGE unit of work that wraps the <see cref="NpgsqlConnection"/> and <see cref="NpgsqlTransaction"/> returned by <see cref="AgeGraphTransactionOpener"/>.
/// </summary>
/// <remarks>
/// Commit and rollback drive the underlying transaction; dispose returns the connection to the data source pool and rolls back the transaction first if neither commit nor rollback ran. Sync paths bridge to async via <c>GetAwaiter().GetResult()</c>. Every terminal call (commit, rollback, dispose) clears the opener's ambient slot exactly once so that <see cref="IGraphTransactionOpener.Open"/> can be invoked again afterwards. The terminal is one-shot: a second commit or rollback after the first is a no-op.
/// </remarks>
internal sealed class AgeGraphTransaction : IGraphTransaction
{
    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;
    private readonly AgeGraphTransactionOpener opener;
    private readonly ILogger<AgeGraphTransaction> logger;
    private bool closed;

    internal AgeGraphTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction, AgeGraphTransactionOpener opener, ILogger<AgeGraphTransaction> logger)
    {
        this.connection = connection;
        this.transaction = transaction;
        this.opener = opener;
        this.logger = logger;
    }

    /// <summary>
    /// Gets the underlying connection that <see cref="AgeRawStatementExecutor"/> routes ambient queries through.
    /// </summary>
    internal NpgsqlConnection Connection => connection;

    /// <summary>
    /// Gets the underlying transaction that ambient queries enlist on.
    /// </summary>
    internal NpgsqlTransaction Transaction => transaction;

    /// <inheritdoc/>
    public void Commit() => CommitAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken token = default)
    {
        if (closed) return;

        try
        {
            await transaction.CommitAsync(token).ConfigureAwait(false);
            logger.LogInformation("AGE graph transaction committed.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to commit AGE graph transaction.");
            throw new GraphTransactionException("Failed to commit the active graph transaction against the Apache AGE endpoint.", exception);
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
            await transaction.RollbackAsync(token).ConfigureAwait(false);
            logger.LogInformation("AGE graph transaction rolled back.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to roll back AGE graph transaction.");
            throw new GraphTransactionException("Failed to roll back the active graph transaction against the Apache AGE endpoint.", exception);
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
            logger.LogInformation("AGE graph transaction disposed without explicit commit/rollback; best-effort rollback succeeded.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Best-effort rollback during AGE transaction dispose failed.");
        }

        await CloseAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Releases the underlying transaction and connection back to the pool, and clears the opener's ambient slot exactly once.
    /// </summary>
    private async ValueTask CloseAsync()
    {
        if (closed) return;
        closed = true;

        try
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to dispose AGE transaction during terminal.");
        }

        try
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to return AGE connection to the pool during terminal; ambient slot will still be cleared.");
        }

        opener.ClearActive();
    }
}
