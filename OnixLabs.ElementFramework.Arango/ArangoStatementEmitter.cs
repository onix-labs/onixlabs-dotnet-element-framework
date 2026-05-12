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
/// Represents the ArangoDB implementation of <see cref="IStatementEmitter"/> that translates framework operations into AQL with bind variables.
/// </summary>
/// <remarks>
/// Each emit method returns a <see cref="DataStatement"/> whose statement text is AQL and whose parameter dictionary keys follow ArangoDB's bind-variable convention: value bind variables are keyed without a leading <c>@</c> (referenced in AQL as <c>@name</c>), and collection bind variables are keyed with a single leading <c>@</c> (referenced in AQL as <c>@@name</c>). The provider maps node types to document collections by <see cref="INodeMetadata.Label"/>, and relationship types to edge collections by <see cref="IRelationshipMetadata.RelationshipType"/>. The framework's key property is mirrored into the document's <c>_key</c> field as well as kept under its own property name, so the materializer reads the key like any other property. Edges store their endpoints as <c>_from</c> / <c>_to</c> document IDs of the form <c>Collection/_key</c>; edge identity is composite (<c>_from</c>, <c>_to</c>) with no separate edge key — disconnects remove all edges matching the endpoint pair.
/// </remarks>
internal sealed class ArangoStatementEmitter : IStatementEmitter
{
    private const string CollectionParameter = "@col";
    private const string EdgeCollectionParameter = "@edge";
    private const string KeyParameter = "key";
    private const string DocumentParameter = "doc";
    private const string FromParameter = "from";
    private const string ToParameter = "to";

    /// <inheritdoc/>
    public DataStatement EmitAdd<T>(IGraphModel model, T node)
    {
        INodeMetadata meta = ResolveNode<T>(model);
        string keyValue = ExtractKey(meta, node);
        Dictionary<string, object?> doc = BuildNodeDocument(meta, node, keyValue);
        Dictionary<string, object?> parameters = new()
        {
            [DocumentParameter] = doc,
            [CollectionParameter] = meta.Label
        };
        return new DataStatement($"INSERT @{DocumentParameter} INTO @{CollectionParameter}", parameters);
    }

    /// <inheritdoc/>
    public DataStatement EmitUpdate<T>(IGraphModel model, T node)
    {
        INodeMetadata meta = ResolveNode<T>(model);
        string keyValue = ExtractKey(meta, node);
        Dictionary<string, object?> doc = BuildNodeUpdateDocument(meta, node);
        Dictionary<string, object?> parameters = new()
        {
            [KeyParameter] = keyValue,
            [DocumentParameter] = doc,
            [CollectionParameter] = meta.Label
        };
        return new DataStatement($"UPDATE @{KeyParameter} WITH @{DocumentParameter} IN @{CollectionParameter}", parameters);
    }

    /// <inheritdoc/>
    public DataStatement EmitRemove<T>(IGraphModel model, T node)
    {
        INodeMetadata meta = ResolveNode<T>(model);
        string keyValue = ExtractKey(meta, node);
        Dictionary<string, object?> parameters = new()
        {
            [KeyParameter] = keyValue,
            [CollectionParameter] = meta.Label
        };
        return new DataStatement($"REMOVE @{KeyParameter} IN @{CollectionParameter}", parameters);
    }

    /// <inheritdoc/>
    public DataStatement EmitMerge<T>(IGraphModel model, T node)
    {
        INodeMetadata meta = ResolveNode<T>(model);
        string keyValue = ExtractKey(meta, node);
        Dictionary<string, object?> insertDoc = BuildNodeDocument(meta, node, keyValue);
        Dictionary<string, object?> updateDoc = BuildNodeUpdateDocument(meta, node);
        Dictionary<string, object?> parameters = new()
        {
            [KeyParameter] = keyValue,
            ["insert"] = insertDoc,
            ["update"] = updateDoc,
            [CollectionParameter] = meta.Label
        };
        return new DataStatement(
            $"UPSERT {{ _key: @{KeyParameter} }} INSERT @insert UPDATE @update IN @{CollectionParameter}",
            parameters);
    }

    /// <inheritdoc/>
    public DataStatement EmitConnect<TStart, TEdge, TEnd>(IGraphModel model, TStart start, TEdge edge, TEnd end)
    {
        (string fromId, string toId, IRelationshipMetadata edgeMeta) = ResolveEdge<TStart, TEdge, TEnd>(model, start, end);
        Dictionary<string, object?> doc = BuildEdgeDocument(edgeMeta, edge, fromId, toId);
        Dictionary<string, object?> parameters = new()
        {
            [DocumentParameter] = doc,
            [EdgeCollectionParameter] = edgeMeta.RelationshipType
        };
        return new DataStatement($"INSERT @{DocumentParameter} INTO @{EdgeCollectionParameter}", parameters);
    }

    /// <inheritdoc/>
    public DataStatement EmitDisconnect<TStart, TEdge, TEnd>(IGraphModel model, TStart start, TEnd end)
    {
        (string fromId, string toId, IRelationshipMetadata edgeMeta) = ResolveEdge<TStart, TEdge, TEnd>(model, start, end);
        Dictionary<string, object?> parameters = new()
        {
            [FromParameter] = fromId,
            [ToParameter] = toId,
            [EdgeCollectionParameter] = edgeMeta.RelationshipType
        };
        return new DataStatement(
            $"FOR e IN @{EdgeCollectionParameter} FILTER e._from == @{FromParameter} AND e._to == @{ToParameter} REMOVE e IN @{EdgeCollectionParameter}",
            parameters);
    }

    /// <inheritdoc/>
    public DataStatement EmitFindById<T>(IGraphModel model, object key)
    {
        INodeMetadata meta = ResolveNode<T>(model);
        string keyValue = ArangoPropertySerializer.SerializeKey(key);
        Dictionary<string, object?> parameters = new()
        {
            [KeyParameter] = keyValue,
            [CollectionParameter] = meta.Label
        };
        return new DataStatement(
            $"FOR {ArangoResultMaterializer.NodeAlias} IN @{CollectionParameter} FILTER {ArangoResultMaterializer.NodeAlias}._key == @{KeyParameter} LIMIT 1 RETURN {{ {ArangoResultMaterializer.NodeAlias}: {ArangoResultMaterializer.NodeAlias} }}",
            parameters);
    }

    /// <inheritdoc/>
    public DataStatement EmitExists<T>(IGraphModel model, object key)
    {
        INodeMetadata meta = ResolveNode<T>(model);
        string keyValue = ArangoPropertySerializer.SerializeKey(key);
        Dictionary<string, object?> parameters = new()
        {
            [KeyParameter] = keyValue,
            [CollectionParameter] = meta.Label
        };
        return new DataStatement(
            $"RETURN {{ {ArangoResultMaterializer.CountAlias}: LENGTH(FOR n IN @{CollectionParameter} FILTER n._key == @{KeyParameter} LIMIT 1 RETURN 1) }}",
            parameters);
    }

    /// <inheritdoc/>
    public DataStatement EmitAsEnumerableNodes<T>(IGraphModel model)
    {
        INodeMetadata meta = ResolveNode<T>(model);
        Dictionary<string, object?> parameters = new()
        {
            [CollectionParameter] = meta.Label
        };
        return new DataStatement(
            $"FOR {ArangoResultMaterializer.NodeAlias} IN @{CollectionParameter} RETURN {{ {ArangoResultMaterializer.NodeAlias}: {ArangoResultMaterializer.NodeAlias} }}",
            parameters);
    }

    /// <inheritdoc/>
    public DataStatement EmitAsEnumerableEdges<T>(IGraphModel model)
    {
        IRelationshipMetadata meta = ResolveEdge<T>(model);
        Dictionary<string, object?> parameters = new()
        {
            [EdgeCollectionParameter] = meta.RelationshipType
        };
        return new DataStatement(
            $"FOR {ArangoResultMaterializer.EdgeAlias} IN @{EdgeCollectionParameter} RETURN {{ {ArangoResultMaterializer.EdgeAlias}: {ArangoResultMaterializer.EdgeAlias} }}",
            parameters);
    }

    /// <inheritdoc/>
    public DataStatement EmitTraversal(IGraphModel model, TraversalAst ast) =>
        ArangoTraversalEmitter.Emit(model, ast);

    private static INodeMetadata ResolveNode<T>(IGraphModel model)
    {
        INodeMetadata meta = model.GetNode(typeof(T));
        if (meta.Key is null)
            throw new StatementEmissionException($"Node type '{typeof(T).FullName}' has no key configured; ArangoDB requires every document to have an immutable _key.");
        return meta;
    }

    private static IRelationshipMetadata ResolveEdge<T>(IGraphModel model) =>
        model.GetRelationship(typeof(T));

    private static (string FromId, string ToId, IRelationshipMetadata EdgeMeta) ResolveEdge<TStart, TEdge, TEnd>(IGraphModel model, TStart start, TEnd end)
    {
        INodeMetadata startMeta = ResolveNode<TStart>(model);
        INodeMetadata endMeta = ResolveNode<TEnd>(model);
        IRelationshipMetadata edgeMeta = ResolveEdge<TEdge>(model);
        string startKey = ExtractKey(startMeta, start);
        string endKey = ExtractKey(endMeta, end);
        return (DocumentId(startMeta.Label, startKey), DocumentId(endMeta.Label, endKey), edgeMeta);
    }

    private static string ExtractKey<T>(INodeMetadata meta, T instance) =>
        ArangoPropertySerializer.SerializeKey(meta.Key!.Getter(instance!));

    private static string DocumentId(string collection, string key) => $"{collection}/{key}";

    private static Dictionary<string, object?> BuildNodeDocument<T>(INodeMetadata meta, T node, string keyValue)
    {
        Dictionary<string, object?> doc = new(meta.Properties.Count + 1, StringComparer.Ordinal)
        {
            ["_key"] = keyValue
        };
        foreach (IPropertyMetadata property in meta.Properties)
            doc[property.Name] = ArangoPropertySerializer.Serialize(property.Getter(node!));
        return doc;
    }

    private static Dictionary<string, object?> BuildNodeUpdateDocument<T>(INodeMetadata meta, T node)
    {
        Dictionary<string, object?> doc = new(meta.Properties.Count, StringComparer.Ordinal);
        foreach (IPropertyMetadata property in meta.Properties)
        {
            if (ReferenceEquals(property, meta.Key)) continue;
            doc[property.Name] = ArangoPropertySerializer.Serialize(property.Getter(node!));
        }
        return doc;
    }

    private static Dictionary<string, object?> BuildEdgeDocument<TEdge>(IRelationshipMetadata meta, TEdge edge, string fromId, string toId)
    {
        Dictionary<string, object?> doc = new(meta.Properties.Count + 2, StringComparer.Ordinal)
        {
            ["_from"] = fromId,
            ["_to"] = toId
        };
        foreach (IPropertyMetadata property in meta.Properties)
            doc[property.Name] = ArangoPropertySerializer.Serialize(property.Getter(edge!));
        return doc;
    }
}
