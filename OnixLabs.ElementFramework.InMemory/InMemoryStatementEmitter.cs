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
/// Represents the in-memory provider's <see cref="IStatementEmitter"/> implementation.
/// </summary>
/// <remarks>
/// Rather than emitting a query-language string, the emitter encodes the requested operation as an op-code in
/// <see cref="DataStatement.Statement"/> and packs the resolved key and payload references in
/// <see cref="DataStatement.Parameters"/>. The <see cref="InMemoryRawStatementExecutor"/> interprets the resulting
/// statement against the in-memory store.
/// </remarks>
internal sealed class InMemoryStatementEmitter : IStatementEmitter
{
    /// <summary>The op-code emitted for adding a node.</summary>
    internal const string AddNode = "ADD_NODE";

    /// <summary>The op-code emitted for updating a node.</summary>
    internal const string UpdateNode = "UPDATE_NODE";

    /// <summary>The op-code emitted for removing a node.</summary>
    internal const string RemoveNode = "REMOVE_NODE";

    /// <summary>The op-code emitted for merging (upserting) a node.</summary>
    internal const string MergeNode = "MERGE_NODE";

    /// <summary>The op-code emitted for adding an edge between two nodes.</summary>
    internal const string AddEdge = "ADD_EDGE";

    /// <summary>The op-code emitted for removing edges between two nodes.</summary>
    internal const string RemoveEdge = "REMOVE_EDGE";

    /// <summary>The op-code emitted for finding a node by its configured key.</summary>
    internal const string FindById = "FIND_BY_ID";

    /// <summary>The op-code emitted for testing node existence by its configured key.</summary>
    internal const string Exists = "EXISTS";

    /// <summary>The op-code emitted for enumerating every node of a type.</summary>
    internal const string EnumerateNodes = "ENUMERATE_NODES";

    /// <summary>The op-code emitted for enumerating every edge of a type.</summary>
    internal const string EnumerateEdges = "ENUMERATE_EDGES";

    /// <summary>The parameter key carrying a <see cref="Type"/> discriminator.</summary>
    internal const string TypeParameter = "__type";

    /// <summary>The parameter key carrying a node's configured key value.</summary>
    internal const string KeyParameter = "__key";

    /// <summary>The parameter key carrying a node payload.</summary>
    internal const string NodeParameter = "__node";

    /// <summary>The parameter key carrying a start-node's CLR type.</summary>
    internal const string StartTypeParameter = "__startType";

    /// <summary>The parameter key carrying a start-node's configured key value.</summary>
    internal const string StartKeyParameter = "__startKey";

    /// <summary>The parameter key carrying an edge's CLR type.</summary>
    internal const string EdgeTypeParameter = "__edgeType";

    /// <summary>The parameter key carrying an edge payload.</summary>
    internal const string EdgeParameter = "__edge";

    /// <summary>The parameter key carrying an end-node's CLR type.</summary>
    internal const string EndTypeParameter = "__endType";

    /// <summary>The parameter key carrying an end-node's configured key value.</summary>
    internal const string EndKeyParameter = "__endKey";

    /// <inheritdoc/>
    public DataStatement EmitAdd<T>(IGraphModel model, T node) => EmitNodeStatement(AddNode, model, node);

    /// <inheritdoc/>
    public DataStatement EmitUpdate<T>(IGraphModel model, T node) => EmitNodeStatement(UpdateNode, model, node);

    /// <inheritdoc/>
    public DataStatement EmitRemove<T>(IGraphModel model, T node) => EmitNodeStatement(RemoveNode, model, node);

    /// <inheritdoc/>
    public DataStatement EmitMerge<T>(IGraphModel model, T node) => EmitNodeStatement(MergeNode, model, node);

    /// <inheritdoc/>
    public DataStatement EmitConnect<TStart, TEdge, TEnd>(IGraphModel model, TStart start, TEdge edge, TEnd end)
    {
        object startKey = ResolveKey(model, typeof(TStart), start!);
        object endKey = ResolveKey(model, typeof(TEnd), end!);
        return new DataStatement(AddEdge, new Dictionary<string, object?>
        {
            [StartTypeParameter] = typeof(TStart),
            [StartKeyParameter] = startKey,
            [EdgeTypeParameter] = typeof(TEdge),
            [EdgeParameter] = edge!,
            [EndTypeParameter] = typeof(TEnd),
            [EndKeyParameter] = endKey
        });
    }

    /// <inheritdoc/>
    public DataStatement EmitDisconnect<TStart, TEdge, TEnd>(IGraphModel model, TStart start, TEnd end)
    {
        object startKey = ResolveKey(model, typeof(TStart), start!);
        object endKey = ResolveKey(model, typeof(TEnd), end!);
        return new DataStatement(RemoveEdge, new Dictionary<string, object?>
        {
            [StartTypeParameter] = typeof(TStart),
            [StartKeyParameter] = startKey,
            [EdgeTypeParameter] = typeof(TEdge),
            [EndTypeParameter] = typeof(TEnd),
            [EndKeyParameter] = endKey
        });
    }

    /// <inheritdoc/>
    public DataStatement EmitFindById<T>(IGraphModel model, object key) => new(FindById, new Dictionary<string, object?>
    {
        [TypeParameter] = typeof(T),
        [KeyParameter] = key
    });

    /// <inheritdoc/>
    public DataStatement EmitExists<T>(IGraphModel model, object key) => new(Exists, new Dictionary<string, object?>
    {
        [TypeParameter] = typeof(T),
        [KeyParameter] = key
    });

    /// <inheritdoc/>
    public DataStatement EmitAsEnumerableNodes<T>(IGraphModel model) => new(EnumerateNodes, new Dictionary<string, object?>
    {
        [TypeParameter] = typeof(T)
    });

    /// <inheritdoc/>
    public DataStatement EmitAsEnumerableEdges<T>(IGraphModel model) => new(EnumerateEdges, new Dictionary<string, object?>
    {
        [TypeParameter] = typeof(T)
    });

    /// <inheritdoc/>
    public DataStatement EmitTraversal(IGraphModel model, TraversalAst ast) =>
        throw new StatementEmissionException(
            "The in-memory provider routes traversals through ITraversalTranslator directly, not via EmitTraversal.");

    /// <summary>
    /// Builds a single-node statement that carries the resolved key and node instance in its parameters.
    /// </summary>
    /// <typeparam name="T">The CLR type of the node.</typeparam>
    /// <param name="opcode">The op-code identifying the node operation.</param>
    /// <param name="model">The frozen graph model used to resolve the node's configured key property.</param>
    /// <param name="node">The node instance being staged.</param>
    /// <returns>Returns the encoded <see cref="DataStatement"/>.</returns>
    private static DataStatement EmitNodeStatement<T>(string opcode, IGraphModel model, T node)
    {
        object key = ResolveKey(model, typeof(T), node!);
        return new DataStatement(opcode, new Dictionary<string, object?>
        {
            [TypeParameter] = typeof(T),
            [KeyParameter] = key,
            [NodeParameter] = node!
        });
    }

    /// <summary>
    /// Resolves the configured key value for the supplied instance.
    /// </summary>
    /// <param name="model">The frozen graph model that owns the node metadata.</param>
    /// <param name="type">The CLR type of the node.</param>
    /// <param name="node">The node instance whose key is being read.</param>
    /// <returns>Returns the key value, never <see langword="null"/>.</returns>
    /// <exception cref="StatementEmissionException">Thrown when the node type has no key configured or the configured key returns <see langword="null"/>.</exception>
    private static object ResolveKey(IGraphModel model, Type type, object node)
    {
        INodeMetadata metadata = model.GetNode(type);
        if (metadata.Key is null)
            throw new StatementEmissionException(
                $"Node type {type.FullName} has no key configured. Call builder.HasKey(...) in the node's configuration.");

        object? key = metadata.Key.Getter(node);
        if (key is null)
            throw new StatementEmissionException(
                $"Node of type {type.FullName} has a null key value on property '{metadata.Key.Name}'.");

        return key;
    }
}
