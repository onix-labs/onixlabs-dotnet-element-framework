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

using ArangoDBNetStandard;
using Microsoft.Extensions.Logging;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents a single ArangoDB unit of work that wraps a Stream Transaction by ID.
/// </summary>
/// <remarks>
/// Commit and rollback issue <c>PUT /_api/transaction/{id}</c> and <c>DELETE /_api/transaction/{id}</c> respectively against the underlying <see cref="ArangoDBClient"/>; dispose performs a best-effort abort if neither commit nor rollback ran. Sync paths bridge to async via <c>GetAwaiter().GetResult()</c>. Every terminal call clears the opener's ambient slot exactly once so that <see cref="IGraphTransactionOpener.Open"/> can be invoked again afterwards. The terminal is one-shot: a second commit or rollback after the first is a no-op.
/// </remarks>
internal sealed class ArangoGraphTransaction : IGraphTransaction
{
    private readonly ArangoDBClient client;
    private readonly ArangoGraphTransactionOpener opener;
    private readonly ILogger<ArangoGraphTransaction> logger;
    private bool closed;

    internal ArangoGraphTransaction(ArangoDBClient client, string transactionId, ArangoGraphTransactionOpener opener, ILogger<ArangoGraphTransaction> logger)
    {
        this.client = client;
        TransactionId = transactionId;
        this.opener = opener;
        this.logger = logger;
    }

    /// <summary>
    /// Gets the Stream Transaction ID assigned by ArangoDB. Used by <see cref="ArangoRawStatementExecutor"/> to populate the <c>x-arango-trx-id</c> request header on ambient routing.
    /// </summary>
    internal string TransactionId { get; }

    /// <inheritdoc/>
    public void Commit() => CommitAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken token = default)
    {
        if (closed) return;

        try
        {
            await client.Transaction.CommitTransaction(TransactionId, token).ConfigureAwait(false);
            logger.LogInformation("Arango stream transaction committed ({TransactionId}).", TransactionId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to commit Arango stream transaction ({TransactionId}).", TransactionId);
            throw new GraphTransactionException("Failed to commit the active graph transaction against the ArangoDB endpoint.", exception);
        }
        finally
        {
            Close();
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
            await client.Transaction.AbortTransaction(TransactionId, token).ConfigureAwait(false);
            logger.LogInformation("Arango stream transaction rolled back ({TransactionId}).", TransactionId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to abort Arango stream transaction ({TransactionId}).", TransactionId);
            throw new GraphTransactionException("Failed to roll back the active graph transaction against the ArangoDB endpoint.", exception);
        }
        finally
        {
            Close();
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
            await client.Transaction.AbortTransaction(TransactionId).ConfigureAwait(false);
            logger.LogInformation("Arango stream transaction disposed without explicit commit/rollback; best-effort abort succeeded ({TransactionId}).", TransactionId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Best-effort abort during Arango transaction dispose failed ({TransactionId}).", TransactionId);
        }

        Close();
    }

    private void Close()
    {
        if (closed) return;
        closed = true;
        opener.ClearActive();
    }
}
