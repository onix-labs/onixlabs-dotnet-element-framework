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

        TraversalPredicate result = PredicateTranslator.Translate("a", predicate);

        Assert.Equal("a", result.Alias);
        Assert.Equal(nameof(Author.Name), result.ClrPropertyName);
        Assert.Equal("Alice", result.Value);
    }

    [Fact(DisplayName = "Translates constant == property against the bound parameter (operands swapped)")]
    public void TranslatesConstantEqualsProperty()
    {
        Expression<Func<Author, bool>> predicate = a => "Alice" == a.Name;

        TraversalPredicate result = PredicateTranslator.Translate("a", predicate);

        Assert.Equal(nameof(Author.Name), result.ClrPropertyName);
        Assert.Equal("Alice", result.Value);
    }

    [Fact(DisplayName = "Translates property == captured-variable by compiling and invoking the value side")]
    public void TranslatesPropertyEqualsCapturedVariable()
    {
        string captured = "Bob";
        Expression<Func<Author, bool>> predicate = a => a.Name == captured;

        TraversalPredicate result = PredicateTranslator.Translate("a", predicate);

        Assert.Equal(nameof(Author.Name), result.ClrPropertyName);
        Assert.Equal("Bob", result.Value);
    }

    [Fact(DisplayName = "Throws NotSupportedException for non-equality operators")]
    public void ThrowsForNonEqualityOperator()
    {
        Expression<Func<Post, bool>> predicate = p => p.Title.Length > 5;

        Assert.Throws<NotSupportedException>(() => PredicateTranslator.Translate("p", predicate));
    }

    [Fact(DisplayName = "Throws NotSupportedException when neither side is a parameter property access")]
    public void ThrowsWhenNeitherSideIsParameterProperty()
    {
        const string left = "x";
        const string right = "y";
        Expression<Func<Author, bool>> predicate = _ => left == right;

        Assert.Throws<NotSupportedException>(() => PredicateTranslator.Translate("a", predicate));
    }

    [Fact(DisplayName = "Throws NotSupportedException when the property access is on a method-call result")]
    public void ThrowsForMethodCallPropertyAccess()
    {
        Expression<Func<Author, bool>> predicate = a => a.Name.ToUpperInvariant() == "ALICE";

        Assert.Throws<NotSupportedException>(() => PredicateTranslator.Translate("a", predicate));
    }
}
