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

namespace OnixLabs.ElementFramework.UnitTests;

public class PredicateTranslatorTests
{
    [Fact(DisplayName = "Translates property == constant against the bound parameter")]
    public void TranslatesPropertyEqualsConstant()
    {
        Expression<Func<Author, bool>> predicate = a => a.Name == "Alice";

        PropertyComparisonPredicate result = Assert.IsType<PropertyComparisonPredicate>(
            PredicateTranslator.Translate("a", predicate));

        Assert.Equal("a", result.Alias);
        Assert.Equal(nameof(Author.Name), result.ClrPropertyName);
        Assert.Equal(ComparisonOperator.Equal, result.Operator);
        Assert.Equal("Alice", result.Value);
    }

    [Fact(DisplayName = "Translates constant == property (operands swapped) without flipping the operator")]
    public void TranslatesConstantEqualsProperty()
    {
        Expression<Func<Author, bool>> predicate = a => "Alice" == a.Name;

        PropertyComparisonPredicate result = Assert.IsType<PropertyComparisonPredicate>(
            PredicateTranslator.Translate("a", predicate));

        Assert.Equal(ComparisonOperator.Equal, result.Operator);
        Assert.Equal("Alice", result.Value);
    }

    [Fact(DisplayName = "Translates property != constant")]
    public void TranslatesPropertyNotEqualsConstant()
    {
        Expression<Func<Author, bool>> predicate = a => a.Name != "Alice";

        PropertyComparisonPredicate result = Assert.IsType<PropertyComparisonPredicate>(
            PredicateTranslator.Translate("a", predicate));

        Assert.Equal(ComparisonOperator.NotEqual, result.Operator);
        Assert.Equal("Alice", result.Value);
    }

    [Fact(DisplayName = "Translates property < constant")]
    public void TranslatesPropertyLessThanConstant()
    {
        DateTimeOffset cutoff = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Expression<Func<Author, bool>> predicate = a => a.JoinedAt < cutoff;

        PropertyComparisonPredicate result = Assert.IsType<PropertyComparisonPredicate>(
            PredicateTranslator.Translate("a", predicate));

        Assert.Equal(ComparisonOperator.LessThan, result.Operator);
        Assert.Equal(cutoff, result.Value);
    }

    [Fact(DisplayName = "Translates property <= constant")]
    public void TranslatesPropertyLessThanOrEqualConstant()
    {
        DateTimeOffset cutoff = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Expression<Func<Author, bool>> predicate = a => a.JoinedAt <= cutoff;

        PropertyComparisonPredicate result = Assert.IsType<PropertyComparisonPredicate>(
            PredicateTranslator.Translate("a", predicate));

        Assert.Equal(ComparisonOperator.LessThanOrEqual, result.Operator);
    }

    [Fact(DisplayName = "Translates property > constant")]
    public void TranslatesPropertyGreaterThanConstant()
    {
        DateTimeOffset cutoff = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Expression<Func<Author, bool>> predicate = a => a.JoinedAt > cutoff;

        PropertyComparisonPredicate result = Assert.IsType<PropertyComparisonPredicate>(
            PredicateTranslator.Translate("a", predicate));

        Assert.Equal(ComparisonOperator.GreaterThan, result.Operator);
    }

    [Fact(DisplayName = "Translates property >= constant")]
    public void TranslatesPropertyGreaterThanOrEqualConstant()
    {
        DateTimeOffset cutoff = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Expression<Func<Author, bool>> predicate = a => a.JoinedAt >= cutoff;

        PropertyComparisonPredicate result = Assert.IsType<PropertyComparisonPredicate>(
            PredicateTranslator.Translate("a", predicate));

        Assert.Equal(ComparisonOperator.GreaterThanOrEqual, result.Operator);
    }

    [Fact(DisplayName = "Flips the operator when the property is on the right of an ordered comparison")]
    public void FlipsOperatorWhenPropertyOnRight()
    {
        DateTimeOffset cutoff = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Expression<Func<Author, bool>> predicate = a => cutoff < a.JoinedAt;

        PropertyComparisonPredicate result = Assert.IsType<PropertyComparisonPredicate>(
            PredicateTranslator.Translate("a", predicate));

        Assert.Equal(ComparisonOperator.GreaterThan, result.Operator);
        Assert.Equal(cutoff, result.Value);
    }

    [Fact(DisplayName = "Translates property == captured-variable by compiling and invoking the value side")]
    public void TranslatesPropertyEqualsCapturedVariable()
    {
        string captured = "Bob";
        Expression<Func<Author, bool>> predicate = a => a.Name == captured;

        PropertyComparisonPredicate result = Assert.IsType<PropertyComparisonPredicate>(
            PredicateTranslator.Translate("a", predicate));

        Assert.Equal("Bob", result.Value);
    }

    [Fact(DisplayName = "Translates property == null to a NullPredicate with IsNull = true")]
    public void TranslatesPropertyEqualsNull()
    {
        Expression<Func<Author, bool>> predicate = a => a.Name == null;

        NullPredicate result = Assert.IsType<NullPredicate>(PredicateTranslator.Translate("a", predicate));

        Assert.Equal(nameof(Author.Name), result.ClrPropertyName);
        Assert.True(result.IsNull);
    }

    [Fact(DisplayName = "Translates property != null to a NullPredicate with IsNull = false")]
    public void TranslatesPropertyNotEqualsNull()
    {
        Expression<Func<Author, bool>> predicate = a => a.Name != null;

        NullPredicate result = Assert.IsType<NullPredicate>(PredicateTranslator.Translate("a", predicate));

        Assert.False(result.IsNull);
    }

    [Fact(DisplayName = "Translates && into AndPredicate")]
    public void TranslatesAndAlsoIntoAndPredicate()
    {
        DateTimeOffset cutoff = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Expression<Func<Author, bool>> predicate = a => a.Name == "Alice" && a.JoinedAt > cutoff;

        AndPredicate result = Assert.IsType<AndPredicate>(PredicateTranslator.Translate("a", predicate));

        Assert.IsType<PropertyComparisonPredicate>(result.Left);
        Assert.IsType<PropertyComparisonPredicate>(result.Right);
    }

    [Fact(DisplayName = "Translates || into OrPredicate")]
    public void TranslatesOrElseIntoOrPredicate()
    {
        Expression<Func<Author, bool>> predicate = a => a.Name == "Alice" || a.Name == "Bob";

        OrPredicate result = Assert.IsType<OrPredicate>(PredicateTranslator.Translate("a", predicate));

        Assert.Equal("Alice", Assert.IsType<PropertyComparisonPredicate>(result.Left).Value);
        Assert.Equal("Bob", Assert.IsType<PropertyComparisonPredicate>(result.Right).Value);
    }

    [Fact(DisplayName = "Translates ! into NotPredicate")]
    public void TranslatesNotIntoNotPredicate()
    {
        Expression<Func<Author, bool>> predicate = a => !(a.Name == "Alice");

        NotPredicate result = Assert.IsType<NotPredicate>(PredicateTranslator.Translate("a", predicate));

        Assert.Equal("Alice", Assert.IsType<PropertyComparisonPredicate>(result.Inner).Value);
    }

    [Fact(DisplayName = "Translates string.Contains into StringComparisonPredicate.Contains")]
    public void TranslatesStringContains()
    {
        Expression<Func<Author, bool>> predicate = a => a.Name.Contains("li");

        StringComparisonPredicate result = Assert.IsType<StringComparisonPredicate>(
            PredicateTranslator.Translate("a", predicate));

        Assert.Equal(StringComparisonOperator.Contains, result.Operator);
        Assert.Equal("li", result.Value);
    }

    [Fact(DisplayName = "Translates string.StartsWith into StringComparisonPredicate.StartsWith")]
    public void TranslatesStringStartsWith()
    {
        Expression<Func<Author, bool>> predicate = a => a.Name.StartsWith("Al");

        StringComparisonPredicate result = Assert.IsType<StringComparisonPredicate>(
            PredicateTranslator.Translate("a", predicate));

        Assert.Equal(StringComparisonOperator.StartsWith, result.Operator);
    }

    [Fact(DisplayName = "Translates string.EndsWith into StringComparisonPredicate.EndsWith")]
    public void TranslatesStringEndsWith()
    {
        Expression<Func<Author, bool>> predicate = a => a.Name.EndsWith("ce");

        StringComparisonPredicate result = Assert.IsType<StringComparisonPredicate>(
            PredicateTranslator.Translate("a", predicate));

        Assert.Equal(StringComparisonOperator.EndsWith, result.Operator);
    }

    [Fact(DisplayName = "Composes nested boolean operators into a faithful tree")]
    public void ComposesNestedBooleanOperators()
    {
        DateTimeOffset cutoff = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Expression<Func<Author, bool>> predicate = a =>
            (a.Name == "Alice" || a.Name == "Bob") && a.JoinedAt > cutoff;

        AndPredicate root = Assert.IsType<AndPredicate>(PredicateTranslator.Translate("a", predicate));

        OrPredicate or = Assert.IsType<OrPredicate>(root.Left);
        Assert.Equal("Alice", Assert.IsType<PropertyComparisonPredicate>(or.Left).Value);
        Assert.Equal("Bob", Assert.IsType<PropertyComparisonPredicate>(or.Right).Value);

        PropertyComparisonPredicate joinedAt = Assert.IsType<PropertyComparisonPredicate>(root.Right);
        Assert.Equal(ComparisonOperator.GreaterThan, joinedAt.Operator);
    }

    [Fact(DisplayName = "Throws NotSupportedException when neither side is a parameter property access")]
    public void ThrowsWhenNeitherSideIsParameterProperty()
    {
        const string left = "x";
        const string right = "y";
        Expression<Func<Author, bool>> predicate = _ => left == right;

        Assert.Throws<NotSupportedException>(() => PredicateTranslator.Translate("a", predicate));
    }

    [Fact(DisplayName = "Throws NotSupportedException for property comparisons routed through a method call")]
    public void ThrowsForMethodCallPropertyAccess()
    {
        Expression<Func<Author, bool>> predicate = a => a.Name.ToUpperInvariant() == "ALICE";

        Assert.Throws<NotSupportedException>(() => PredicateTranslator.Translate("a", predicate));
    }

    [Fact(DisplayName = "Throws NotSupportedException for unsupported string method calls")]
    public void ThrowsForUnsupportedStringMethod()
    {
        Expression<Func<Author, bool>> predicate = a => a.Name.Equals("Alice");

        Assert.Throws<NotSupportedException>(() => PredicateTranslator.Translate("a", predicate));
    }

}
