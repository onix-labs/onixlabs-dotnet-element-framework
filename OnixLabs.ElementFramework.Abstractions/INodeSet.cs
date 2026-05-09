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
/// Defines a typed accessor over nodes of a single registered type.
/// </summary>
/// <typeparam name="T">The node type. Must be a registered node in the model.</typeparam>
public interface INodeSet<T> where T : class
{
    /// <summary>
    /// Returns the set as a synchronously enumerable sequence over all nodes of the type in the graph.
    /// </summary>
    /// <returns>An enumerable over the nodes. Construction is eager; enumeration is lazy.</returns>
    /// <exception cref="GraphContextException">Thrown when the query cannot be constructed.</exception>
    IEnumerable<T> AsEnumerable();

    /// <summary>
    /// Returns the set as an asynchronously enumerable sequence over all nodes of the type in the graph.
    /// </summary>
    /// <param name="token">The token that may be used to cancel the enumeration.</param>
    /// <returns>An asynchronous enumerable over the nodes.</returns>
    /// <exception cref="GraphContextException">Thrown when the query cannot be constructed.</exception>
    IAsyncEnumerable<T> AsAsyncEnumerable(CancellationToken token = default);

    /// <summary>
    /// Determines whether a node with the specified identifier exists in the graph.
    /// </summary>
    /// <param name="id">The natural identifier configured for the node type via the model.</param>
    /// <returns><see langword="true"/> if a node with the specified identifier exists; otherwise <see langword="false"/>.</returns>
    /// <exception cref="GraphContextException">Thrown when the existence check fails.</exception>
    bool Exists(object id);

    /// <summary>
    /// Asynchronously determines whether a node with the specified identifier exists in the graph.
    /// </summary>
    /// <param name="id">The natural identifier configured for the node type via the model.</param>
    /// <param name="token">The token that may be used to cancel the operation.</param>
    /// <returns>A task that resolves to <see langword="true"/> if a node with the specified identifier exists; otherwise <see langword="false"/>.</returns>
    /// <exception cref="GraphContextException">Thrown when the existence check fails.</exception>
    Task<bool> ExistsAsync(object id, CancellationToken token = default);

    /// <summary>
    /// Finds a node in the graph by identifier.
    /// </summary>
    /// <param name="id">The natural identifier configured for the node type via the model.</param>
    /// <returns>The matching node, or <see langword="null"/> if no node with the specified identifier exists.</returns>
    /// <exception cref="GraphContextException">Thrown when the lookup fails.</exception>
    T? FindById(object id);

    /// <summary>
    /// Asynchronously finds a node in the graph by identifier.
    /// </summary>
    /// <param name="id">The natural identifier configured for the node type via the model.</param>
    /// <param name="token">The token that may be used to cancel the operation.</param>
    /// <returns>A task that resolves to the matching node, or <see langword="null"/> if no node with the specified identifier exists.</returns>
    /// <exception cref="GraphContextException">Thrown when the lookup fails.</exception>
    Task<T?> FindByIdAsync(object id, CancellationToken token = default);

    /// <summary>
    /// Stages a node for addition.
    /// </summary>
    /// <param name="node">The node to add.</param>
    /// <exception cref="GraphContextException">Thrown when the operation cannot be staged.</exception>
    void Add(T node);

    /// <summary>
    /// Asynchronously stages a node for addition.
    /// </summary>
    /// <param name="node">The node to add.</param>
    /// <param name="token">The token that may be used to cancel the operation.</param>
    /// <returns>A task that represents the staging operation.</returns>
    /// <exception cref="GraphContextException">Thrown when the operation cannot be staged.</exception>
    Task AddAsync(T node, CancellationToken token = default);

    /// <summary>
    /// Stages an existing node for update.
    /// </summary>
    /// <param name="node">The node to update.</param>
    /// <exception cref="GraphContextException">Thrown when the operation cannot be staged.</exception>
    void Update(T node);

    /// <summary>
    /// Asynchronously stages an existing node for update.
    /// </summary>
    /// <param name="node">The node to update.</param>
    /// <param name="token">The token that may be used to cancel the operation.</param>
    /// <returns>A task that represents the staging operation.</returns>
    /// <exception cref="GraphContextException">Thrown when the operation cannot be staged.</exception>
    Task UpdateAsync(T node, CancellationToken token = default);

    /// <summary>
    /// Stages a node for removal.
    /// </summary>
    /// <param name="node">The node to remove.</param>
    /// <exception cref="GraphContextException">Thrown when the operation cannot be staged.</exception>
    void Remove(T node);

    /// <summary>
    /// Asynchronously stages a node for removal.
    /// </summary>
    /// <param name="node">The node to remove.</param>
    /// <param name="token">The token that may be used to cancel the operation.</param>
    /// <returns>A task that represents the staging operation.</returns>
    /// <exception cref="GraphContextException">Thrown when the operation cannot be staged.</exception>
    Task RemoveAsync(T node, CancellationToken token = default);

    /// <summary>
    /// Stages a node for merge — adds the node if no matching node exists, or updates its properties if one does.
    /// </summary>
    /// <param name="node">The node to merge.</param>
    /// <exception cref="GraphContextException">Thrown when the operation cannot be staged.</exception>
    void Merge(T node);

    /// <summary>
    /// Asynchronously stages a node for merge — adds the node if no matching node exists, or updates its properties if one does.
    /// </summary>
    /// <param name="node">The node to merge.</param>
    /// <param name="token">The token that may be used to cancel the operation.</param>
    /// <returns>A task that represents the staging operation.</returns>
    /// <exception cref="GraphContextException">Thrown when the operation cannot be staged.</exception>
    Task MergeAsync(T node, CancellationToken token = default);
}
