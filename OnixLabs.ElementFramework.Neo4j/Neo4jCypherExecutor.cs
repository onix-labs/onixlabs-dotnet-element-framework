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

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the Neo4j implementation of <see cref="IRawStatementExecutor"/> that runs raw Cypher queries against the bound endpoint with ambient-transaction routing.
/// </summary>
/// <remarks>
/// When <see cref="IGraphTransactionOpener.Active"/> is set, the executor runs the query through the active transaction's <see cref="IAsyncTransaction.RunAsync(string, object)"/>; otherwise it opens a fresh auto-commit <see cref="IAsyncSession"/> per call. <b>Async path streams records lazily</b> — the cursor is iterated as the consumer pulls rows, and the underlying session (auto-commit) is closed when the consumer disposes the enumerator. Open-time failures still surface as <see cref="RawStatementException"/>; mid-stream failures during enumeration flow as raw Neo4j driver exceptions, which is the trade-off lazy streaming makes for not OOMing on large result sets. The <b>sync path materializes eagerly</b> — <see cref="Execute"/> drains the entire cursor into a list before returning, so sync consumers see the pre-streaming exception model (every failure surfaces at execute time, wrapped). Both surfaces ultimately route through the same streaming primitive. The sync surface bridges to the async driver via <c>GetAwaiter().GetResult()</c>.
/// </remarks>
internal sealed class Neo4jCypherExecutor : IRawStatementExecutor
{
    /// <summary>The lazy handle to the shared <see cref="IDriver"/> for the bound Neo4j endpoint.</summary>
    private readonly Lazy<IDriver> driver;

    /// <summary>The transaction opener whose ambient slot decides the routing target.</summary>
    private readonly IGraphTransactionOpener transactionOpener;

    /// <summary>The logger this executor writes diagnostic events to.</summary>
    private readonly ILogger<Neo4jCypherExecutor> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4jCypherExecutor"/> class.
    /// </summary>
    /// <param name="driver">A lazy handle to the shared <see cref="IDriver"/> for the bound Neo4j endpoint, used for auto-commit fallback. Resolution defers to first execute, allowing the connection string to be deferred past <c>AddGraphContext</c> registration time.</param>
    /// <param name="transactionOpener">The <see cref="IGraphTransactionOpener"/> whose <see cref="IGraphTransactionOpener.Active"/> indicates the routing target.</param>
    /// <param name="logger">The typed logger this executor writes diagnostic events to. Pass <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}.Instance"/> to disable logging.</param>
    internal Neo4jCypherExecutor(Lazy<IDriver> driver, IGraphTransactionOpener transactionOpener, ILogger<Neo4jCypherExecutor> logger)
    {
        this.driver = driver;
        this.transactionOpener = transactionOpener;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public IEnumerable<IReadOnlyDictionary<string, object?>> Execute(string statement, IReadOnlyDictionary<string, object?> parameters)
    {
        try
        {
            return MaterializeAllAsync(statement, parameters, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (RawStatementException)
        {
            // The streaming primitive already wrapped the open-time failure; preserve its message and inner.
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to execute Cypher statement: {Statement}", statement);
            throw new RawStatementException(
                "Failed to execute the supplied Cypher statement against the Neo4j endpoint.", exception);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        string statement,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken token = default) => StreamAsync(statement, parameters, token);

    /// <summary>
    /// Eager-materialization adapter over <see cref="StreamAsync"/> for the sync surface. Collects every yielded row into a list so the caller sees the pre-streaming exception model — every failure (open-time or mid-stream) surfaces at execute time rather than at enumeration time.
    /// </summary>
    /// <param name="statement">The Cypher statement to execute.</param>
    /// <param name="parameters">The parameter bindings for the statement.</param>
    /// <param name="token">A <see cref="CancellationToken"/> that cancels the collection.</param>
    /// <returns>Returns a task that resolves to the materialized rows.</returns>
    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> MaterializeAllAsync(
        string statement,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken token)
    {
        List<IReadOnlyDictionary<string, object?>> rows = [];
        await foreach (IReadOnlyDictionary<string, object?> row in StreamAsync(statement, parameters, token).WithCancellation(token).ConfigureAwait(false))
            rows.Add(row);
        return rows;
    }

    /// <summary>
    /// Executes the supplied Cypher <paramref name="cypher"/> and yields each row as it arrives from the driver. The auto-commit session is held open for the lifetime of the enumerator and closed via <c>await using</c> when enumeration completes or the consumer disposes the enumerator. Ambient-transaction queries reuse the transaction's connection and never close it here. Initial open-time failures are wrapped as <see cref="RawStatementException"/>; mid-stream driver exceptions propagate raw.
    /// </summary>
    /// <param name="cypher">The Cypher statement to execute.</param>
    /// <param name="parameters">The parameter bindings for the statement.</param>
    /// <param name="token">A <see cref="CancellationToken"/> that cancels the enumeration.</param>
    /// <returns>Returns an asynchronous enumeration of result rows keyed by alias.</returns>
    private async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> StreamAsync(
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        [EnumeratorCancellation] CancellationToken token)
    {
        Dictionary<string, object?> driverParameters = parameters.ToDictionary(kv => kv.Key, kv => PropertySerializer.Serialize(kv.Value));

        if (transactionOpener.Active is Neo4jGraphTransaction ambient)
        {
            IResultCursor cursor;
            try
            {
                logger.LogDebug("Executing Cypher within ambient transaction ({ParameterCount} parameters): {Statement}", parameters.Count, cypher);
                cursor = await ambient.Transaction.RunAsync(cypher, driverParameters).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to execute Cypher statement: {Statement}", cypher);
                throw new RawStatementException(
                    "Failed to execute the supplied Cypher statement against the Neo4j endpoint.", exception);
            }

            await foreach (IRecord record in cursor.WithCancellation(token).ConfigureAwait(false))
                yield return ToRow(record);

            yield break;
        }

        // The session must outlive the cursor; `await using` defers its close until the iterator falls
        // out of scope (consumer-side enumerator dispose or normal completion).
        await using IAsyncSession session = driver.Value.AsyncSession();
        IResultCursor autoCursor;
        try
        {
            logger.LogDebug("Executing Cypher in auto-commit session ({ParameterCount} parameters): {Statement}", parameters.Count, cypher);
            autoCursor = await session.RunAsync(cypher, driverParameters).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to execute Cypher statement: {Statement}", cypher);
            throw new RawStatementException(
                "Failed to execute the supplied Cypher statement against the Neo4j endpoint.", exception);
        }

        await foreach (IRecord record in autoCursor.WithCancellation(token).ConfigureAwait(false))
            yield return ToRow(record);
    }

    /// <summary>
    /// Projects a Bolt <see cref="IRecord"/> into the alias-keyed row dictionary the framework expects.
    /// </summary>
    /// <param name="record">The record whose values to copy.</param>
    /// <returns>Returns the row.</returns>
    private static IReadOnlyDictionary<string, object?> ToRow(IRecord record)
    {
        Dictionary<string, object?> row = new(record.Values.Count);
        foreach (KeyValuePair<string, object> entry in record.Values) row[entry.Key] = entry.Value;
        return row;
    }
}
