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
/// Represents a frozen description of a registered relationship triple.
/// </summary>
internal sealed class RelationshipMetadata : IRelationshipMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RelationshipMetadata"/> class.
    /// </summary>
    /// <param name="startType">The CLR type of the start node.</param>
    /// <param name="edgeType">The CLR type of the edge connecting the endpoints.</param>
    /// <param name="endType">The CLR type of the end node.</param>
    /// <param name="relationshipType">The relationship type written to the underlying store.</param>
    /// <param name="properties">The full list of mapped edge properties in CLR property declaration order, excluding ignored properties.</param>
    internal RelationshipMetadata(
        Type startType,
        Type edgeType,
        Type endType,
        string relationshipType,
        IReadOnlyList<PropertyMetadata> properties)
    {
        StartType = startType;
        EdgeType = edgeType;
        EndType = endType;
        RelationshipType = relationshipType;
        Properties = properties;
    }

    /// <inheritdoc/>
    public Type StartType { get; }

    /// <inheritdoc/>
    public Type EdgeType { get; }

    /// <inheritdoc/>
    public Type EndType { get; }

    /// <inheritdoc/>
    public string RelationshipType { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IPropertyMetadata> Properties { get; }
}
