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
/// Defines the change tracker that holds the identity map and pending operations for a single <see cref="GraphContext"/>.
/// </summary>
internal interface IChangeTracker
{
    /// <summary>
    /// Flushes all pending tracked operations to the underlying store.
    /// </summary>
    /// <returns>The number of changes that were written.</returns>
    /// <exception cref="GraphContextException">Thrown when the flush fails.</exception>
    int Flush();

    /// <summary>
    /// Asynchronously flushes all pending tracked operations to the underlying store.
    /// </summary>
    /// <param name="token">The token that may be used to cancel the operation.</param>
    /// <returns>A task that resolves to the number of changes that were written.</returns>
    /// <exception cref="GraphContextException">Thrown when the flush fails.</exception>
    Task<int> FlushAsync(CancellationToken token = default);

    /// <summary>
    /// Stages a node for addition.
    /// </summary>
    /// <typeparam name="T">The node type. Must be a registered node in the model.</typeparam>
    /// <param name="node">The node to add.</param>
    /// <exception cref="ModelConfigurationException">Thrown when <typeparamref name="T"/> has no key configured in the model.</exception>
    /// <exception cref="GraphContextException">Thrown when the node's configured key property holds a <see langword="null"/> value.</exception>
    void TrackAdd<T>(T node) where T : class;

    /// <summary>
    /// Stages an existing node for update.
    /// </summary>
    /// <typeparam name="T">The node type. Must be a registered node in the model.</typeparam>
    /// <param name="node">The node to update.</param>
    /// <exception cref="ModelConfigurationException">Thrown when <typeparamref name="T"/> has no key configured in the model.</exception>
    /// <exception cref="GraphContextException">Thrown when the node's configured key property holds a <see langword="null"/> value.</exception>
    void TrackUpdate<T>(T node) where T : class;

    /// <summary>
    /// Stages a node for removal.
    /// </summary>
    /// <typeparam name="T">The node type. Must be a registered node in the model.</typeparam>
    /// <param name="node">The node to remove.</param>
    /// <exception cref="ModelConfigurationException">Thrown when <typeparamref name="T"/> has no key configured in the model.</exception>
    /// <exception cref="GraphContextException">Thrown when the node's configured key property holds a <see langword="null"/> value.</exception>
    void TrackRemove<T>(T node) where T : class;

    /// <summary>
    /// Stages a node for merge: adds the node if no matching node exists, or updates its properties if one does.
    /// </summary>
    /// <typeparam name="T">The node type. Must be a registered node in the model.</typeparam>
    /// <param name="node">The node to merge.</param>
    /// <exception cref="ModelConfigurationException">Thrown when <typeparamref name="T"/> has no key configured in the model.</exception>
    /// <exception cref="GraphContextException">Thrown when the node's configured key property holds a <see langword="null"/> value.</exception>
    void TrackMerge<T>(T node) where T : class;

    /// <summary>
    /// Stages an edge for connection between two endpoint nodes.
    /// </summary>
    /// <typeparam name="TStart">The start node type.</typeparam>
    /// <typeparam name="TEdge">The edge type. Must be a registered edge in the model.</typeparam>
    /// <typeparam name="TEnd">The end node type.</typeparam>
    /// <param name="start">The start node.</param>
    /// <param name="edge">The edge instance carrying the connection's payload.</param>
    /// <param name="end">The end node.</param>
    /// <exception cref="ModelConfigurationException">Thrown when <typeparamref name="TEdge"/> is not registered or its endpoints conflict with the supplied types.</exception>
    void TrackConnect<TStart, TEdge, TEnd>(TStart start, TEdge edge, TEnd end)
        where TStart : class
        where TEdge : class
        where TEnd : class;

    /// <summary>
    /// Stages all edges of the specified type between the two endpoint nodes for removal.
    /// </summary>
    /// <typeparam name="TStart">The start node type.</typeparam>
    /// <typeparam name="TEdge">The edge type. Must be a registered edge in the model.</typeparam>
    /// <typeparam name="TEnd">The end node type.</typeparam>
    /// <param name="start">The start node.</param>
    /// <param name="end">The end node.</param>
    /// <exception cref="ModelConfigurationException">Thrown when <typeparamref name="TEdge"/> is not registered or its endpoints conflict with the supplied types.</exception>
    void TrackDisconnect<TStart, TEdge, TEnd>(TStart start, TEnd end)
        where TStart : class
        where TEdge : class
        where TEnd : class;

    /// <summary>
    /// Looks up a node in the identity map by its configured key.
    /// </summary>
    /// <typeparam name="T">The node type. Must be a registered node in the model.</typeparam>
    /// <param name="key">The configured key value of the node.</param>
    /// <returns>The tracked node with the supplied key, or <see langword="null"/> if no such node is tracked.</returns>
    T? Find<T>(object key) where T : class;

    /// <summary>
    /// Registers an existing node instance in the identity map without staging any pending operation.
    /// </summary>
    /// <typeparam name="T">The node type. Must be a registered node in the model.</typeparam>
    /// <param name="node">The node to attach.</param>
    /// <exception cref="ModelConfigurationException">Thrown when <typeparamref name="T"/> has no key configured in the model.</exception>
    /// <exception cref="GraphContextException">Thrown when the node's configured key property holds a <see langword="null"/> value, or when a different instance with the same key is already tracked.</exception>
    void Attach<T>(T node) where T : class;

    /// <summary>
    /// Clears the identity map and any pending tracked operations.
    /// </summary>
    void Reset();
}
