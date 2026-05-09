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

using System.Linq.Expressions;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Defines the fluent configuration surface for a node type.
/// </summary>
/// <typeparam name="T">The CLR type being configured as a node.</typeparam>
public interface INodeBuilder<T> where T : class
{
    /// <summary>
    /// Overrides the label that the underlying store uses for this node type.
    /// </summary>
    /// <param name="label">The label to apply.</param>
    /// <returns>This <see cref="INodeBuilder{T}"/> to allow further chaining.</returns>
    INodeBuilder<T> HasLabel(string label);

    /// <summary>
    /// Designates a property as the natural identifier for this node type. Required for identifier-based operations on the node set.
    /// </summary>
    /// <typeparam name="TKey">The CLR type of the key property.</typeparam>
    /// <param name="selector">An expression selecting the key property.</param>
    /// <returns>This <see cref="INodeBuilder{T}"/> to allow further chaining.</returns>
    INodeBuilder<T> HasKey<TKey>(Expression<Func<T, TKey>> selector);

    /// <summary>
    /// Configures a property mapping for this node type, optionally overriding the property name written to the underlying store.
    /// </summary>
    /// <typeparam name="TProperty">The CLR type of the property.</typeparam>
    /// <param name="selector">An expression selecting the property.</param>
    /// <param name="name">The property name to apply, or <see langword="null"/> to use the CLR property name.</param>
    /// <returns>This <see cref="INodeBuilder{T}"/> to allow further chaining.</returns>
    INodeBuilder<T> Property<TProperty>(Expression<Func<T, TProperty>> selector, string? name = null);

    /// <summary>
    /// Excludes a property from being mapped to the underlying store.
    /// </summary>
    /// <typeparam name="TProperty">The CLR type of the property.</typeparam>
    /// <param name="selector">An expression selecting the property to exclude.</param>
    /// <returns>This <see cref="INodeBuilder{T}"/> to allow further chaining.</returns>
    INodeBuilder<T> Ignore<TProperty>(Expression<Func<T, TProperty>> selector);
}
