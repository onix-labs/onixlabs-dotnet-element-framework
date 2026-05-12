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
using ArangoDBNetStandard.CollectionApi.Models;
using ArangoDBNetStandard.TransactionApi.Models;
using Microsoft.Extensions.Logging;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the ArangoDB implementation of <see cref="IGraphTransactionOpener"/> that opens fresh Stream Transactions on demand and holds the canonical ambient transaction for the bound context.
/// </summary>
/// <remarks>
/// ArangoDB Stream Transactions require every collection touched by the transaction to be declared up-front; the framework's <see cref="ChangeTracker"/> opens the transaction <i>before</i> the pending queue is drained, so we don't yet know which collections will be involved. The pragmatic workaround is to list every non-system collection in the bound database at <see cref="Open"/> time and declare them all as read+write. This costs one extra HTTP call per transaction begin but avoids coupling the opener to the <see cref="IGraphModel"/>. V1 is one-ambient-at-a-time: opening a second transaction while another is active throws <see cref="GraphTransactionAlreadyActiveException"/>. Sync paths bridge to async via <c>GetAwaiter().GetResult()</c>.
/// </remarks>
internal sealed class ArangoGraphTransactionOpener : IGraphTransactionOpener
{
    private readonly ArangoDBClient client;
    private readonly ILogger<ArangoGraphTransactionOpener> logger;
    private readonly ILogger<ArangoGraphTransaction> transactionLogger;
    private ArangoGraphTransaction? active;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArangoGraphTransactionOpener"/> class.
    /// </summary>
    /// <param name="client">The shared <see cref="ArangoDBClient"/> bound to the target database.</param>
    /// <param name="logger">The typed logger this opener writes diagnostic events to.</param>
    /// <param name="transactionLogger">The typed logger every spawned <see cref="ArangoGraphTransaction"/> receives. Pass <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}.Instance"/> for both arguments to disable logging.</param>
    internal ArangoGraphTransactionOpener(ArangoDBClient client, ILogger<ArangoGraphTransactionOpener> logger, ILogger<ArangoGraphTransaction> transactionLogger)
    {
        this.client = client;
        this.logger = logger;
        this.transactionLogger = transactionLogger;
    }

    /// <inheritdoc/>
    public IGraphTransaction? Active => active;

    /// <inheritdoc/>
    public IGraphTransaction Open() => OpenInternalAsync(CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async Task<IGraphTransaction> OpenAsync(CancellationToken token = default) =>
        await OpenInternalAsync(token).ConfigureAwait(false);

    /// <summary>
    /// Clears the ambient transaction slot so that <see cref="Open"/> can be invoked again. Called by <see cref="ArangoGraphTransaction"/> on terminal.
    /// </summary>
    internal void ClearActive() => active = null;

    private async Task<ArangoGraphTransaction> OpenInternalAsync(CancellationToken token)
    {
        if (active is not null)
            throw new GraphTransactionAlreadyActiveException(
                "An ambient graph transaction is already active for this context. Commit, roll back, or dispose it before opening another.");

        IReadOnlyList<string> collections;
        try
        {
            GetCollectionsResponse response = await client.Collection
                .GetCollectionsAsync(new GetCollectionsQuery { ExcludeSystem = true }, token)
                .ConfigureAwait(false);
            collections = response.Result
                .Where(collection => !collection.Name.StartsWith('_'))
                .Select(collection => collection.Name)
                .ToArray();
            logger.LogDebug("Arango stream transaction will declare collections: {Collections}", string.Join(", ", collections));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to list collections for Arango stream transaction begin.");
            throw new GraphTransactionException("Failed to open a graph transaction against the ArangoDB endpoint.", exception);
        }

        StreamTransactionBody body = new()
        {
            AllowImplicit = true,
            Collections = new PostTransactionRequestCollections
            {
                Read = collections.ToList(),
                Write = collections.ToList()
            }
        };

        string transactionId;
        try
        {
            StreamTransactionResponse response = await client.Transaction.BeginTransaction(body, null, token).ConfigureAwait(false);
            transactionId = response.Result.Id;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to begin Arango stream transaction.");
            throw new GraphTransactionException("Failed to open a graph transaction against the ArangoDB endpoint.", exception);
        }

        ArangoGraphTransaction wrapped = new(client, transactionId, this, transactionLogger);
        active = wrapped;
        logger.LogInformation("Arango stream transaction opened ({TransactionId}).", transactionId);
        return wrapped;
    }
}
