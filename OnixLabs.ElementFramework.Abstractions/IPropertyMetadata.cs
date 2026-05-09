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

using System.Reflection;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Defines metadata for a mapped property within a node or relationship type.
/// </summary>
public interface IPropertyMetadata
{
    /// <summary>
    /// Gets the CLR property described by this metadata.
    /// </summary>
    /// <value>The <see cref="System.Reflection.PropertyInfo"/> for the underlying CLR property.</value>
    PropertyInfo Property { get; }

    /// <summary>
    /// Gets the property's name as exposed to the underlying provider.
    /// </summary>
    /// <value>The provider-facing property name.</value>
    string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the property accepts <see langword="null"/>.
    /// </summary>
    /// <value><see langword="true"/> if the property accepts <see langword="null"/>; otherwise, <see langword="false"/>.</value>
    bool Nullable { get; }

    /// <summary>
    /// Gets a compiled accessor that reads the property value from a target instance, avoiding per-call reflection.
    /// </summary>
    /// <value>The compiled accessor that reads the property value from a target instance.</value>
    Func<object, object?> Getter { get; }

    /// <summary>
    /// Gets a compiled accessor that writes a property value to a target instance, avoiding per-call reflection.
    /// </summary>
    /// <value>The compiled accessor that writes a property value to a target instance.</value>
    Action<object, object?> Setter { get; }
}
