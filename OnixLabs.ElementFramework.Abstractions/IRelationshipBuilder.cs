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
/// Defines the fluent configuration surface for a typed relationship.
/// </summary>
/// <typeparam name="TStart">The CLR type of the start node.</typeparam>
/// <typeparam name="TEdge">The CLR type being configured as the edge connecting the endpoints.</typeparam>
/// <typeparam name="TEnd">The CLR type of the end node.</typeparam>
public interface IRelationshipBuilder<TStart, TEdge, TEnd>
    where TStart : class
    where TEdge : class
    where TEnd : class
{
    /// <summary>
    /// Overrides the relationship type that the underlying store uses for this relationship.
    /// </summary>
    /// <param name="type">The relationship type to apply.</param>
    /// <returns>This <see cref="IRelationshipBuilder{TStart, TEdge, TEnd}"/> to allow further chaining.</returns>
    IRelationshipBuilder<TStart, TEdge, TEnd> HasType(string type);

    /// <summary>
    /// Configures a property mapping for this relationship, optionally overriding the property name written to the underlying store.
    /// </summary>
    /// <typeparam name="TProperty">The CLR type of the property.</typeparam>
    /// <param name="selector">An expression selecting the property.</param>
    /// <param name="name">The property name to apply, or <see langword="null"/> to use the CLR property name.</param>
    /// <returns>This <see cref="IRelationshipBuilder{TStart, TEdge, TEnd}"/> to allow further chaining.</returns>
    IRelationshipBuilder<TStart, TEdge, TEnd> Property<TProperty>(Expression<Func<TEdge, TProperty>> selector, string? name = null);

    /// <summary>
    /// Excludes a property from being mapped to the underlying store.
    /// </summary>
    /// <typeparam name="TProperty">The CLR type of the property.</typeparam>
    /// <param name="selector">An expression selecting the property to exclude.</param>
    /// <returns>This <see cref="IRelationshipBuilder{TStart, TEdge, TEnd}"/> to allow further chaining.</returns>
    IRelationshipBuilder<TStart, TEdge, TEnd> Ignore<TProperty>(Expression<Func<TEdge, TProperty>> selector);
}
