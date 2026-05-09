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
/// Represents the Neo4j implementation of <see cref="IRawStatementExecutor"/> that runs raw Cypher queries against the bound endpoint with ambient-transaction routing.
/// </summary>
/// <remarks>
/// When <see cref="IGraphTransactionOpener.Active"/> is set, the executor runs the query through the active transaction's <see cref="IAsyncTransaction.RunAsync(string, object)"/>; otherwise it opens a fresh auto-commit <see cref="IAsyncSession"/> per call. Result rows are materialized eagerly so faults during the driver round-trip surface immediately rather than at enumeration time. The sync surface bridges to the async driver via <c>GetAwaiter().GetResult()</c>.
/// </remarks>
/// <param name="driver">A lazy handle to the shared <see cref="IDriver"/> for the bound Neo4j endpoint, used for auto-commit fallback. Resolution defers to first execute, allowing the connection string to be deferred past <c>AddGraphContext</c> registration time.</param>
/// <param name="transactionOpener">The <see cref="IGraphTransactionOpener"/> whose <see cref="IGraphTransactionOpener.Active"/> indicates the routing target.</param>
internal sealed class Neo4jCypherExecutor(Lazy<IDriver> driver, IGraphTransactionOpener transactionOpener) : IRawStatementExecutor
{
    /// <inheritdoc/>
    public IEnumerable<IReadOnlyDictionary<string, object?>> Execute(string statement, IReadOnlyDictionary<string, object?> parameters)
    {
        try
        {
            return RunAsync(statement, parameters, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            throw new RawStatementException(
                "Failed to execute the supplied Cypher statement against the Neo4j endpoint.", exception);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ExecuteAsync(string statement, IReadOnlyDictionary<string, object?> parameters, CancellationToken token = default) =>
        ExecuteAsyncCore(statement, parameters, token);

    private async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ExecuteAsyncCore(string statement, IReadOnlyDictionary<string, object?> parameters, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows;
        try
        {
            rows = await RunAsync(statement, parameters, token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new RawStatementException(
                "Failed to execute the supplied Cypher statement against the Neo4j endpoint.", exception);
        }

        foreach (IReadOnlyDictionary<string, object?> row in rows)
            yield return row;
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> RunAsync(string cypher, IReadOnlyDictionary<string, object?> parameters, CancellationToken token)
    {
        Dictionary<string, object?> driverParameters = parameters.ToDictionary(kv => kv.Key, kv => PropertySerializer.Serialize(kv.Value));

        if (transactionOpener.Active is Neo4jGraphTransaction ambient)
        {
            IResultCursor cursor = await ambient.Transaction.RunAsync(cypher, driverParameters).ConfigureAwait(false);
            return await MaterializeAsync(cursor, token).ConfigureAwait(false);
        }

        await using IAsyncSession session = driver.Value.AsyncSession();
        IResultCursor autoCursor = await session.RunAsync(cypher, driverParameters).ConfigureAwait(false);
        return await MaterializeAsync(autoCursor, token).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> MaterializeAsync(IResultCursor cursor, CancellationToken token)
    {
        List<IRecord> records = await cursor.ToListAsync(token).ConfigureAwait(false);
        List<IReadOnlyDictionary<string, object?>> rows = new(records.Count);
        foreach (IRecord record in records)
        {
            Dictionary<string, object?> row = new(record.Values.Count);
            foreach (KeyValuePair<string, object> entry in record.Values) row[entry.Key] = entry.Value;
            rows.Add(row);
        }
        return rows;
    }
}
