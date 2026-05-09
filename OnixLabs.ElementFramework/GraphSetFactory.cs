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

using System.Collections.Concurrent;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the per-context cache that produces and caches typed-set instances for a <see cref="GraphContext"/>.
/// </summary>
/// <remarks>
/// The first <see cref="GetNodesOfType{T}"/> or <see cref="GetEdgesOfType{T}"/> call for a given CLR type instantiates the corresponding set, threading the per-context tracker plus the per-options emitter, executor, and materializer into its constructor; subsequent calls return the cached instance.
/// </remarks>
/// <param name="model">The frozen <see cref="IGraphModel"/>.</param>
/// <param name="tracker">The owning context's <see cref="IChangeTracker"/>.</param>
/// <param name="emitter">The provider's <see cref="IStatementEmitter"/>.</param>
/// <param name="executor">The provider's <see cref="IRawStatementExecutor"/>.</param>
/// <param name="materializer">The provider's <see cref="IResultMaterializer"/>.</param>
internal sealed class GraphSetFactory(
    IGraphModel model,
    IChangeTracker tracker,
    IStatementEmitter emitter,
    IRawStatementExecutor executor,
    IResultMaterializer materializer
) : IGraphSetFactory
{
    private readonly ConcurrentDictionary<Type, object> nodeSets = new();
    private readonly ConcurrentDictionary<Type, object> edgeSets = new();

    /// <inheritdoc/>
    public INodeSet<T> GetNodesOfType<T>() where T : class =>
        (INodeSet<T>)nodeSets.GetOrAdd(typeof(T), static (_, state) =>
                new NodeSet<T>(state.model, state.tracker, state.emitter, state.executor, state.materializer),
            (model, tracker, emitter, executor, materializer));

    /// <inheritdoc/>
    public IEdgeSet<T> GetEdgesOfType<T>() where T : class =>
        (IEdgeSet<T>)edgeSets.GetOrAdd(typeof(T), static (_, state) =>
                new EdgeSet<T>(state.model, state.tracker, state.emitter, state.executor, state.materializer),
            (model, tracker, emitter, executor, materializer));
}
