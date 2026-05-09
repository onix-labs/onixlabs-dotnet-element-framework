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
/// Defines a translator from a domain operation against an <see cref="IGraphModel"/> to a provider-specific <see cref="DataStatement"/>.
/// </summary>
public interface IStatementEmitter
{
    /// <summary>
    /// Emits the provider statement that adds <paramref name="node"/> to the underlying store.
    /// </summary>
    /// <typeparam name="T">The CLR type of the node being added.</typeparam>
    /// <param name="model">The frozen graph model used to resolve metadata.</param>
    /// <param name="node">The node instance to be added.</param>
    /// <returns>Returns the provider statement that performs the add operation.</returns>
    /// <exception cref="StatementEmissionException">Thrown when the statement cannot be emitted.</exception>
    DataStatement EmitAdd<T>(IGraphModel model, T node);

    /// <summary>
    /// Emits the provider statement that updates <paramref name="node"/> in the underlying store.
    /// </summary>
    /// <typeparam name="T">The CLR type of the node being updated.</typeparam>
    /// <param name="model">The frozen graph model used to resolve metadata.</param>
    /// <param name="node">The node instance to be updated.</param>
    /// <returns>Returns the provider statement that performs the update operation.</returns>
    /// <exception cref="StatementEmissionException">Thrown when the statement cannot be emitted.</exception>
    DataStatement EmitUpdate<T>(IGraphModel model, T node);

    /// <summary>
    /// Emits the provider statement that removes <paramref name="node"/> from the underlying store.
    /// </summary>
    /// <typeparam name="T">The CLR type of the node being removed.</typeparam>
    /// <param name="model">The frozen graph model used to resolve metadata.</param>
    /// <param name="node">The node instance to be removed.</param>
    /// <returns>Returns the provider statement that performs the remove operation.</returns>
    /// <exception cref="StatementEmissionException">Thrown when the statement cannot be emitted.</exception>
    DataStatement EmitRemove<T>(IGraphModel model, T node);

    /// <summary>
    /// Emits the provider statement that merges <paramref name="node"/> into the underlying store.
    /// </summary>
    /// <typeparam name="T">The CLR type of the node being merged.</typeparam>
    /// <param name="model">The frozen graph model used to resolve metadata.</param>
    /// <param name="node">The node instance to be merged.</param>
    /// <returns>Returns the provider statement that performs the merge operation.</returns>
    /// <exception cref="StatementEmissionException">Thrown when the statement cannot be emitted.</exception>
    DataStatement EmitMerge<T>(IGraphModel model, T node);

    /// <summary>
    /// Emits the provider statement that connects <paramref name="start"/> and <paramref name="end"/> via <paramref name="edge"/>.
    /// </summary>
    /// <typeparam name="TStart">The CLR type of the start node.</typeparam>
    /// <typeparam name="TEdge">The CLR type of the edge.</typeparam>
    /// <typeparam name="TEnd">The CLR type of the end node.</typeparam>
    /// <param name="model">The frozen graph model used to resolve metadata.</param>
    /// <param name="start">The start node of the relationship.</param>
    /// <param name="edge">The edge instance carrying the relationship's properties.</param>
    /// <param name="end">The end node of the relationship.</param>
    /// <returns>Returns the provider statement that performs the connect operation.</returns>
    /// <exception cref="StatementEmissionException">Thrown when the statement cannot be emitted.</exception>
    DataStatement EmitConnect<TStart, TEdge, TEnd>(IGraphModel model, TStart start, TEdge edge, TEnd end);

    /// <summary>
    /// Emits the provider statement that disconnects <paramref name="start"/> from <paramref name="end"/> across the relationship of type <typeparamref name="TEdge"/>.
    /// </summary>
    /// <typeparam name="TStart">The CLR type of the start node.</typeparam>
    /// <typeparam name="TEdge">The CLR type of the edge identifying the relationship to disconnect.</typeparam>
    /// <typeparam name="TEnd">The CLR type of the end node.</typeparam>
    /// <param name="model">The frozen graph model used to resolve metadata.</param>
    /// <param name="start">The start node of the relationship.</param>
    /// <param name="end">The end node of the relationship.</param>
    /// <returns>Returns the provider statement that performs the disconnect operation.</returns>
    /// <exception cref="StatementEmissionException">Thrown when the statement cannot be emitted.</exception>
    DataStatement EmitDisconnect<TStart, TEdge, TEnd>(IGraphModel model, TStart start, TEnd end);

    /// <summary>
    /// Emits the provider statement that finds a node of type <typeparamref name="T"/> by its key value.
    /// </summary>
    /// <typeparam name="T">The CLR type of the node to find.</typeparam>
    /// <param name="model">The frozen graph model used to resolve metadata.</param>
    /// <param name="key">The key value to look up.</param>
    /// <returns>Returns the provider statement that performs the lookup.</returns>
    /// <exception cref="StatementEmissionException">Thrown when the statement cannot be emitted.</exception>
    DataStatement EmitFindById<T>(IGraphModel model, object key);

    /// <summary>
    /// Emits the provider statement that tests whether a node of type <typeparamref name="T"/> with the supplied key exists.
    /// </summary>
    /// <typeparam name="T">The CLR type of the node whose existence is being tested.</typeparam>
    /// <param name="model">The frozen graph model used to resolve metadata.</param>
    /// <param name="key">The key value to test.</param>
    /// <returns>Returns the provider statement that performs the existence check.</returns>
    /// <exception cref="StatementEmissionException">Thrown when the statement cannot be emitted.</exception>
    DataStatement EmitExists<T>(IGraphModel model, object key);

    /// <summary>
    /// Emits the provider statement that enumerates all nodes of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The CLR type of the nodes to enumerate.</typeparam>
    /// <param name="model">The frozen graph model used to resolve metadata.</param>
    /// <returns>Returns the provider statement that enumerates the nodes.</returns>
    /// <exception cref="StatementEmissionException">Thrown when the statement cannot be emitted.</exception>
    DataStatement EmitAsEnumerableNodes<T>(IGraphModel model);

    /// <summary>
    /// Emits the provider statement that enumerates all edges of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The CLR type of the edges to enumerate.</typeparam>
    /// <param name="model">The frozen graph model used to resolve metadata.</param>
    /// <returns>Returns the provider statement that enumerates the edges.</returns>
    /// <exception cref="StatementEmissionException">Thrown when the statement cannot be emitted.</exception>
    DataStatement EmitAsEnumerableEdges<T>(IGraphModel model);

    /// <summary>
    /// Emits the provider statement for the supplied traversal AST.
    /// </summary>
    /// <param name="model">The frozen graph model used to resolve metadata.</param>
    /// <param name="ast">The traversal AST produced by the fluent builder.</param>
    /// <returns>Returns the provider statement that executes the traversal.</returns>
    /// <exception cref="StatementEmissionException">Thrown when the statement cannot be emitted.</exception>
    DataStatement EmitTraversal(IGraphModel model, TraversalAst ast);
}
