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
/// Defines the fluent surface for declaring the graph domain model.
/// </summary>
public interface IGraphModelBuilder
{
    /// <summary>
    /// Begins or continues configuration for a node type.
    /// </summary>
    /// <typeparam name="T">The CLR type to register as a node.</typeparam>
    /// <returns>Returns an <see cref="INodeBuilder{T}"/> for further fluent configuration.</returns>
    INodeBuilder<T> Node<T>() where T : class;

    /// <summary>
    /// Begins or continues configuration for a relationship: the triple of start node, edge, and end node.
    /// </summary>
    /// <typeparam name="TStart">The CLR type of the start node.</typeparam>
    /// <typeparam name="TEdge">The CLR type to register as the edge connecting the endpoints.</typeparam>
    /// <typeparam name="TEnd">The CLR type of the end node.</typeparam>
    /// <returns>Returns an <see cref="IRelationshipBuilder{TStart, TEdge, TEnd}"/> for further fluent configuration.</returns>
    /// <exception cref="ModelConfigurationException">Thrown when the same edge CLR type is registered with conflicting endpoint types — each edge CLR type belongs to exactly one relationship.</exception>
    IRelationshipBuilder<TStart, TEdge, TEnd> Relationship<TStart, TEdge, TEnd>()
        where TStart : class
        where TEdge : class
        where TEnd : class;

    /// <summary>
    /// Applies a node-type configuration to this model. Allows model-creation code to be split into one class per node aggregate.
    /// </summary>
    /// <typeparam name="TNode">The CLR type being configured as a node.</typeparam>
    /// <param name="configuration">The node-type configuration to apply.</param>
    /// <returns>Returns this <see cref="IGraphModelBuilder"/> to allow further chaining.</returns>
    IGraphModelBuilder ApplyConfiguration<TNode>(INodeTypeConfiguration<TNode> configuration) where TNode : class;

    /// <summary>
    /// Applies a relationship configuration to this model. Allows model-creation code to be split into one class per relationship triple.
    /// </summary>
    /// <typeparam name="TStart">The CLR type of the start node.</typeparam>
    /// <typeparam name="TEdge">The CLR type of the edge connecting the endpoints.</typeparam>
    /// <typeparam name="TEnd">The CLR type of the end node.</typeparam>
    /// <param name="configuration">The relationship configuration to apply.</param>
    /// <returns>Returns this <see cref="IGraphModelBuilder"/> to allow further chaining.</returns>
    IGraphModelBuilder ApplyConfiguration<TStart, TEdge, TEnd>(IRelationshipConfiguration<TStart, TEdge, TEnd> configuration)
        where TStart : class
        where TEdge : class
        where TEnd : class;
}
