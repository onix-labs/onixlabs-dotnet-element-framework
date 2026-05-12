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
/// Represents the Apache AGE implementation of <see cref="IGraphTransactionOpener"/> that opens fresh Npgsql transactions on demand and holds the canonical ambient transaction for the bound context.
/// </summary>
/// <remarks>
/// Each successful <see cref="Open"/> borrows a <see cref="NpgsqlConnection"/> from the data source, begins an <see cref="NpgsqlTransaction"/>, wraps both in an <see cref="AgeGraphTransaction"/>, and stores the wrapper as the ambient transaction; the wrapper calls <see cref="ClearActive"/> on commit, rollback, or dispose. V1 is one-ambient-at-a-time: opening a second transaction while another is active throws <see cref="GraphTransactionAlreadyActiveException"/>. Sync paths bridge to async via <c>GetAwaiter().GetResult()</c>.
/// </remarks>
internal sealed class AgeGraphTransactionOpener : IGraphTransactionOpener
{
    private readonly NpgsqlDataSource dataSource;
    private readonly ILogger<AgeGraphTransactionOpener> logger;
    private readonly ILogger<AgeGraphTransaction> transactionLogger;
    private AgeGraphTransaction? active;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgeGraphTransactionOpener"/> class.
    /// </summary>
    /// <param name="dataSource">The shared <see cref="NpgsqlDataSource"/> the opener borrows connections from.</param>
    /// <param name="logger">The typed logger this opener writes diagnostic events to.</param>
    /// <param name="transactionLogger">The typed logger every spawned <see cref="AgeGraphTransaction"/> receives. Pass <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}.Instance"/> for both arguments to disable logging.</param>
    internal AgeGraphTransactionOpener(NpgsqlDataSource dataSource, ILogger<AgeGraphTransactionOpener> logger, ILogger<AgeGraphTransaction> transactionLogger)
    {
        this.dataSource = dataSource;
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
    /// Clears the ambient transaction slot so that <see cref="Open"/> can be invoked again. Called by <see cref="AgeGraphTransaction"/> on terminal.
    /// </summary>
    internal void ClearActive() => active = null;

    private async Task<AgeGraphTransaction> OpenInternalAsync(CancellationToken token)
    {
        if (active is not null)
            throw new GraphTransactionAlreadyActiveException(
                "An ambient graph transaction is already active for this context. Commit, roll back, or dispose it before opening another.");

        NpgsqlConnection connection;
        NpgsqlTransaction transaction;

        try
        {
            connection = await dataSource.OpenConnectionAsync(token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to open AGE connection.");
            throw new GraphTransactionException("Failed to open a graph transaction against the Apache AGE endpoint.", exception);
        }

        try
        {
            transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to begin AGE transaction; closing connection.");
            await connection.DisposeAsync().ConfigureAwait(false);
            throw new GraphTransactionException("Failed to open a graph transaction against the Apache AGE endpoint.", exception);
        }

        AgeGraphTransaction wrapped = new(connection, transaction, this, transactionLogger);
        active = wrapped;
        logger.LogInformation("AGE graph transaction opened.");
        return wrapped;
    }
}
