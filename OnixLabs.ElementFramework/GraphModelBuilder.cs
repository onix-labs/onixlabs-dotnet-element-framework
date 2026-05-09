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
/// Represents the fluent builder implementation for declaring the graph domain model.
/// </summary>
internal sealed class GraphModelBuilder : IGraphModelBuilder
{
    private readonly Dictionary<Type, INodeMetadataBuilder> nodeBuilders = [];
    private readonly Dictionary<Type, IRelationshipMetadataBuilder> relationshipBuilders = [];

    /// <inheritdoc/>
    public INodeBuilder<T> Node<T>() where T : class
    {
        if (nodeBuilders.TryGetValue(typeof(T), out INodeMetadataBuilder? existing))
            return (NodeBuilder<T>)existing;

        NodeBuilder<T> builder = new();
        nodeBuilders[typeof(T)] = builder;
        return builder;
    }

    /// <inheritdoc/>
    public IRelationshipBuilder<TStart, TEdge, TEnd> Relationship<TStart, TEdge, TEnd>()
        where TStart : class
        where TEdge : class
        where TEnd : class
    {
        if (relationshipBuilders.TryGetValue(typeof(TEdge), out IRelationshipMetadataBuilder? existing))
        {
            if (existing is RelationshipBuilder<TStart, TEdge, TEnd> match) return match;
            throw new ModelConfigurationException(
                $"Edge type {typeof(TEdge).FullName} is already registered with different endpoint types.");
        }

        RelationshipBuilder<TStart, TEdge, TEnd> builder = new();
        relationshipBuilders[typeof(TEdge)] = builder;
        return builder;
    }

    /// <inheritdoc/>
    public IGraphModelBuilder ApplyConfiguration<TNode>(INodeTypeConfiguration<TNode> configuration) where TNode : class
    {
        configuration.Configure(Node<TNode>());
        return this;
    }

    /// <inheritdoc/>
    public IGraphModelBuilder ApplyConfiguration<TStart, TEdge, TEnd>(IRelationshipConfiguration<TStart, TEdge, TEnd> configuration)
        where TStart : class
        where TEdge : class
        where TEnd : class
    {
        configuration.Configure(Relationship<TStart, TEdge, TEnd>());
        return this;
    }

    /// <summary>
    /// Materializes the accumulated builder configuration into a frozen, validated <see cref="GraphModel"/>.
    /// </summary>
    /// <returns>The frozen <see cref="GraphModel"/> built from the registered nodes and relationships.</returns>
    /// <exception cref="ModelConfigurationException">Thrown when validation of the resulting model fails.</exception>
    public GraphModel Build()
    {
        List<NodeMetadata> nodes = nodeBuilders.Values.Select(b => b.Build()).ToList();
        List<RelationshipMetadata> relationships = relationshipBuilders.Values.Select(b => b.Build()).ToList();
        return new GraphModel(nodes, relationships);
    }
}
