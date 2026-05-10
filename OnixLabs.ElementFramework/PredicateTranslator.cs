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
/// Provides translation of lambda predicates into <see cref="TraversalPredicate"/> trees for the fluent traversal builder.
/// </summary>
/// <remarks>
/// Supported shapes:
/// <list type="bullet">
///   <item>Comparison operators <c>==</c>, <c>!=</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c> between a bound property and a constant.</item>
///   <item>Boolean composition <c>&amp;&amp;</c>, <c>||</c>, and unary <c>!</c>.</item>
///   <item>Null checks via <c>== null</c> / <c>!= null</c> in either operand order.</item>
///   <item>String instance methods <c>Contains</c>, <c>StartsWith</c>, and <c>EndsWith</c> in their single-argument overload.</item>
/// </list>
/// Anything else throws <see cref="NotSupportedException"/> with a pointer to <see cref="IRawStatementExecutor"/>.
/// </remarks>
internal static class PredicateTranslator
{
    /// <summary>
    /// Translates the supplied predicate expression into a <see cref="TraversalPredicate"/> tree scoped to <paramref name="alias"/>.
    /// </summary>
    /// <typeparam name="TNode">The bound CLR type the lambda parameter refers to.</typeparam>
    /// <param name="alias">The alias the predicate is scoped to (the alias of the bound segment that <c>Where</c> was invoked on).</param>
    /// <param name="predicate">The lambda predicate to translate.</param>
    /// <returns>Returns the translated predicate tree ready for accumulation in <see cref="TraversalState.Predicates"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown when the expression contains an unsupported shape.</exception>
    public static TraversalPredicate Translate<TNode>(string alias, Expression<Func<TNode, bool>> predicate) =>
        Walk(predicate.Body, alias, predicate.Parameters[0]);

    private static TraversalPredicate Walk(Expression expression, string alias, ParameterExpression parameter) =>
        expression.NodeType switch
        {
            ExpressionType.AndAlso => new AndPredicate(
                Walk(((BinaryExpression)expression).Left, alias, parameter),
                Walk(((BinaryExpression)expression).Right, alias, parameter)),
            ExpressionType.OrElse => new OrPredicate(
                Walk(((BinaryExpression)expression).Left, alias, parameter),
                Walk(((BinaryExpression)expression).Right, alias, parameter)),
            ExpressionType.Not => new NotPredicate(
                Walk(((UnaryExpression)expression).Operand, alias, parameter)),
            ExpressionType.Equal or
                ExpressionType.NotEqual or
                ExpressionType.LessThan or
                ExpressionType.LessThanOrEqual or
                ExpressionType.GreaterThan or
                ExpressionType.GreaterThanOrEqual => TranslateComparison((BinaryExpression)expression, alias, parameter),
            ExpressionType.Call => TranslateMethodCall((MethodCallExpression)expression, alias, parameter),
            _ => throw NotSupported(expression)
        };

    private static TraversalPredicate TranslateComparison(BinaryExpression binary, string alias, ParameterExpression parameter)
    {
        PropertyInfo property;
        Expression valueSide;
        bool flipped;

        if (TryReadParameterPropertyAccess(binary.Left, parameter, out PropertyInfo? leftProperty))
        {
            property = leftProperty;
            valueSide = binary.Right;
            flipped = false;
        }
        else if (TryReadParameterPropertyAccess(binary.Right, parameter, out PropertyInfo? rightProperty))
        {
            property = rightProperty;
            valueSide = binary.Left;
            flipped = true;
        }
        else
        {
            throw NotSupported(binary);
        }

        object? value = Evaluate(valueSide);

        if (value is null)
        {
            return binary.NodeType switch
            {
                ExpressionType.Equal => new NullPredicate(alias, property.Name, IsNull: true),
                ExpressionType.NotEqual => new NullPredicate(alias, property.Name, IsNull: false),
                _ => throw NotSupported(binary, "null may only be compared with == or !=.")
            };
        }

        ComparisonOperator op = binary.NodeType switch
        {
            ExpressionType.Equal => ComparisonOperator.Equal,
            ExpressionType.NotEqual => ComparisonOperator.NotEqual,
            ExpressionType.LessThan => flipped ? ComparisonOperator.GreaterThan : ComparisonOperator.LessThan,
            ExpressionType.LessThanOrEqual => flipped ? ComparisonOperator.GreaterThanOrEqual : ComparisonOperator.LessThanOrEqual,
            ExpressionType.GreaterThan => flipped ? ComparisonOperator.LessThan : ComparisonOperator.GreaterThan,
            ExpressionType.GreaterThanOrEqual => flipped ? ComparisonOperator.LessThanOrEqual : ComparisonOperator.GreaterThanOrEqual,
            _ => throw NotSupported(binary)
        };

        return new PropertyComparisonPredicate(alias, property.Name, op, value);
    }

    private static TraversalPredicate TranslateMethodCall(MethodCallExpression call, string alias, ParameterExpression parameter)
    {
        if (call.Method.DeclaringType != typeof(string) || call.Object is null)
            throw NotSupported(call);

        if (!TryReadParameterPropertyAccess(call.Object, parameter, out PropertyInfo? property))
            throw NotSupported(call);

        if (call.Arguments.Count != 1)
            throw NotSupported(call, "Only the single-argument overloads of Contains, StartsWith, and EndsWith are supported.");

        if (Evaluate(call.Arguments[0]) is not string value)
            throw NotSupported(call, "String operator argument must evaluate to a non-null string.");

        StringComparisonOperator op = call.Method.Name switch
        {
            nameof(string.Contains) => StringComparisonOperator.Contains,
            nameof(string.StartsWith) => StringComparisonOperator.StartsWith,
            nameof(string.EndsWith) => StringComparisonOperator.EndsWith,
            _ => throw NotSupported(call)
        };

        return new StringComparisonPredicate(alias, property.Name, op, value);
    }

    private static bool TryReadParameterPropertyAccess(
        Expression expression,
        ParameterExpression parameter,
        [NotNullWhen(true)] out PropertyInfo? property)
    {
        if (expression is MemberExpression { Member: PropertyInfo info, Expression: ParameterExpression candidate }
            && candidate == parameter)
        {
            property = info;
            return true;
        }
        property = null;
        return false;
    }

    private static object? Evaluate(Expression expression) =>
        Expression.Lambda(expression).Compile().DynamicInvoke();

    private static NotSupportedException NotSupported(Expression expression, string? detail = null) =>
        new($"Unsupported predicate shape '{expression.NodeType}'" +
            (detail is null ? "" : $": {detail}") +
            ". Supported shapes: comparison (==, !=, <, <=, >, >=), boolean composition (&&, ||, !), null checks, and string Contains/StartsWith/EndsWith. " +
            "Use IRawStatementExecutor.Execute(...) for richer expressions.");
}
