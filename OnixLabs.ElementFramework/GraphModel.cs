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
/// Represents a frozen, queryable representation of the registered graph domain model.
/// </summary>
internal sealed class GraphModel : IGraphModel
{
    /// <summary>
    /// The lookup of registered nodes keyed by CLR type.
    /// </summary>
    private readonly Dictionary<Type, NodeMetadata> nodesByClrType;

    /// <summary>
    /// The lookup of registered relationships keyed by edge CLR type.
    /// </summary>
    private readonly Dictionary<Type, RelationshipMetadata> relationshipsByEdgeType;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphModel"/> class.
    /// </summary>
    /// <param name="nodes">The frozen node metadata produced by the builder pipeline.</param>
    /// <param name="relationships">The frozen relationship metadata produced by the builder pipeline.</param>
    /// <exception cref="ModelConfigurationException">Thrown when validation fails: a node has no configured key, two nodes share a label, a relationship endpoint type is not registered as a node, or two relationships share a (start, end, relationship-type) triple.</exception>
    internal GraphModel(IReadOnlyList<NodeMetadata> nodes, IReadOnlyList<RelationshipMetadata> relationships)
    {
        Validate(nodes, relationships);
        Nodes = nodes;
        Relationships = relationships;
        nodesByClrType = nodes.ToDictionary(node => node.Type);
        relationshipsByEdgeType = relationships.ToDictionary(relationship => relationship.EdgeType);
    }

    /// <summary>
    /// Gets the registered node metadata in registration order.
    /// </summary>
    /// <value>The collection of <see cref="NodeMetadata"/> in registration order.</value>
    internal IReadOnlyList<NodeMetadata> Nodes { get; }

    /// <summary>
    /// Gets the registered relationship metadata in registration order.
    /// </summary>
    /// <value>The collection of <see cref="RelationshipMetadata"/> in registration order.</value>
    internal IReadOnlyList<RelationshipMetadata> Relationships { get; }

    /// <inheritdoc/>
    public INodeMetadata GetNode(Type nodeType)
    {
        return nodesByClrType.TryGetValue(nodeType, out NodeMetadata? node)
            ? node
            : throw new ModelConfigurationException($"No node of type {nodeType.FullName} is registered in this model.");
    }

    /// <inheritdoc/>
    public IRelationshipMetadata GetRelationship(Type edgeType)
    {
        return relationshipsByEdgeType.TryGetValue(edgeType, out RelationshipMetadata? relationship)
            ? relationship
            : throw new ModelConfigurationException($"No relationship with edge type {edgeType.FullName} is registered in this model.");
    }

    /// <summary>
    /// Validates the supplied node and relationship metadata to ensure the model is internally consistent before being frozen.
    /// </summary>
    /// <param name="nodes">The registered node metadata.</param>
    /// <param name="relationships">The registered relationship metadata.</param>
    /// <exception cref="ModelConfigurationException">Thrown when validation fails.</exception>
    private static void Validate(IReadOnlyList<NodeMetadata> nodes, IReadOnlyList<RelationshipMetadata> relationships)
    {
        foreach (NodeMetadata node in nodes)
            if (node.Key is null)
                throw new ModelConfigurationException(
                    $"Node {node.Type.FullName} has no key configured. Call builder.HasKey(...) in the node's configuration.");

        HashSet<string> labels = [];
        foreach (NodeMetadata node in nodes)
            if (!labels.Add(node.Label))
                throw new ModelConfigurationException(
                    $"Duplicate node label '{node.Label}' is registered for multiple node types.");

        HashSet<Type> nodeTypes = nodes.Select(node => node.Type).ToHashSet();
        foreach (RelationshipMetadata relationship in relationships)
        {
            if (!nodeTypes.Contains(relationship.StartType))
                throw new ModelConfigurationException(
                    $"Relationship {relationship.EdgeType.FullName}: start endpoint type " +
                    $"{relationship.StartType.FullName} is not registered as a node.");

            if (!nodeTypes.Contains(relationship.EndType))
                throw new ModelConfigurationException(
                    $"Relationship {relationship.EdgeType.FullName}: end endpoint type " +
                    $"{relationship.EndType.FullName} is not registered as a node.");
        }

        Dictionary<(Type Start, Type End, string Type), Type> seen = [];
        foreach (RelationshipMetadata relationship in relationships)
        {
            (Type, Type, string) key = (relationship.StartType, relationship.EndType, relationship.RelationshipType);
            if (seen.TryGetValue(key, out Type? existingEdge))
                throw new ModelConfigurationException(
                    $"Relationship type '{relationship.RelationshipType}' is registered twice between " +
                    $"{relationship.StartType.Name} and {relationship.EndType.Name} " +
                    $"(edge types: {existingEdge.FullName} and {relationship.EdgeType.FullName}).");

            seen[key] = relationship.EdgeType;
        }
    }
}
