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
using System.Reflection;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the fluent configuration implementation for a typed relationship.
/// </summary>
/// <typeparam name="TStart">The CLR type of the start node.</typeparam>
/// <typeparam name="TEdge">The CLR type being configured as the edge connecting the endpoints.</typeparam>
/// <typeparam name="TEnd">The CLR type of the end node.</typeparam>
internal sealed class RelationshipBuilder<TStart, TEdge, TEnd> : IRelationshipBuilder<TStart, TEdge, TEnd>, IRelationshipMetadataBuilder
    where TStart : class
    where TEdge : class
    where TEnd : class
{
    /// <summary>
    /// The set of edge property name overrides keyed by <see cref="PropertyInfo"/>.
    /// </summary>
    private readonly Dictionary<PropertyInfo, string> propertyNameOverrides = [];

    /// <summary>
    /// The set of edge properties explicitly excluded from the produced <see cref="RelationshipMetadata"/>.
    /// </summary>
    private readonly HashSet<PropertyInfo> ignoredProperties = [];

    /// <summary>
    /// The configured relationship type, defaulting to the edge CLR type name.
    /// </summary>
    private string relationshipType = typeof(TEdge).Name;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelationshipBuilder{TStart, TEdge, TEnd}"/> class.
    /// </summary>
    internal RelationshipBuilder()
    {
    }

    /// <inheritdoc/>
    public IRelationshipBuilder<TStart, TEdge, TEnd> HasType(string type)
    {
        relationshipType = type;
        return this;
    }

    /// <inheritdoc/>
    public IRelationshipBuilder<TStart, TEdge, TEnd> Property<TProperty>(Expression<Func<TEdge, TProperty>> selector, string? name = null)
    {
        PropertyInfo property = ResolveProperty(selector);
        if (name is not null) propertyNameOverrides[property] = name;
        return this;
    }

    /// <inheritdoc/>
    public IRelationshipBuilder<TStart, TEdge, TEnd> Ignore<TProperty>(Expression<Func<TEdge, TProperty>> selector)
    {
        ignoredProperties.Add(ResolveProperty(selector));
        return this;
    }

    /// <inheritdoc/>
    RelationshipMetadata IRelationshipMetadataBuilder.Build()
    {
        PropertyInfo[] declared = typeof(TEdge).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        List<PropertyMetadata> properties = new(declared.Length);
        NullabilityInfoContext nullability = new();

        foreach (PropertyInfo property in declared)
        {
            if (ignoredProperties.Contains(property)) continue;
            string name = propertyNameOverrides.TryGetValue(property, out string? overridden) ? overridden : property.Name;
            bool nullable = nullability.Create(property).WriteState == NullabilityState.Nullable;
            properties.Add(new PropertyMetadata(property, name, nullable));
        }

        return new RelationshipMetadata(typeof(TStart), typeof(TEdge), typeof(TEnd), relationshipType, properties);
    }

    /// <summary>
    /// Resolves the <see cref="PropertyInfo"/> referenced by the supplied member-access expression.
    /// </summary>
    /// <typeparam name="TProperty">The CLR type of the selected property.</typeparam>
    /// <param name="selector">The lambda expression selecting the property.</param>
    /// <returns>Returns the <see cref="PropertyInfo"/> referenced by <paramref name="selector"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="selector"/> is not a property-access expression.</exception>
    private static PropertyInfo ResolveProperty<TProperty>(Expression<Func<TEdge, TProperty>> selector)
    {
        return selector.Body switch
        {
            MemberExpression { Member: PropertyInfo p } => p,
            UnaryExpression { Operand: MemberExpression { Member: PropertyInfo p } } => p,
            _ => throw new ArgumentException(
                $"Selector must be a property access expression, but was {selector.Body.NodeType}.",
                nameof(selector))
        };
    }
}
