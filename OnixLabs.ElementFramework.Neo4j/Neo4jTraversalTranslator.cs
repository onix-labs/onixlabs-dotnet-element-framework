// All Rights Reserved License
//
// 1. Grant of License
// Subject to the terms and conditions of this License, ONIXLabs ("Licensor") hereby grants to you a limited, non-exclusive, non-transferable, non-sublicensable license to use the Software for commercial, private, and paid purposes. This license does not include any rights to modify, distribute, or create derivative works of the Software.
//
// 2. Permitted Uses
// You are permitted to:
//  - Use the Software for commercial purposes.
//  - Use the Software for private purposes.
//  - Use the Software for paid purposes.
//  - Exercise any patent rights associated with the Software, solely in connection with your use of the Software as permitted under this License.
//
// 3. Restrictions
// You are not permitted to:
//  - Modify, alter, or create any derivative works of the Software.
//  - Distribute, sublicense, lease, rent, or otherwise transfer the Software to any third party.
//  - Use the Software without obtaining a proper license for paid use.
//  - Use the Software in any way that infringes upon the trademarks, service marks, or trade names of the Licensor.
//  - Use the Software in any manner that could cause it to be considered open-source software or otherwise subject to an open-source license.
//
// 4. No Free Use
// This license does not permit any free use of the Software. Any use of the Software without a paid license is strictly prohibited.
//
// 5. No Liability
// To the maximum extent permitted by applicable law, the Software is provided "as is" and "as available" without warranty of any kind, express or implied, including but not limited to the implied warranties of merchantability, fitness for a particular purpose, and non-infringement. In no event shall the Licensor be liable for any damages whatsoever arising out of the use of or inability to use the Software, even if the Licensor has been advised of the possibility of such damages.
//
// 6. No Warranty
// The Licensor makes no warranty that the Software will meet your requirements, be uninterrupted, secure, or error-free. The Licensor disclaims all warranties with respect to the Software, whether express or implied, including but not limited to any warranties of merchantability, fitness for a particular purpose, and non-infringement.
//
// 7. Termination
// This license is effective until terminated. Your rights under this license will terminate automatically without notice if you fail to comply with any term of this license. Upon termination, you must immediately cease all use of the Software and destroy all copies of the Software in your possession or control.
//
// 8. Governing Law
// This license will be governed by and construed in accordance with the laws of [Your Jurisdiction], without regard to its conflict of laws principles.
//
// 9. Entire Agreement
// This license constitutes the entire agreement between you and the Licensor concerning the Software and supersedes all prior or contemporaneous communications, agreements, or understandings, whether oral or written, concerning the subject matter hereof.
//
// By using the Software, you acknowledge that you have read and understood this license and agree to be bound by its terms and conditions.

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
