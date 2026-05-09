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

using System.Linq.Expressions;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Defines the fluent builder stage that has bound a node alias and may extend the pattern with relationships, predicates, or a return clause.
/// </summary>
/// <typeparam name="TNode">The CLR type of the bound node.</typeparam>
public interface IPatternNode<TNode> where TNode : class
{
    /// <summary>
    /// Extends the pattern with a typed edge connecting the currently bound node and the requested end node. The edge type's endpoints are validated against the model at runtime as an unordered pair, so either the model's natural orientation or its reverse is accepted; the direction is chosen on the returned stage.
    /// </summary>
    /// <typeparam name="TEdge">The CLR type of the edge. Must be a registered edge in the model.</typeparam>
    /// <typeparam name="TEnd">The CLR type of the end node. Must be a registered node in the model.</typeparam>
    /// <param name="alias">The alias used to reference the bound edge.</param>
    /// <returns>Returns an <see cref="IPatternRelationship{TStart, TEdge, TEnd}"/> stage.</returns>
    IPatternRelationship<TNode, TEdge, TEnd> RelatedBy<TEdge, TEnd>(string alias)
        where TEdge : class
        where TEnd : class;

    /// <summary>
    /// Filters the bound node by a property-level equality predicate. Only property-level predicates over the bound variable are supported; richer expressions must use the <see cref="IRawStatementExecutor"/> escape hatch.
    /// </summary>
    /// <param name="predicate">A property-level predicate over the bound node.</param>
    /// <returns>Returns the same node stage to allow further chaining.</returns>
    IPatternNode<TNode> Where(Expression<Func<TNode, bool>> predicate);

    /// <summary>
    /// Closes the pattern with a return of the specified alias and executes it.
    /// </summary>
    /// <typeparam name="TResult">The CLR type of the returned variable.</typeparam>
    /// <param name="alias">The alias of a previously bound variable to return.</param>
    /// <returns>Returns an enumerable of materialized results. Construction is eager; enumeration is lazy.</returns>
    /// <exception cref="TraversalTranslationException">Thrown when the traversal cannot be translated or initial execution fails.</exception>
    IEnumerable<TResult> Return<TResult>(string alias) where TResult : class;

    /// <summary>
    /// Asynchronously closes the pattern with a return of the specified alias and executes it.
    /// </summary>
    /// <typeparam name="TResult">The CLR type of the returned variable.</typeparam>
    /// <param name="alias">The alias of a previously bound variable to return.</param>
    /// <param name="token">The token that may be used to cancel the enumeration.</param>
    /// <returns>Returns an asynchronous enumerable of materialized results.</returns>
    /// <exception cref="TraversalTranslationException">Thrown when the traversal cannot be translated or initial execution fails.</exception>
    IAsyncEnumerable<TResult> ReturnAsync<TResult>(string alias, CancellationToken token = default) where TResult : class;
}
