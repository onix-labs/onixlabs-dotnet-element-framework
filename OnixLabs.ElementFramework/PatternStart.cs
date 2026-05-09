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
/// Represents the fluent builder stage that has begun a clause but not yet bound any node alias.
/// </summary>
/// <param name="state">The mutable <see cref="TraversalState"/> shared across all stages of this traversal.</param>
internal sealed class PatternStart(TraversalState state) : IPatternStart
{
    /// <inheritdoc/>
    public IPatternNode<TNode> Node<TNode>(string alias) where TNode : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        state.Model.GetNode(typeof(TNode));
        state.Segments.Add(new NodePatternSegment(typeof(TNode), alias));
        return new PatternNode<TNode>(state, alias);
    }
}
