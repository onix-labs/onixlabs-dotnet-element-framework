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
/// Represents the mutable in-memory backing store for a named in-memory graph database.
/// </summary>
/// <remarks>
/// Holds node instances keyed by CLR type and configured key value, plus an ordered list of edges as
/// <see cref="InMemoryEdge"/> tuples. The store is intentionally single-threaded — concurrent contexts pointing
/// at the same store are not protected against data races.
/// </remarks>
public sealed class InMemoryStore
{
    private Dictionary<(Type Type, object Key), object> nodes = [];
    private List<InMemoryEdge> edges = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryStore"/> class.
    /// </summary>
    /// <param name="databaseName">The unique name identifying this store within the process-wide registry.</param>
    internal InMemoryStore(string databaseName) => DatabaseName = databaseName;

    /// <summary>
    /// Gets the unique name identifying this store within the process-wide registry.
    /// </summary>
    /// <value>The database name supplied at registration time.</value>
    public string DatabaseName { get; }

    /// <summary>
    /// Removes every node and edge from the store.
    /// </summary>
    public void Clear()
    {
        nodes.Clear();
        edges.Clear();
    }

    /// <summary>
    /// Inserts or replaces the node identified by <paramref name="type"/> and <paramref name="key"/>.
    /// </summary>
    /// <param name="type">The CLR type of the node.</param>
    /// <param name="key">The configured key value of the node.</param>
    /// <param name="node">The node instance.</param>
    internal void UpsertNode(Type type, object key, object node) => nodes[(type, key)] = node;

    /// <summary>
    /// Removes the node identified by <paramref name="type"/> and <paramref name="key"/> and any edges that touch it.
    /// </summary>
    /// <param name="type">The CLR type of the node.</param>
    /// <param name="key">The configured key value of the node.</param>
    /// <returns>Returns <see langword="true"/> when a node was removed; otherwise <see langword="false"/>.</returns>
    internal bool RemoveNode(Type type, object key)
    {
        if (!nodes.Remove((type, key))) return false;
        edges.RemoveAll(e =>
            (e.StartType == type && e.StartKey.Equals(key)) ||
            (e.EndType == type && e.EndKey.Equals(key)));
        return true;
    }

    /// <summary>
    /// Finds the node identified by <paramref name="type"/> and <paramref name="key"/>.
    /// </summary>
    /// <param name="type">The CLR type of the node.</param>
    /// <param name="key">The configured key value of the node.</param>
    /// <returns>Returns the node instance, or <see langword="null"/> when no such node exists.</returns>
    internal object? FindNode(Type type, object key) =>
        nodes.TryGetValue((type, key), out object? value) ? value : null;

    /// <summary>
    /// Yields every node of the supplied <paramref name="type"/> in insertion order.
    /// </summary>
    /// <param name="type">The CLR type of the nodes to enumerate.</param>
    /// <returns>Returns the matching node instances.</returns>
    internal IEnumerable<object> NodesOfType(Type type) => nodes
        .Where(kv => kv.Key.Type == type)
        .Select(kv => kv.Value);

    /// <summary>
    /// Adds an edge to the store.
    /// </summary>
    /// <param name="edge">The edge tuple to add.</param>
    internal void AddEdge(InMemoryEdge edge) => edges.Add(edge);

    /// <summary>
    /// Removes every edge of the supplied <paramref name="edgeType"/> between the configured endpoints.
    /// </summary>
    /// <param name="startType">The CLR type of the start node.</param>
    /// <param name="startKey">The configured key of the start node.</param>
    /// <param name="edgeType">The CLR type of the edge.</param>
    /// <param name="endType">The CLR type of the end node.</param>
    /// <param name="endKey">The configured key of the end node.</param>
    /// <returns>Returns the number of edges that were removed.</returns>
    internal int RemoveEdges(Type startType, object startKey, Type edgeType, Type endType, object endKey) =>
        edges.RemoveAll(e =>
            e.EdgeType == edgeType &&
            e.StartType == startType && e.StartKey.Equals(startKey) &&
            e.EndType == endType && e.EndKey.Equals(endKey));

    /// <summary>
    /// Yields every edge of the supplied <paramref name="edgeType"/>.
    /// </summary>
    /// <param name="edgeType">The CLR type of the edges to enumerate.</param>
    /// <returns>Returns the matching edge tuples.</returns>
    internal IEnumerable<InMemoryEdge> EdgesOfType(Type edgeType) =>
        edges.Where(e => e.EdgeType == edgeType);

    /// <summary>
    /// Yields every edge of the supplied <paramref name="edgeType"/> incident on the supplied node, mapping the
    /// far endpoint into a <c>(EndType, EndKey)</c> projection that reflects the requested <paramref name="direction"/>.
    /// </summary>
    /// <param name="nodeType">The CLR type of the bound node.</param>
    /// <param name="nodeKey">The configured key of the bound node.</param>
    /// <param name="edgeType">The CLR type of the edge.</param>
    /// <param name="direction">The relationship direction relative to the bound node.</param>
    /// <returns>Returns each incident edge along with the far-endpoint type and key.</returns>
    internal IEnumerable<(InMemoryEdge Edge, Type FarType, object FarKey)> EdgesIncidentTo(
        Type nodeType, object nodeKey, Type edgeType, RelationshipDirection direction)
    {
        foreach (InMemoryEdge edge in edges)
        {
            if (edge.EdgeType != edgeType) continue;

            bool outgoing = edge.StartType == nodeType && edge.StartKey.Equals(nodeKey);
            bool incoming = edge.EndType == nodeType && edge.EndKey.Equals(nodeKey);

            switch (direction)
            {
                case RelationshipDirection.Outgoing when outgoing:
                    yield return (edge, edge.EndType, edge.EndKey);
                    break;
                case RelationshipDirection.Incoming when incoming:
                    yield return (edge, edge.StartType, edge.StartKey);
                    break;
                case RelationshipDirection.Either when outgoing:
                    yield return (edge, edge.EndType, edge.EndKey);
                    break;
                case RelationshipDirection.Either when incoming:
                    yield return (edge, edge.StartType, edge.StartKey);
                    break;
            }
        }
    }

    /// <summary>
    /// Returns a snapshot of this store that shares no mutable state with the original.
    /// </summary>
    /// <returns>Returns a deep-enough copy for transaction isolation. Node and edge payload instances are shared by reference; only the lookup structures are duplicated.</returns>
    internal InMemoryStore Clone()
    {
        InMemoryStore copy = new(DatabaseName)
        {
            nodes = new Dictionary<(Type, object), object>(nodes),
            edges = [.. edges]
        };
        return copy;
    }

    /// <summary>
    /// Replaces this store's contents with the contents of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The source store whose contents this store should adopt.</param>
    internal void ReplaceWith(InMemoryStore other)
    {
        nodes = other.nodes;
        edges = other.edges;
    }
}
