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
/// Represents the provider-agnostic entry point to the fluent traversal builder.
/// </summary>
/// <remarks>
/// Each <see cref="Match"/>, <see cref="Merge"/>, or <see cref="Create"/> call allocates a fresh <see cref="TraversalState"/> and a <see cref="PatternStart"/> stage.
/// </remarks>
/// <param name="model">The frozen <see cref="IGraphModel"/> the traversal is built against.</param>
/// <param name="translator">The provider's <see cref="ITraversalTranslator"/> that the eventual <c>Return</c> / <c>ReturnAsync</c> hands the AST off to.</param>
internal sealed class GraphTraversal(IGraphModel model, ITraversalTranslator translator) : IGraphTraversal
{
    /// <inheritdoc/>
    public IPatternStart Match() => new PatternStart(new TraversalState(TraversalKind.Match, model, translator));

    /// <inheritdoc/>
    public IPatternStart Merge() => new PatternStart(new TraversalState(TraversalKind.Merge, model, translator));

    /// <inheritdoc/>
    public IPatternStart Create() => new PatternStart(new TraversalState(TraversalKind.Create, model, translator));
}
