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
/// Represents a frozen description of a registered node type.
/// </summary>
internal sealed class NodeMetadata : INodeMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NodeMetadata"/> class.
    /// </summary>
    /// <param name="clrType">The CLR type registered as this node.</param>
    /// <param name="label">The label written to the underlying store for this node type.</param>
    /// <param name="key">The configured key property, or <see langword="null"/> when no key has been designated.</param>
    /// <param name="properties">The full list of mapped properties in CLR property declaration order, excluding ignored properties.</param>
    internal NodeMetadata(Type clrType, string label, PropertyMetadata? key, IReadOnlyList<PropertyMetadata> properties)
    {
        Type = clrType;
        Label = label;
        Key = key;
        Properties = properties;
    }

    /// <inheritdoc/>
    public Type Type { get; }

    /// <inheritdoc/>
    public string Label { get; }

    /// <inheritdoc/>
    public IPropertyMetadata? Key { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IPropertyMetadata> Properties { get; }
}
