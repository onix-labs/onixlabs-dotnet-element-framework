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
internal sealed class Neo4jTraversalTranslator(IStatementEmitter emitter, IRawStatementExecutor executor, IResultMaterializer materializer) : ITraversalTranslator
{
    /// <inheritdoc/>
    public IEnumerable<TResult> Translate<TResult>(IGraphModel model, TraversalAst ast)
    {
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
            throw new TraversalTranslationException("Failed to translate or execute the supplied fluent traversal against the Neo4j endpoint.", exception);
        }

        return results;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<TResult> TranslateAsync<TResult>(IGraphModel model, TraversalAst ast, CancellationToken token = default) =>
        TranslateAsyncCore<TResult>(model, ast, token);

    private async IAsyncEnumerable<TResult> TranslateAsyncCore<TResult>(IGraphModel model, TraversalAst ast, [EnumeratorCancellation] CancellationToken token)
    {
        IAsyncEnumerable<IReadOnlyDictionary<string, object?>> rows;
        ReturnKind kind;

        try
        {
            DataStatement statement = emitter.EmitTraversal(model, ast);
            rows = executor.ExecuteAsync(statement.Statement, statement.Parameters, token);
            kind = ResolveReturnKind(ast);
        }
        catch (Exception exception)
        {
            throw new TraversalTranslationException("Failed to translate or execute the supplied fluent traversal against the Neo4j endpoint.", exception);
        }

        await foreach (IReadOnlyDictionary<string, object?> row in rows.WithCancellation(token).ConfigureAwait(false))
            yield return Materialize<TResult>(model, row, ast.ReturnAlias, kind);
    }

    private TResult Materialize<TResult>(IGraphModel model, IReadOnlyDictionary<string, object?> row, string alias, ReturnKind kind) => kind switch
    {
        ReturnKind.Node => materializer.MaterializeNode<TResult>(model, row, alias),
        ReturnKind.Edge => materializer.MaterializeEdge<TResult>(model, row, alias),
        _ => throw new TraversalTranslationException($"Unknown return kind '{kind}'.")
    };

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

    private enum ReturnKind
    {
        Node,
        Edge
    }
}
