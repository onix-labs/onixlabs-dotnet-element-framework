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

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the fluent builder stage that has pinned a relationship's direction and is ready to bind the end node.
/// </summary>
/// <typeparam name="TStart">The CLR type of the start node.</typeparam>
/// <typeparam name="TEdge">The CLR type of the bound edge.</typeparam>
/// <typeparam name="TEnd">The CLR type of the end node.</typeparam>
/// <param name="state">The mutable <see cref="TraversalState"/> shared across all stages of this traversal.</param>
/// <param name="relationshipAlias">The alias the bound relationship will carry.</param>
/// <param name="direction">The pinned <see cref="RelationshipDirection"/> from the preceding node segment to the following node segment.</param>
internal sealed class PatternRelationshipDirected<TStart, TEdge, TEnd>(
    TraversalState state,
    string relationshipAlias,
    RelationshipDirection direction)
    : IPatternRelationshipDirected<TStart, TEdge, TEnd>
    where TStart : class
    where TEdge : class
    where TEnd : class
{
    /// <inheritdoc/>
    public IPatternNode<TEnd> To(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        state.Segments.Add(new RelationshipPatternSegment(typeof(TEdge), relationshipAlias, direction));
        state.Segments.Add(new NodePatternSegment(typeof(TEnd), alias));
        return new PatternNode<TEnd>(state, alias);
    }
}
