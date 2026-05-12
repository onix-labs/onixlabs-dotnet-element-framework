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
/// Defines a frozen, queryable representation of the registered graph domain model.
/// </summary>
public interface IGraphModel
{
    /// <summary>
    /// Gets the metadata for every registered node type in the model.
    /// </summary>
    /// <value>The collection of <see cref="INodeMetadata"/> for every registered node type, in registration order.</value>
    IReadOnlyList<INodeMetadata> Nodes { get; }

    /// <summary>
    /// Gets the metadata for every registered relationship in the model.
    /// </summary>
    /// <value>The collection of <see cref="IRelationshipMetadata"/> for every registered relationship, in registration order.</value>
    IReadOnlyList<IRelationshipMetadata> Relationships { get; }

    /// <summary>
    /// Gets the metadata for the registered node type identified by <paramref name="nodeType"/>.
    /// </summary>
    /// <param name="nodeType">The CLR type of the node to look up.</param>
    /// <returns>Returns the metadata for the registered node type.</returns>
    /// <exception cref="ModelConfigurationException">Thrown when the node type is not registered with the model.</exception>
    INodeMetadata GetNode(Type nodeType);

    /// <summary>
    /// Gets the metadata for the registered relationship identified by <paramref name="edgeType"/>.
    /// </summary>
    /// <param name="edgeType">The CLR type of the edge to look up.</param>
    /// <returns>Returns the metadata for the registered relationship.</returns>
    /// <exception cref="ModelConfigurationException">Thrown when the relationship is not registered with the model.</exception>
    IRelationshipMetadata GetRelationship(Type edgeType);
}
