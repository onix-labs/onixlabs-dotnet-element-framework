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
/// Defines metadata for a registered node type.
/// </summary>
public interface INodeMetadata
{
    /// <summary>
    /// Gets the CLR type of the node.
    /// </summary>
    /// <value>The <see cref="System.Type"/> representing the node's CLR runtime type.</value>
    Type Type { get; }

    /// <summary>
    /// Gets the label used to identify this node type in the underlying store.
    /// </summary>
    /// <value>The label string used by the underlying store to identify the node type.</value>
    string Label { get; }

    /// <summary>
    /// Gets the metadata for the node's natural key, or <see langword="null"/> if no key is configured.
    /// </summary>
    /// <value>The key's <see cref="IPropertyMetadata"/>, or <see langword="null"/> when no key is configured.</value>
    IPropertyMetadata? Key { get; }

    /// <summary>
    /// Gets the metadata for the node's mapped properties.
    /// </summary>
    /// <value>The collection of <see cref="IPropertyMetadata"/> describing each mapped property on the node.</value>
    IReadOnlyList<IPropertyMetadata> Properties { get; }
}
