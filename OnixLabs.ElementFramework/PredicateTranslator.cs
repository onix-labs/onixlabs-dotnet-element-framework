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

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Provides translation of property-equality lambda expressions into <see cref="TraversalPredicate"/> values for the fluent traversal builder.
/// </summary>
/// <remarks>
/// V1 supports only <c>x =&gt; x.PropertyName == value</c> shapes; anything richer (path predicates, function calls, list comprehensions, boolean composition, non-equality operators) throws <see cref="NotSupportedException"/> with a pointer to the raw statement escape hatch.
/// </remarks>
internal static class PredicateTranslator
{
    /// <summary>
    /// Translates the supplied predicate expression into a property-equality <see cref="TraversalPredicate"/>.
    /// </summary>
    /// <typeparam name="TNode">The bound CLR type the lambda parameter refers to.</typeparam>
    /// <param name="alias">The alias the predicate is scoped to (the alias of the bound segment that <c>Where</c> was invoked on).</param>
    /// <param name="predicate">The lambda predicate to translate.</param>
    /// <returns>The translated predicate ready for accumulation in <see cref="TraversalState.Predicates"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown when the expression shape is anything other than <c>p =&gt; p.Property == constant</c>.</exception>
    public static TraversalPredicate Translate<TNode>(string alias, Expression<Func<TNode, bool>> predicate)
    {
        if (predicate.Body is not BinaryExpression { NodeType: ExpressionType.Equal } binary)
            throw NotSupported();

        ParameterExpression parameter = predicate.Parameters[0];
        PropertyInfo property;
        Expression valueSide;

        if (TryReadParameterPropertyAccess(binary.Left, parameter, out PropertyInfo? leftProperty))
        {
            property = leftProperty;
            valueSide = binary.Right;
        }
        else if (TryReadParameterPropertyAccess(binary.Right, parameter, out PropertyInfo? rightProperty))
        {
            property = rightProperty;
            valueSide = binary.Left;
        }
        else
        {
            throw NotSupported();
        }

        object? value = Expression.Lambda(valueSide).Compile().DynamicInvoke();
        return new TraversalPredicate(alias, property.Name, value);
    }

    private static bool TryReadParameterPropertyAccess(Expression expression, ParameterExpression parameter, [NotNullWhen(true)] out PropertyInfo? property)
    {
        if (expression is MemberExpression { Member: PropertyInfo info, Expression: ParameterExpression candidate } && candidate == parameter)
        {
            property = info;
            return true;
        }
        property = null;
        return false;
    }

    private static NotSupportedException NotSupported() =>
        new("Only property-equality predicates over the bound variable are supported (e.g. x => x.Name == \"Alice\"). " +
            "Use IRawStatementExecutor.Execute(...) for richer expressions.");
}
