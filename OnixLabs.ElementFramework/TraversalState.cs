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
/// Represents the mutable per-traversal scratchpad threaded through the fluent pattern builder stages.
/// </summary>
/// <remarks>
/// Accumulates the segments and predicates that <see cref="IPatternNode{TNode}.Return{TResult}"/> and <see cref="IPatternNode{TNode}.ReturnAsync{TResult}"/> snapshot into an immutable <see cref="TraversalAst"/>.
/// </remarks>
/// <param name="kind">The clause kind that begins the traversal.</param>
/// <param name="model">The frozen <see cref="IGraphModel"/> the traversal is built against.</param>
/// <param name="translator">The provider's <see cref="ITraversalTranslator"/> that the eventual <c>Return</c> / <c>ReturnAsync</c> hands the AST off to.</param>
internal sealed class TraversalState(TraversalKind kind, IGraphModel model, ITraversalTranslator translator)
{
    /// <summary>
    /// Gets the clause kind that begins the traversal.
    /// </summary>
    /// <value>The <see cref="TraversalKind"/> selected by the entry method.</value>
    public TraversalKind Kind { get; } = kind;

    /// <summary>
    /// Gets the frozen graph model the traversal is built against.
    /// </summary>
    /// <value>The frozen <see cref="IGraphModel"/>.</value>
    public IGraphModel Model { get; } = model;

    /// <summary>
    /// Gets the provider's translator that <c>Return</c> / <c>ReturnAsync</c> hands the AST off to.
    /// </summary>
    /// <value>The provider's <see cref="ITraversalTranslator"/>.</value>
    public ITraversalTranslator Translator { get; } = translator;

    /// <summary>
    /// Gets the accumulating list of pattern segments. Begins with a node and alternates with relationship segments thereafter.
    /// </summary>
    /// <value>The mutable list of accumulated <see cref="PatternSegment"/> values.</value>
    public List<PatternSegment> Segments { get; } = [];

    /// <summary>
    /// Gets the accumulating list of property-equality predicates.
    /// </summary>
    /// <value>The mutable list of accumulated <see cref="TraversalPredicate"/> values.</value>
    public List<TraversalPredicate> Predicates { get; } = [];

    /// <summary>
    /// Gets the accumulating list of ordering clauses. v1 admits at most one entry; <see cref="PatternNode{TNode}"/> enforces this by throwing on the second <c>OrderBy</c> / <c>OrderByDescending</c> call.
    /// </summary>
    /// <value>The mutable list of accumulated <see cref="TraversalOrdering"/> values.</value>
    public List<TraversalOrdering> Orderings { get; } = [];

    /// <summary>
    /// Gets or sets the number of leading rows the traversal skips, or <see langword="null"/> when no <c>Skip</c> was applied.
    /// </summary>
    /// <value>A non-negative row count, or <see langword="null"/>.</value>
    public int? Skip { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of rows the traversal returns, or <see langword="null"/> when no <c>Take</c> was applied.
    /// </summary>
    /// <value>A non-negative row count, or <see langword="null"/>.</value>
    public int? Take { get; set; }
}
