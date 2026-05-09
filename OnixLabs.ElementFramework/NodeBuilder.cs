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
/// Represents the fluent configuration implementation for a node type.
/// </summary>
/// <typeparam name="T">The CLR type being configured as a node.</typeparam>
internal sealed class NodeBuilder<T> : INodeBuilder<T>, INodeMetadataBuilder where T : class
{
    /// <summary>
    /// The set of property name overrides keyed by <see cref="PropertyInfo"/>.
    /// </summary>
    private readonly Dictionary<PropertyInfo, string> propertyNameOverrides = [];

    /// <summary>
    /// The set of properties explicitly excluded from the produced <see cref="NodeMetadata"/>.
    /// </summary>
    private readonly HashSet<PropertyInfo> ignoredProperties = [];

    /// <summary>
    /// The configured node label, defaulting to the CLR type name.
    /// </summary>
    private string label = typeof(T).Name;

    /// <summary>
    /// The configured key property, or <see langword="null"/> when no key has been designated.
    /// </summary>
    private PropertyInfo? keyProperty;

    /// <summary>
    /// Initializes a new instance of the <see cref="NodeBuilder{T}"/> class.
    /// </summary>
    internal NodeBuilder()
    {
    }

    /// <inheritdoc/>
    public INodeBuilder<T> HasLabel(string label)
    {
        this.label = label;
        return this;
    }

    /// <inheritdoc/>
    public INodeBuilder<T> HasKey<TKey>(Expression<Func<T, TKey>> selector)
    {
        keyProperty = ResolveProperty(selector);
        return this;
    }

    /// <inheritdoc/>
    public INodeBuilder<T> Property<TProperty>(Expression<Func<T, TProperty>> selector, string? name = null)
    {
        PropertyInfo property = ResolveProperty(selector);
        if (name is not null) propertyNameOverrides[property] = name;
        return this;
    }

    /// <inheritdoc/>
    public INodeBuilder<T> Ignore<TProperty>(Expression<Func<T, TProperty>> selector)
    {
        ignoredProperties.Add(ResolveProperty(selector));
        return this;
    }

    /// <inheritdoc/>
    NodeMetadata INodeMetadataBuilder.Build()
    {
        PropertyInfo[] declared = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        List<PropertyMetadata> properties = new(declared.Length);
        NullabilityInfoContext nullability = new();
        PropertyMetadata? keyMetadata = null;

        foreach (PropertyInfo property in declared)
        {
            if (ignoredProperties.Contains(property)) continue;
            string name = propertyNameOverrides.TryGetValue(property, out string? overridden) ? overridden : property.Name;
            bool nullable = nullability.Create(property).WriteState == NullabilityState.Nullable;
            PropertyMetadata metadata = new(property, name, nullable);
            properties.Add(metadata);
            if (property == keyProperty) keyMetadata = metadata;
        }

        return new NodeMetadata(typeof(T), label, keyMetadata, properties);
    }

    /// <summary>
    /// Resolves the <see cref="PropertyInfo"/> referenced by the supplied member-access expression.
    /// </summary>
    /// <typeparam name="TProperty">The CLR type of the selected property.</typeparam>
    /// <param name="selector">The lambda expression selecting the property.</param>
    /// <returns>Returns the <see cref="PropertyInfo"/> referenced by <paramref name="selector"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="selector"/> is not a property-access expression.</exception>
    private static PropertyInfo ResolveProperty<TProperty>(Expression<Func<T, TProperty>> selector)
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
