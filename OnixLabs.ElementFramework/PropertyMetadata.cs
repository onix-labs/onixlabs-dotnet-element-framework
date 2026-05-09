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
/// Represents a frozen description of a single mapped property on a registered node or edge type.
/// </summary>
internal sealed class PropertyMetadata : IPropertyMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyMetadata"/> class.
    /// </summary>
    /// <param name="clrProperty">The CLR property descriptor backing this mapping.</param>
    /// <param name="name">The property name as exposed to the underlying provider.</param>
    /// <param name="nullable">A value indicating whether the property accepts <see langword="null"/>.</param>
    internal PropertyMetadata(PropertyInfo clrProperty, string name, bool nullable)
    {
        Property = clrProperty;
        Name = name;
        Nullable = nullable;
        Getter = CompileGetter(clrProperty);
        Setter = CompileSetter(clrProperty);
    }

    /// <inheritdoc/>
    public PropertyInfo Property { get; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public bool Nullable { get; }

    /// <inheritdoc/>
    public Func<object, object?> Getter { get; }

    /// <inheritdoc/>
    public Action<object, object?> Setter { get; }

    private static Func<object, object?> CompileGetter(PropertyInfo property)
    {
        ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
        UnaryExpression typedInstance = Expression.Convert(instance, property.DeclaringType!);
        MemberExpression access = Expression.Property(typedInstance, property);
        UnaryExpression boxed = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<object, object?>>(boxed, instance).Compile();
    }

    private static Action<object, object?> CompileSetter(PropertyInfo property)
    {
        ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
        ParameterExpression value = Expression.Parameter(typeof(object), "value");
        UnaryExpression typedInstance = Expression.Convert(instance, property.DeclaringType!);
        UnaryExpression typedValue = Expression.Convert(value, property.PropertyType);
        BinaryExpression assign = Expression.Assign(Expression.Property(typedInstance, property), typedValue);
        return Expression.Lambda<Action<object, object?>>(assign, instance, value).Compile();
    }
}
