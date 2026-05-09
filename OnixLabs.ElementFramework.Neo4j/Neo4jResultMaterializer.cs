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

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Neo4j.Driver;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the Neo4j implementation of <see cref="IResultMaterializer"/> that projects a Bolt entity from a result row into a CLR node or edge instance.
/// </summary>
/// <remarks>
/// Reads the bound Bolt entity (<see cref="INode"/> or <see cref="IRelationship"/>) from the supplied row, then walks each registered <see cref="IPropertyMetadata"/> and assigns the converted value to the matching CLR property. CLR-side instance construction uses the type's parameterless constructor when available (compiled once and cached per type), falling back to <see cref="RuntimeHelpers.GetUninitializedObject(Type)"/> only for types that lack one. Bolt-to-CLR conversions cover the inverse of <see cref="PropertySerializer"/>: <see cref="ZonedDateTime"/> to <see cref="DateTimeOffset"/>, <see cref="string"/> to <see cref="Guid"/> when the target is <see cref="Guid"/>, primitives passthrough.
/// </remarks>
internal sealed class Neo4jResultMaterializer : IResultMaterializer
{
    /// <summary>
    /// Cache of compiled parameterless-constructor delegates keyed by CLR type, used by <see cref="Materialize{T}"/>.
    /// </summary>
    private readonly ConcurrentDictionary<Type, Func<object>> instantiators = new();

    /// <inheritdoc/>
    public T MaterializeNode<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row, string alias) => row[alias] is INode node
        ? Materialize<T>(model.GetNode(typeof(T)).Properties, node.Properties)
        : throw new ResultMaterializationException($"Expected an INode at row alias '{alias}', got {row[alias]?.GetType().FullName ?? "null"}.");

    /// <inheritdoc/>
    public T MaterializeEdge<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row, string alias) => row[alias] is IRelationship relationship
        ? Materialize<T>(model.GetRelationship(typeof(T)).Properties, relationship.Properties)
        : throw new ResultMaterializationException($"Expected an IRelationship at row alias '{alias}', got {row[alias]?.GetType().FullName ?? "null"}.");

    /// <summary>
    /// Constructs a CLR instance of <typeparamref name="T"/> and assigns each mapped property from <paramref name="source"/>.
    /// </summary>
    /// <typeparam name="T">The CLR type of the materialized instance.</typeparam>
    /// <param name="properties">The mapped property metadata for the type.</param>
    /// <param name="source">The Bolt entity properties keyed by stored name.</param>
    /// <returns>Returns the populated CLR instance.</returns>
    private T Materialize<T>(IReadOnlyList<IPropertyMetadata> properties, IReadOnlyDictionary<string, object> source)
    {
        T instance = (T)instantiators.GetOrAdd(typeof(T), BuildInstantiator)();

        foreach (IPropertyMetadata property in properties)
        {
            if (!source.TryGetValue(property.Name, out object? raw)) continue;
            object? converted = Convert(raw, property.Property.PropertyType);
            property.Setter(instance!, converted);
        }

        return instance;
    }

    /// <summary>
    /// Builds a delegate that constructs an instance of <paramref name="type"/>, preferring its parameterless constructor and falling back to <see cref="RuntimeHelpers.GetUninitializedObject(Type)"/> when none exists.
    /// </summary>
    /// <param name="type">The CLR type to build an instantiator for.</param>
    /// <returns>Returns a delegate that produces a fresh instance of <paramref name="type"/>.</returns>
    private static Func<object> BuildInstantiator(Type type)
    {
        ConstructorInfo? parameterless = type.GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        return parameterless is not null
            ? Expression.Lambda<Func<object>>(Expression.New(parameterless)).Compile()
            : () => RuntimeHelpers.GetUninitializedObject(type);
    }

    /// <summary>
    /// Converts a Bolt-side <paramref name="value"/> to the CLR <paramref name="targetType"/>, applying the inverse of <see cref="PropertySerializer"/>.
    /// </summary>
    /// <param name="value">The raw Bolt value, possibly <see langword="null"/>.</param>
    /// <param name="targetType">The CLR property type to convert to.</param>
    /// <returns>Returns the converted CLR value, or <see langword="null"/> when <paramref name="value"/> is <see langword="null"/>.</returns>
    private static object? Convert(object? value, Type targetType)
    {
        if (value is null) return null;

        Type underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying == typeof(Guid) && value is string guidText) return Guid.Parse(guidText);
        if (underlying == typeof(DateTimeOffset) && value is ZonedDateTime zoned) return zoned.ToDateTimeOffset();
        if (underlying.IsEnum && value is string enumText) return Enum.Parse(underlying, enumText);
        if (underlying != value.GetType() && underlying.IsPrimitive) return System.Convert.ChangeType(value, underlying);
        return value;
    }
}
