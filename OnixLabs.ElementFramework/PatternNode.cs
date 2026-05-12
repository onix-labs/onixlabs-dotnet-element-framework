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
using System.Reflection;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the fluent builder stage that has bound a node alias and may extend the pattern with relationships, predicates, or a final return.
/// </summary>
/// <typeparam name="TNode">The CLR type of the bound node.</typeparam>
/// <param name="state">The mutable <see cref="TraversalState"/> shared across all stages of this traversal.</param>
/// <param name="alias">The alias of the most recently bound node, used to scope <c>Where</c> predicates.</param>
internal sealed class PatternNode<TNode>(TraversalState state, string alias) : IPatternNode<TNode> where TNode : class
{
    /// <inheritdoc/>
    public IPatternRelationship<TNode, TEdge, TEnd> RelatedBy<TEdge, TEnd>(string alias)
        where TEdge : class
        where TEnd : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        IRelationshipMetadata relationship = state.Model.GetRelationship(typeof(TEdge));
        bool natural = relationship.StartType == typeof(TNode) && relationship.EndType == typeof(TEnd);
        bool reversed = relationship.StartType == typeof(TEnd) && relationship.EndType == typeof(TNode);
        if (!natural && !reversed)
            throw new ModelConfigurationException(
                $"Relationship {typeof(TEdge).FullName} is not registered between {typeof(TNode).FullName} and {typeof(TEnd).FullName}; " +
                $"the model declares it between {relationship.StartType.FullName} and {relationship.EndType.FullName}.");
        return new PatternRelationship<TNode, TEdge, TEnd>(state, alias);
    }

    /// <inheritdoc/>
    public IPatternNode<TNode> Where(Expression<Func<TNode, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        state.Predicates.Add(PredicateTranslator.Translate(alias, predicate));
        return this;
    }

    /// <inheritdoc/>
    public IPatternNode<TNode> OrderBy<TKey>(Expression<Func<TNode, TKey>> selector) =>
        AccumulateOrdering(selector, OrderDirection.Ascending);

    /// <inheritdoc/>
    public IPatternNode<TNode> OrderByDescending<TKey>(Expression<Func<TNode, TKey>> selector) =>
        AccumulateOrdering(selector, OrderDirection.Descending);

    /// <inheritdoc/>
    public IPatternNode<TNode> Skip(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (state.Skip is not null)
            throw new InvalidOperationException("Skip has already been applied to this traversal. v1 supports a single Skip clause per traversal.");
        state.Skip = count;
        return this;
    }

    /// <inheritdoc/>
    public IPatternNode<TNode> Take(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (state.Take is not null)
            throw new InvalidOperationException("Take has already been applied to this traversal. v1 supports a single Take clause per traversal.");
        state.Take = count;
        return this;
    }

    /// <inheritdoc/>
    public IEnumerable<TResult> Return<TResult>(string alias) where TResult : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        TraversalAst ast = BuildAst(alias);
        return state.Translator.Translate<TResult>(state.Model, ast);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<TResult> ReturnAsync<TResult>(string alias, CancellationToken token = default) where TResult : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        TraversalAst ast = BuildAst(alias);
        return state.Translator.TranslateAsync<TResult>(state.Model, ast, token);
    }

    /// <summary>
    /// Snapshots the accumulated traversal state into an immutable <see cref="TraversalAst"/> bound to the supplied return alias.
    /// </summary>
    /// <param name="returnAlias">The alias to project in the resulting AST.</param>
    /// <returns>Returns an immutable <see cref="TraversalAst"/> representing the accumulated traversal.</returns>
    private TraversalAst BuildAst(string returnAlias) =>
        new(state.Kind, [..state.Segments], [..state.Predicates], returnAlias)
        {
            Orderings = [..state.Orderings],
            Skip = state.Skip,
            Take = state.Take
        };

    /// <summary>
    /// Accumulates a single-property ordering clause scoped to the bound alias. Enforces v1's one-ordering-per-traversal invariant and the single-property-access selector shape.
    /// </summary>
    /// <typeparam name="TKey">The CLR type of the property the ordering is keyed on.</typeparam>
    /// <param name="selector">The selector lambda; must be a single property access on the bound parameter.</param>
    /// <param name="direction">The ordering direction.</param>
    /// <returns>Returns the same node stage to allow further chaining.</returns>
    private IPatternNode<TNode> AccumulateOrdering<TKey>(Expression<Func<TNode, TKey>> selector, OrderDirection direction)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (state.Orderings.Count > 0)
            throw new InvalidOperationException("An ordering has already been applied to this traversal. v1 supports a single OrderBy / OrderByDescending clause per traversal.");
        PropertyInfo property = ExtractPropertyAccess(selector);
        state.Orderings.Add(new TraversalOrdering(alias, property.Name, direction));
        return this;
    }

    /// <summary>
    /// Extracts the single property accessed by an OrderBy / OrderByDescending selector. Unwraps a trailing <see cref="ExpressionType.Convert"/> wrapper that the compiler inserts when the property type does not match the lambda's return type (e.g. boxing a value-typed property to <see cref="object"/>).
    /// </summary>
    /// <typeparam name="TKey">The CLR type of the property the selector returns.</typeparam>
    /// <param name="selector">The selector to inspect.</param>
    /// <returns>Returns the <see cref="PropertyInfo"/> the selector accesses.</returns>
    /// <exception cref="NotSupportedException">Thrown when the selector body is not a single property access on the lambda's parameter.</exception>
    private static PropertyInfo ExtractPropertyAccess<TKey>(Expression<Func<TNode, TKey>> selector)
    {
        Expression body = selector.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert
            ? convert.Operand
            : selector.Body;

        if (body is MemberExpression { Member: PropertyInfo property, Expression: ParameterExpression candidate }
            && candidate == selector.Parameters[0])
        {
            return property;
        }

        throw new NotSupportedException(
            "OrderBy / OrderByDescending selectors must be a single property access on the bound parameter, e.g. 'a => a.Name'. " +
            "Compound expressions, method calls, and non-property members are not supported in v1.");
    }
}
