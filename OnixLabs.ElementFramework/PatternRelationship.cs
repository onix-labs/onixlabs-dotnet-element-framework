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
/// Represents the fluent builder stage that has bound a relationship alias but has not yet pinned its direction.
/// </summary>
/// <typeparam name="TStart">The CLR type of the start node.</typeparam>
/// <typeparam name="TEdge">The CLR type of the bound edge.</typeparam>
/// <typeparam name="TEnd">The CLR type of the end node.</typeparam>
/// <param name="state">The mutable <see cref="TraversalState"/> shared across all stages of this traversal.</param>
/// <param name="relationshipAlias">The alias the bound relationship will carry once direction is pinned.</param>
internal sealed class PatternRelationship<TStart, TEdge, TEnd>(TraversalState state, string relationshipAlias)
    : IPatternRelationship<TStart, TEdge, TEnd>
    where TStart : class
    where TEdge : class
    where TEnd : class
{
    /// <inheritdoc/>
    public IPatternRelationshipDirected<TStart, TEdge, TEnd> Outgoing() =>
        new PatternRelationshipDirected<TStart, TEdge, TEnd>(state, relationshipAlias, RelationshipDirection.Outgoing);

    /// <inheritdoc/>
    public IPatternRelationshipDirected<TStart, TEdge, TEnd> Incoming() =>
        new PatternRelationshipDirected<TStart, TEdge, TEnd>(state, relationshipAlias, RelationshipDirection.Incoming);

    /// <inheritdoc/>
    public IPatternRelationshipDirected<TStart, TEdge, TEnd> Either() =>
        new PatternRelationshipDirected<TStart, TEdge, TEnd>(state, relationshipAlias, RelationshipDirection.Either);
}
