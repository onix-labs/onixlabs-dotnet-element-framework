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

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the Neo4j implementation of <see cref="ITraversalTranslator"/> that translates a <see cref="TraversalAst"/> into Cypher, executes it, and materializes the result rows.
/// </summary>
/// <remarks>
/// Translation forwards to <see cref="IStatementEmitter.EmitTraversal"/>; execution forwards to <see cref="IRawStatementExecutor"/>; materialization forwards to <see cref="IResultMaterializer"/>. Whether each row is materialized as a node or as an edge is determined by the segment kind that the AST's return alias resolves to. The sync surface materializes eagerly; the async surface is an async iterator that streams rows as the executor yields them.
/// </remarks>
/// <param name="emitter">The Cypher emitter that translates the AST into a <see cref="DataStatement"/>.</param>
/// <param name="executor">The Cypher executor that runs the emitted statement against the active transaction.</param>
/// <param name="materializer">The result materializer that projects rows into the requested CLR type.</param>
internal sealed class Neo4jTraversalTranslator(
    IStatementEmitter emitter,
    IRawStatementExecutor executor,
    IResultMaterializer materializer) : ITraversalTranslator
{
    /// <inheritdoc/>
    public IEnumerable<TResult> Translate<TResult>(IGraphModel model, TraversalAst ast)
    {
        using Activity? activity = StartTranslateActivity(ast);
        IEnumerable<IReadOnlyDictionary<string, object?>> rows;
        ReturnKind kind;

        try
        {
            DataStatement statement = emitter.EmitTraversal(model, ast);
            rows = executor.Execute(statement.Statement, statement.Parameters);
            kind = ResolveReturnKind(ast);
        }
        catch (Exception exception)
        {
            RecordTranslateFailure(activity, exception);
            throw new TraversalTranslationException("Failed to translate or execute the supplied fluent traversal against the Neo4j endpoint.", exception);
        }

        List<TResult> results = [];

        try
        {
            foreach (IReadOnlyDictionary<string, object?> row in rows)
                results.Add(Materialize<TResult>(model, row, ast.ReturnAlias, kind));
        }
        catch (Exception exception)
        {
            RecordTranslateFailure(activity, exception);
            throw new TraversalTranslationException("Failed to translate or execute the supplied fluent traversal against the Neo4j endpoint.", exception);
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        return results;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<TResult> TranslateAsync<TResult>(IGraphModel model, TraversalAst ast, CancellationToken token = default) =>
        TranslateAsyncCore<TResult>(model, ast, token);

    /// <summary>
    /// Translates and executes <paramref name="ast"/>, yielding each materialized row as a <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">The CLR type to materialize each row into.</typeparam>
    /// <param name="model">The graph model that scopes the traversal.</param>
    /// <param name="ast">The traversal AST to translate and execute.</param>
    /// <param name="token">A <see cref="CancellationToken"/> that cancels the enumeration.</param>
    /// <returns>Returns an asynchronous enumeration of materialized results.</returns>
    private async IAsyncEnumerable<TResult> TranslateAsyncCore<TResult>(
        IGraphModel model,
        TraversalAst ast,
        [EnumeratorCancellation] CancellationToken token)
    {
        Activity? activity = StartTranslateActivity(ast);
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows;
        ReturnKind kind;

        try
        {
            DataStatement statement = emitter.EmitTraversal(model, ast);
            rows = executor.ExecuteAsync(statement.Statement, statement.Parameters, token);
            kind = ResolveReturnKind(ast);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception exception)
        {
            RecordTranslateFailure(activity, exception);
            activity?.Dispose();
            throw new TraversalTranslationException("Failed to translate or execute the supplied fluent traversal against the Neo4j endpoint.", exception);
        }

        // The span ends once translation + execute have completed; downstream enumeration of `rows`
        // is covered by the executor's own ExecuteStatement span (and the Neo4j driver's wire-layer
        // ActivitySource). Disposing here keeps the translate span tight to the actual translation work.
        activity?.Dispose();

        await foreach (IReadOnlyDictionary<string, object?> row in rows.WithCancellation(token).ConfigureAwait(false))
            yield return Materialize<TResult>(model, row, ast.ReturnAlias, kind);
    }

    /// <summary>
    /// Starts a <c>Neo4j.TranslateTraversal</c> span tagged with the traversal kind, segment count, and return alias.
    /// </summary>
    private static Activity? StartTranslateActivity(TraversalAst ast)
    {
        Activity? activity = Neo4jDiagnostics.Source.StartActivity("Neo4j.TranslateTraversal", ActivityKind.Internal);
        activity?.SetTag("elementframework.traversal.kind", ast.Kind.ToString());
        activity?.SetTag("elementframework.traversal.segment_count", ast.Segments.Count);
        activity?.SetTag("elementframework.traversal.predicate_count", ast.Predicates.Count);
        activity?.SetTag("elementframework.traversal.return_alias", ast.ReturnAlias);
        activity?.SetTag("db.system", "neo4j");
        return activity;
    }

    /// <summary>
    /// Marks the supplied <paramref name="activity"/> as failed with status + exception type.
    /// </summary>
    private static void RecordTranslateFailure(Activity? activity, Exception exception)
    {
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.SetTag("exception.type", exception.GetType().FullName);
    }

    /// <summary>
    /// Materializes a single result row as a node or edge based on <paramref name="kind"/>.
    /// </summary>
    /// <typeparam name="TResult">The CLR type to materialize into.</typeparam>
    /// <param name="model">The graph model that scopes the traversal.</param>
    /// <param name="row">The result row to materialize.</param>
    /// <param name="alias">The return alias whose value is materialized from <paramref name="row"/>.</param>
    /// <param name="kind">Indicates whether the alias resolves to a node or an edge.</param>
    /// <returns>Returns the materialized <typeparamref name="TResult"/> instance.</returns>
    /// <exception cref="TraversalTranslationException">Thrown when <paramref name="kind"/> is not a recognized return kind.</exception>
    private TResult Materialize<TResult>(
        IGraphModel model,
        IReadOnlyDictionary<string, object?> row,
        string alias,
        ReturnKind kind) => kind switch
    {
        ReturnKind.Node => materializer.MaterializeNodeAt<TResult>(model, row, alias),
        ReturnKind.Edge => materializer.MaterializeEdgeAt<TResult>(model, row, alias),
        _ => throw new TraversalTranslationException($"Unknown return kind '{kind}'.")
    };

    /// <summary>
    /// Resolves whether the AST's return alias refers to a node or an edge segment.
    /// </summary>
    /// <param name="ast">The traversal AST whose return alias is being resolved.</param>
    /// <returns>Returns <see cref="ReturnKind.Node"/> when the return alias refers to a node segment, or <see cref="ReturnKind.Edge"/> when it refers to a relationship segment.</returns>
    /// <exception cref="TraversalTranslationException">Thrown when the return alias does not match a bound segment, or the segment kind is not recognized.</exception>
    private static ReturnKind ResolveReturnKind(TraversalAst ast)
    {
        PatternSegment segment = ast.Segments.FirstOrDefault(s => s.Alias == ast.ReturnAlias) ?? throw new TraversalTranslationException(
            $"Return alias '{ast.ReturnAlias}' does not match any bound segment in the traversal pattern.");

        return segment switch
        {
            NodePatternSegment => ReturnKind.Node,
            RelationshipPatternSegment => ReturnKind.Edge,
            _ => throw new TraversalTranslationException($"Unknown pattern segment type '{segment.GetType().FullName}'.")
        };
    }

    /// <summary>
    /// Specifies whether a traversal's return alias resolves to a node or an edge segment.
    /// </summary>
    private enum ReturnKind
    {
        /// <summary>
        /// The return alias resolves to a node segment.
        /// </summary>
        Node,

        /// <summary>
        /// The return alias resolves to a relationship segment.
        /// </summary>
        Edge
    }
}
