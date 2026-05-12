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

using System.Diagnostics;
using System.Runtime.CompilerServices;
using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using ArangoDBNetStandard.Transport;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the ArangoDB implementation of <see cref="IRawStatementExecutor"/> that runs AQL statements against the bound database with cursor-paged result streaming.
/// </summary>
/// <remarks>
/// The executor delegates to <see cref="ArangoDBClient.Cursor"/>. Each statement is sent as an AQL string plus a bind-vars dictionary; subsequent batches are fetched via the cursor's pagination API as the consumer pulls rows. <b>Async path streams.</b> <see cref="ExecuteAsync"/> is a real async iterator — the cursor is only advanced as the caller iterates, and the next batch is requested when the current one is exhausted. Open-time failures surface as <see cref="RawStatementException"/>; mid-stream failures during enumeration flow as raw <see cref="ApiErrorException"/>. The <b>sync path materializes eagerly</b> — <see cref="Execute"/> drains the entire stream into a list before returning, so every failure surfaces at execute time wrapped. The sync surface bridges to async via <c>GetAwaiter().GetResult()</c>. Ambient transaction routing is not yet wired — <see cref="IGraphTransactionOpener.Active"/> always returns <see langword="null"/> in the current opener stub.
/// </remarks>
internal sealed class ArangoRawStatementExecutor : IRawStatementExecutor
{
    private const int BatchSize = 100;

    private readonly ArangoDBClient client;
    private readonly IGraphTransactionOpener transactionOpener;
    private readonly ILogger<ArangoRawStatementExecutor> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArangoRawStatementExecutor"/> class.
    /// </summary>
    /// <param name="client">The shared, cached <see cref="ArangoDBClient"/> bound to the target database.</param>
    /// <param name="transactionOpener">The <see cref="IGraphTransactionOpener"/> whose <see cref="IGraphTransactionOpener.Active"/> indicates the routing target.</param>
    /// <param name="logger">The typed logger this executor writes diagnostic events to. Pass <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}.Instance"/> to disable logging.</param>
    internal ArangoRawStatementExecutor(ArangoDBClient client, IGraphTransactionOpener transactionOpener, ILogger<ArangoRawStatementExecutor> logger)
    {
        this.client = client;
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
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to execute Arango statement: {Statement}", statement);
            throw new RawStatementException("Failed to execute the supplied statement against the ArangoDB endpoint.", exception);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ExecuteAsync(
        string statement,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken token = default) => StreamAsync(statement, parameters, token);

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

    private async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> StreamAsync(
        string aql,
        IReadOnlyDictionary<string, object?> parameters,
        [EnumeratorCancellation] CancellationToken token)
    {
        CursorHeaderProperties? headers = transactionOpener.Active is ArangoGraphTransaction ambient
            ? new CursorHeaderProperties { TransactionId = ambient.TransactionId }
            : null;
        string mode = headers is null ? "auto" : "ambient";

        PostCursorBody body = new()
        {
            Query = aql,
            BindVars = parameters.ToDictionary(pair => pair.Key, pair => pair.Value),
            BatchSize = BatchSize
        };

        CursorResponse<JObject> initial;
        using (Activity? activity = StartExecuteActivity(mode, parameters.Count))
        {
            try
            {
                logger.LogDebug("Executing Arango statement on {Mode} connection ({ParameterCount} parameters): {Statement}", mode, parameters.Count, aql);
                initial = await client.Cursor.PostCursorAsync<JObject>(body, headerProperties: headers, token: token).ConfigureAwait(false);
                RecordExecuteSuccess(activity, mode);
            }
            catch (Exception exception)
            {
                RecordExecuteFailure(activity, mode, exception);
                logger.LogWarning(exception, "Failed to execute Arango statement: {Statement}", aql);
                throw new RawStatementException("Failed to execute the supplied statement against the ArangoDB endpoint.", exception);
            }
        }

        foreach (JObject row in initial.Result)
            yield return ArangoJsonReader.ReadRow(row);

        string? cursorId = initial.Id;
        bool hasMore = initial.HasMore;

        while (hasMore && cursorId is not null)
        {
            // PutCursorAsync does not accept per-call headers in 3.1.0 — the cursor is already server-side
            // bound to the transaction that opened it via PostCursorAsync, so the transaction-id header
            // does not need to be repeated on continuation fetches.
            PutCursorResponse<JObject> next = await client.Cursor.PutCursorAsync<JObject>(cursorId, token).ConfigureAwait(false);
            foreach (JObject row in next.Result)
                yield return ArangoJsonReader.ReadRow(row);
            hasMore = next.HasMore;
            cursorId = next.Id;
        }
    }

    /// <summary>
    /// Starts an <c>Arango.ExecuteStatement</c> span tagged with the parameter count and routing mode. The span covers only the open-cursor phase — once the initial cursor returns, the span ends and continuation fetches proceed without an active span.
    /// </summary>
    private static Activity? StartExecuteActivity(string mode, int parameterCount)
    {
        Activity? activity = ArangoDiagnostics.Source.StartActivity("Arango.ExecuteStatement", ActivityKind.Client);
        activity?.SetTag("elementframework.parameter.count", parameterCount);
        activity?.SetTag("elementframework.transaction.mode", mode);
        activity?.SetTag("db.system", "arangodb");
        return activity;
    }

    /// <summary>
    /// Marks the supplied <paramref name="activity"/> as successful and ticks the statements counter with the success outcome.
    /// </summary>
    private static void RecordExecuteSuccess(Activity? activity, string mode)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
        ArangoDiagnostics.StatementsCounter.Add(1,
            new KeyValuePair<string, object?>("elementframework.transaction.mode", mode),
            new KeyValuePair<string, object?>("outcome", "success"));
    }

    /// <summary>
    /// Marks the supplied <paramref name="activity"/> as failed (status + exception.type tag) and ticks the statements counter with the failure outcome.
    /// </summary>
    private static void RecordExecuteFailure(Activity? activity, string mode, Exception exception)
    {
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.SetTag("exception.type", exception.GetType().FullName);
        ArangoDiagnostics.StatementsCounter.Add(1,
            new KeyValuePair<string, object?>("elementframework.transaction.mode", mode),
            new KeyValuePair<string, object?>("outcome", "failure"));
    }
}
