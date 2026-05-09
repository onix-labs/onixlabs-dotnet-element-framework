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
/// Defines a typed accessor over edges of a single registered type.
/// </summary>
/// <typeparam name="T">The edge type. Must be a registered edge in the model.</typeparam>
public interface IEdgeSet<T> where T : class
{
    /// <summary>
    /// Returns the set as a synchronously enumerable sequence over all edges of the type in the graph.
    /// </summary>
    /// <returns>An enumerable over the edges. Construction is eager; enumeration is lazy.</returns>
    /// <exception cref="GraphContextException">Thrown when the query cannot be constructed.</exception>
    IEnumerable<T> AsEnumerable();

    /// <summary>
    /// Returns the set as an asynchronously enumerable sequence over all edges of the type in the graph.
    /// </summary>
    /// <param name="token">The token that may be used to cancel the enumeration.</param>
    /// <returns>An asynchronous enumerable over the edges.</returns>
    /// <exception cref="GraphContextException">Thrown when the query cannot be constructed.</exception>
    IAsyncEnumerable<T> AsAsyncEnumerable(CancellationToken token = default);

    /// <summary>
    /// Stages a new edge instance of this type for addition between the supplied endpoints.
    /// </summary>
    /// <typeparam name="TStart">The CLR type of the start node.</typeparam>
    /// <typeparam name="TEnd">The CLR type of the end node.</typeparam>
    /// <param name="start">The start node.</param>
    /// <param name="edge">The edge instance carrying the relationship's properties.</param>
    /// <param name="end">The end node.</param>
    /// <exception cref="GraphContextException">Thrown when the operation cannot be staged.</exception>
    void Connect<TStart, TEnd>(TStart start, T edge, TEnd end)
        where TStart : class
        where TEnd : class;

    /// <summary>
    /// Asynchronously stages a new edge instance of this type for addition between the supplied endpoints.
    /// </summary>
    /// <typeparam name="TStart">The CLR type of the start node.</typeparam>
    /// <typeparam name="TEnd">The CLR type of the end node.</typeparam>
    /// <param name="start">The start node.</param>
    /// <param name="edge">The edge instance carrying the relationship's properties.</param>
    /// <param name="end">The end node.</param>
    /// <param name="token">The token that may be used to cancel the operation.</param>
    /// <returns>A task that represents the staging operation.</returns>
    /// <exception cref="GraphContextException">Thrown when the operation cannot be staged.</exception>
    Task ConnectAsync<TStart, TEnd>(TStart start, T edge, TEnd end, CancellationToken token = default)
        where TStart : class
        where TEnd : class;

    /// <summary>
    /// Stages all edges of this type connecting the supplied endpoints for removal.
    /// </summary>
    /// <typeparam name="TStart">The CLR type of the start node.</typeparam>
    /// <typeparam name="TEnd">The CLR type of the end node.</typeparam>
    /// <param name="start">The start node.</param>
    /// <param name="end">The end node.</param>
    /// <exception cref="GraphContextException">Thrown when the operation cannot be staged.</exception>
    void Disconnect<TStart, TEnd>(TStart start, TEnd end)
        where TStart : class
        where TEnd : class;

    /// <summary>
    /// Asynchronously stages all edges of this type connecting the supplied endpoints for removal.
    /// </summary>
    /// <typeparam name="TStart">The CLR type of the start node.</typeparam>
    /// <typeparam name="TEnd">The CLR type of the end node.</typeparam>
    /// <param name="start">The start node.</param>
    /// <param name="end">The end node.</param>
    /// <param name="token">The token that may be used to cancel the operation.</param>
    /// <returns>A task that represents the staging operation.</returns>
    /// <exception cref="GraphContextException">Thrown when the operation cannot be staged.</exception>
    Task DisconnectAsync<TStart, TEnd>(TStart start, TEnd end, CancellationToken token = default)
        where TStart : class
        where TEnd : class;
}
