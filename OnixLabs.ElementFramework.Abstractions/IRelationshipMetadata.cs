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
/// Defines metadata for a registered relationship.
/// </summary>
public interface IRelationshipMetadata
{
    /// <summary>
    /// Gets the CLR type of the start node.
    /// </summary>
    /// <value>The <see cref="Type"/> representing the start node's CLR runtime type.</value>
    Type StartType { get; }

    /// <summary>
    /// Gets the CLR type of the edge.
    /// </summary>
    /// <value>The <see cref="Type"/> representing the edge's CLR runtime type.</value>
    Type EdgeType { get; }

    /// <summary>
    /// Gets the CLR type of the end node.
    /// </summary>
    /// <value>The <see cref="Type"/> representing the end node's CLR runtime type.</value>
    Type EndType { get; }

    /// <summary>
    /// Gets the relationship type used to identify this relationship in the underlying store.
    /// </summary>
    /// <value>The relationship-type string used by the underlying store to identify the relationship.</value>
    string RelationshipType { get; }

    /// <summary>
    /// Gets the metadata for the edge's mapped properties.
    /// </summary>
    /// <value>The collection of <see cref="IPropertyMetadata"/> describing each mapped property on the edge.</value>
    IReadOnlyList<IPropertyMetadata> Properties { get; }
}
