// All Rights Reserved License
//
// 1. Grant of License
// Subject to the terms and conditions of this License, ONIXLabs ("Licensor") hereby grants to you a limited, non-exclusive, non-transferable, non-sublicensable license to use the Software for commercial, private, and paid purposes. This license does not include any rights to modify, distribute, or create derivative works of the Software.
//
// 2. Permitted Uses
// You are permitted to:
//  - Use the Software for commercial purposes.
//  - Use the Software for private purposes.
//  - Use the Software for paid purposes.
//  - Exercise any patent rights associated with the Software, solely in connection with your use of the Software as permitted under this License.
//
// 3. Restrictions
// You are not permitted to:
//  - Modify, alter, or create any derivative works of the Software.
//  - Distribute, sublicense, lease, rent, or otherwise transfer the Software to any third party.
//  - Use the Software without obtaining a proper license for paid use.
//  - Use the Software in any way that infringes upon the trademarks, service marks, or trade names of the Licensor.
//  - Use the Software in any manner that could cause it to be considered open-source software or otherwise subject to an open-source license.
//
// 4. No Free Use
// This license does not permit any free use of the Software. Any use of the Software without a paid license is strictly prohibited.
//
// 5. No Liability
// To the maximum extent permitted by applicable law, the Software is provided "as is" and "as available" without warranty of any kind, express or implied, including but not limited to the implied warranties of merchantability, fitness for a particular purpose, and non-infringement. In no event shall the Licensor be liable for any damages whatsoever arising out of the use of or inability to use the Software, even if the Licensor has been advised of the possibility of such damages.
//
// 6. No Warranty
// The Licensor makes no warranty that the Software will meet your requirements, be uninterrupted, secure, or error-free. The Licensor disclaims all warranties with respect to the Software, whether express or implied, including but not limited to any warranties of merchantability, fitness for a particular purpose, and non-infringement.
//
// 7. Termination
// This license is effective until terminated. Your rights under this license will terminate automatically without notice if you fail to comply with any term of this license. Upon termination, you must immediately cease all use of the Software and destroy all copies of the Software in your possession or control.
//
// 8. Governing Law
// This license will be governed by and construed in accordance with the laws of [Your Jurisdiction], without regard to its conflict of laws principles.
//
// 9. Entire Agreement
// This license constitutes the entire agreement between you and the Licensor concerning the Software and supersedes all prior or contemporaneous communications, agreements, or understandings, whether oral or written, concerning the subject matter hereof.
//
// By using the Software, you acknowledge that you have read and understood this license and agree to be bound by its terms and conditions.

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
    private readonly ConcurrentDictionary<Type, Func<object>> instantiators = new();

    /// <inheritdoc/>
    public T MaterializeNode<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row, string alias) => row[alias] is INode node
        ? Materialize<T>(model.GetNode(typeof(T)).Properties, node.Properties)
        : throw new ResultMaterializationException($"Expected an INode at row alias '{alias}', got {row[alias]?.GetType().FullName ?? "null"}.");

    /// <inheritdoc/>
    public T MaterializeEdge<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row, string alias) => row[alias] is IRelationship relationship
        ? Materialize<T>(model.GetRelationship(typeof(T)).Properties, relationship.Properties)
        : throw new ResultMaterializationException($"Expected an IRelationship at row alias '{alias}', got {row[alias]?.GetType().FullName ?? "null"}.");

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
