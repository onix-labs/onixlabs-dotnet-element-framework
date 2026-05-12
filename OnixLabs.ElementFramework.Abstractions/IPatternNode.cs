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
    /// Orders the returned rows ascending by a property of the bound node. v1 supports a single ordering clause per traversal — calling <c>OrderBy</c> or <see cref="OrderByDescending{TKey}"/> twice on the same traversal throws.
    /// </summary>
    /// <typeparam name="TKey">The CLR type of the property the ordering is keyed on.</typeparam>
    /// <param name="selector">A single-property selector over the bound node (e.g. <c>a =&gt; a.Name</c>). Anything richer throws <see cref="NotSupportedException"/>.</param>
    /// <returns>Returns the same node stage to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selector"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an ordering has already been applied to the traversal.</exception>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="selector"/> is not a single-property access on the bound parameter.</exception>
    IPatternNode<TNode> OrderBy<TKey>(Expression<Func<TNode, TKey>> selector);

    /// <summary>
    /// Orders the returned rows descending by a property of the bound node. v1 supports a single ordering clause per traversal — calling <see cref="OrderBy{TKey}"/> or <c>OrderByDescending</c> twice on the same traversal throws.
    /// </summary>
    /// <typeparam name="TKey">The CLR type of the property the ordering is keyed on.</typeparam>
    /// <param name="selector">A single-property selector over the bound node. See <see cref="OrderBy{TKey}"/> for the supported shape.</param>
    /// <returns>Returns the same node stage to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selector"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an ordering has already been applied to the traversal.</exception>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="selector"/> is not a single-property access on the bound parameter.</exception>
    IPatternNode<TNode> OrderByDescending<TKey>(Expression<Func<TNode, TKey>> selector);

    /// <summary>
    /// Skips the first <paramref name="count"/> rows of the result set. v1 supports a single skip clause per traversal — calling <c>Skip</c> twice on the same traversal throws.
    /// </summary>
    /// <param name="count">The number of leading rows to skip. Must be non-negative.</param>
    /// <returns>Returns the same node stage to allow further chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a skip clause has already been applied to the traversal.</exception>
    IPatternNode<TNode> Skip(int count);

    /// <summary>
    /// Takes the first <paramref name="count"/> rows of the result set. v1 supports a single take clause per traversal — calling <c>Take</c> twice on the same traversal throws.
    /// </summary>
    /// <remarks>
    /// Named after LINQ's <see cref="System.Linq.Enumerable.Take{TSource}(IEnumerable{TSource}, int)"/> so the consumer surface reads as <c>Skip(n).Take(m)</c> in keeping with the rest of .NET. Each provider's emitter is responsible for translating <see cref="TraversalAst.Take"/> to its query-language equivalent (Cypher's <c>LIMIT</c>, SQL's <c>FETCH FIRST</c>, and so on).
    /// </remarks>
    /// <param name="count">The maximum number of rows the traversal returns. Must be non-negative.</param>
    /// <returns>Returns the same node stage to allow further chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a take clause has already been applied to the traversal.</exception>
    IPatternNode<TNode> Take(int count);

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
