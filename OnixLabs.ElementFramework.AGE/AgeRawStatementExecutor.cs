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
using Npgsql;
using NpgsqlTypes;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the Apache AGE implementation of <see cref="IRawStatementExecutor"/> that runs SQL-wrapped Cypher statements against the bound endpoint with ambient-transaction routing.
/// </summary>
/// <remarks>
/// The executor expects each statement to already be a complete <c>SELECT * FROM cypher(...) AS (...)</c> expression — <see cref="AgeCypherEmitter"/> emits this shape directly. The parameter dictionary is JSON-encoded via <see cref="AgtypeWriter"/> and bound to the single SQL parameter <c>@p</c> using <see cref="NpgsqlDbType.Unknown"/> so Postgres infers <c>agtype</c> on its side. Every command runs with <c>AllResultTypesAreUnknown</c> set, which forces the driver to return columns as text strings — the only reliable way to read agtype through Npgsql. When <see cref="IGraphTransactionOpener.Active"/> is set the executor reuses the ambient <see cref="NpgsqlConnection"/>/<see cref="NpgsqlTransaction"/>; otherwise it opens a fresh connection from the data source for the duration of the call. <b>Async path streams rows lazily</b> — the <see cref="NpgsqlDataReader"/> is iterated as the consumer pulls rows, and the auto-commit connection is returned to the pool when the consumer disposes the enumerator. Open-time failures still surface as <see cref="RawStatementException"/>; mid-stream failures during enumeration flow as raw Npgsql exceptions. The <b>sync path materializes eagerly</b> — <see cref="Execute"/> drains the entire reader into a list before returning. The sync surface bridges to async via <c>GetAwaiter().GetResult()</c>.
/// </remarks>
internal sealed class AgeRawStatementExecutor : IRawStatementExecutor
{
    /// <summary>The shared data source the executor borrows fresh connections from when no ambient transaction is active.</summary>
    private readonly NpgsqlDataSource dataSource;

    /// <summary>The transaction opener whose ambient slot decides the routing target.</summary>
    private readonly IGraphTransactionOpener transactionOpener;

    /// <summary>The logger this executor writes diagnostic events to.</summary>
    private readonly ILogger<AgeRawStatementExecutor> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgeRawStatementExecutor"/> class.
    /// </summary>
    /// <param name="dataSource">The shared <see cref="NpgsqlDataSource"/> the executor borrows connections from for auto-commit fallback. The data source's physical-connection initializer is expected to issue <c>LOAD 'age'</c> and set the search path.</param>
    /// <param name="transactionOpener">The <see cref="IGraphTransactionOpener"/> whose <see cref="IGraphTransactionOpener.Active"/> indicates the routing target.</param>
    /// <param name="logger">The typed logger this executor writes diagnostic events to. Pass <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}.Instance"/> to disable logging.</param>
    internal AgeRawStatementExecutor(NpgsqlDataSource dataSource, IGraphTransactionOpener transactionOpener, ILogger<AgeRawStatementExecutor> logger)
    {
        this.dataSource = dataSource;
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
            logger.LogWarning(exception, "Failed to execute AGE statement: {Statement}", statement);
            throw new RawStatementException(
                "Failed to execute the supplied statement against the Apache AGE endpoint.", exception);
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
    /// <param name="statement">The SQL statement to execute.</param>
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
    /// Executes the supplied SQL and yields each row as it arrives from the reader. The auto-commit connection (and its command / reader) is held open for the lifetime of the enumerator and returned to the pool when enumeration completes or the consumer disposes the enumerator. Ambient-transaction queries reuse the transaction's connection and never close it here. Initial open-time failures are wrapped as <see cref="RawStatementException"/>; mid-stream Npgsql exceptions propagate raw.
    /// </summary>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="parameters">The parameter bindings for the statement.</param>
    /// <param name="token">A <see cref="CancellationToken"/> that cancels the enumeration.</param>
    /// <returns>Returns an asynchronous enumeration of result rows keyed by alias.</returns>
    private async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> StreamAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        [EnumeratorCancellation] CancellationToken token)
    {
        if (transactionOpener.Active is AgeGraphTransaction ambient)
        {
            await using NpgsqlCommand ambientCommand = ambient.Connection.CreateCommand();
            ambientCommand.CommandText = sql;
            ambientCommand.Transaction = ambient.Transaction;
            BindParameters(ambientCommand, parameters);
            ambientCommand.AllResultTypesAreUnknown = true;

            NpgsqlDataReader ambientReader;
            try
            {
                logger.LogDebug("Executing AGE statement within ambient transaction ({ParameterCount} parameters): {Statement}", parameters.Count, sql);
                ambientReader = await ambientCommand.ExecuteReaderAsync(token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to execute AGE statement: {Statement}", sql);
                throw new RawStatementException(
                    "Failed to execute the supplied statement against the Apache AGE endpoint.", exception);
            }

            await using (ambientReader.ConfigureAwait(false))
            {
                while (await ambientReader.ReadAsync(token).ConfigureAwait(false))
                    yield return await ReadRowAsync(ambientReader, token).ConfigureAwait(false);
            }

            yield break;
        }

        // Connection, command, and reader must outlive a single row read; `await using` defers their
        // disposal until the iterator falls out of scope (consumer-side enumerator dispose or normal
        // completion).
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token).ConfigureAwait(false);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command, parameters);
        command.AllResultTypesAreUnknown = true;

        NpgsqlDataReader reader;
        try
        {
            logger.LogDebug("Executing AGE statement on auto-commit connection ({ParameterCount} parameters): {Statement}", parameters.Count, sql);
            reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to execute AGE statement: {Statement}", sql);
            throw new RawStatementException(
                "Failed to execute the supplied statement against the Apache AGE endpoint.", exception);
        }

        await using (reader.ConfigureAwait(false))
        {
            while (await reader.ReadAsync(token).ConfigureAwait(false))
                yield return await ReadRowAsync(reader, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Projects the current row of <paramref name="reader"/> into the alias-keyed row dictionary the framework expects, reading every column as a text-encoded agtype.
    /// </summary>
    /// <param name="reader">The reader positioned at the row to read.</param>
    /// <param name="token">A <see cref="CancellationToken"/> that cancels the per-column reads.</param>
    /// <returns>Returns the row.</returns>
    private static async Task<IReadOnlyDictionary<string, object?>> ReadRowAsync(NpgsqlDataReader reader, CancellationToken token)
    {
        Dictionary<string, object?> row = new(reader.FieldCount);
        for (int i = 0; i < reader.FieldCount; i++)
            row[reader.GetName(i)] = await reader.IsDBNullAsync(i, token).ConfigureAwait(false)
                ? null
                : reader.GetFieldValue<string>(i);
        return row;
    }

    /// <summary>
    /// Binds the supplied parameter dictionary to <paramref name="command"/> as a single <c>@p</c> agtype payload, JSON-encoded by <see cref="AgtypeWriter"/>.
    /// </summary>
    private static void BindParameters(NpgsqlCommand command, IReadOnlyDictionary<string, object?> parameters)
    {
        if (parameters.Count == 0) return;
        string payload = AgtypeWriter.Serialize(parameters);
        command.Parameters.Add(new NpgsqlParameter("p", NpgsqlDbType.Unknown) { Value = payload });
    }
}
