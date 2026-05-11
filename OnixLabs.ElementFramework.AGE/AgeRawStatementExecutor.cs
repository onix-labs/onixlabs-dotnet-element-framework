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
/// The executor expects each statement to already be a complete <c>SELECT * FROM cypher(...) AS (...)</c> expression — <see cref="AgeCypherEmitter"/> emits this shape directly. The parameter dictionary is JSON-encoded via <see cref="AgtypeWriter"/> and bound to the single SQL parameter <c>@p</c> using <see cref="NpgsqlDbType.Unknown"/> so Postgres infers <c>agtype</c> on its side. Every command runs with <c>AllResultTypesAreUnknown</c> set, which forces the driver to return columns as text strings — the only reliable way to read agtype through Npgsql. When <see cref="IGraphTransactionOpener.Active"/> is set the executor reuses the ambient <see cref="NpgsqlConnection"/>/<see cref="NpgsqlTransaction"/>; otherwise it opens a fresh connection from the data source for the duration of the call. The sync surface bridges to async via <c>GetAwaiter().GetResult()</c>.
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
            return RunAsync(statement, parameters, CancellationToken.None).GetAwaiter().GetResult();
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
        CancellationToken token = default) => ExecuteAsyncCore(statement, parameters, token);

    private async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ExecuteAsyncCore(
        string statement,
        IReadOnlyDictionary<string, object?> parameters,
        [EnumeratorCancellation] CancellationToken token)
    {
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows;
        try
        {
            rows = await RunAsync(statement, parameters, token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to execute AGE statement: {Statement}", statement);
            throw new RawStatementException(
                "Failed to execute the supplied statement against the Apache AGE endpoint.", exception);
        }

        foreach (IReadOnlyDictionary<string, object?> row in rows)
            yield return row;
    }

    /// <summary>
    /// Runs the supplied SQL against the ambient transaction's connection, or a fresh connection from the data source, and returns the materialized rows.
    /// </summary>
    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> RunAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken token)
    {
        if (transactionOpener.Active is AgeGraphTransaction ambient)
        {
            logger.LogDebug("Executing AGE statement within ambient transaction ({ParameterCount} parameters): {Statement}", parameters.Count, sql);
            await using NpgsqlCommand ambientCommand = ambient.Connection.CreateCommand();
            ambientCommand.CommandText = sql;
            ambientCommand.Transaction = ambient.Transaction;
            BindParameters(ambientCommand, parameters);
            return await MaterializeAsync(ambientCommand, token).ConfigureAwait(false);
        }

        logger.LogDebug("Executing AGE statement on auto-commit connection ({ParameterCount} parameters): {Statement}", parameters.Count, sql);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token).ConfigureAwait(false);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command, parameters);
        return await MaterializeAsync(command, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Materializes every record yielded by <paramref name="command"/> into a list of alias-keyed dictionaries, with every column read as a text string.
    /// </summary>
    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> MaterializeAsync(NpgsqlCommand command, CancellationToken token)
    {
        command.AllResultTypesAreUnknown = true;
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);

        List<IReadOnlyDictionary<string, object?>> rows = [];
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            Dictionary<string, object?> row = new(reader.FieldCount);
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, token).ConfigureAwait(false)
                    ? null
                    : reader.GetFieldValue<string>(i);
            rows.Add(row);
        }
        return rows;
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
