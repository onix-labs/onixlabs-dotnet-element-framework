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

    private TraversalAst BuildAst(string returnAlias) =>
        new(state.Kind, [..state.Segments], [..state.Predicates], returnAlias);
}
