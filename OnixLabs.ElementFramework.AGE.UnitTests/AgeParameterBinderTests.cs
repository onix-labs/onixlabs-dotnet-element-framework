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

namespace OnixLabs.ElementFramework.AGE.UnitTests;

public class AgeParameterBinderTests
{
    [Fact(DisplayName = "Bind returns the preferred name prefixed with '$' on first use")]
    public void BindReturnsDollarPrefixedNameOnFirstUse()
    {
        AgeParameterBinder binder = new();
        Assert.Equal("$Name", binder.Bind("Name", "Alice"));
    }

    [Fact(DisplayName = "Bind appends a numeric suffix on a second use of the same name")]
    public void BindAppendsSuffixOnCollision()
    {
        AgeParameterBinder binder = new();
        Assert.Equal("$Name", binder.Bind("Name", "Alice"));
        Assert.Equal("$Name_1", binder.Bind("Name", "Bob"));
    }

    [Fact(DisplayName = "Bind continues incrementing suffixes across many collisions on the same name")]
    public void BindIncrementsSuffixesAcrossManyCollisions()
    {
        AgeParameterBinder binder = new();
        Assert.Equal("$Body", binder.Bind("Body", "a"));
        Assert.Equal("$Body_1", binder.Bind("Body", "b"));
        Assert.Equal("$Body_2", binder.Bind("Body", "c"));
        Assert.Equal("$Body_3", binder.Bind("Body", "d"));
    }

    [Fact(DisplayName = "Bind keeps distinct preferred names independent of each other")]
    public void BindKeepsDistinctNamesIndependent()
    {
        AgeParameterBinder binder = new();
        Assert.Equal("$Name", binder.Bind("Name", "Alice"));
        Assert.Equal("$Body", binder.Bind("Body", "x"));
        Assert.Equal("$Name_1", binder.Bind("Name", "Bob"));
    }

    [Fact(DisplayName = "ToParameters returns every bound value keyed by its final, collision-resolved name")]
    public void ToParametersReturnsAllBindings()
    {
        AgeParameterBinder binder = new();
        binder.Bind("Name", "Alice");
        binder.Bind("Body", "x");
        binder.Bind("Name", "Bob");

        IReadOnlyDictionary<string, object?> parameters = binder.ToParameters();
        Assert.Equal(3, parameters.Count);
        Assert.Equal("Alice", parameters["Name"]);
        Assert.Equal("x", parameters["Body"]);
        Assert.Equal("Bob", parameters["Name_1"]);
    }

    [Fact(DisplayName = "Bind preserves a null value under the bound parameter name")]
    public void BindPreservesNullValue()
    {
        AgeParameterBinder binder = new();
        binder.Bind("Optional", null);

        IReadOnlyDictionary<string, object?> parameters = binder.ToParameters();
        Assert.True(parameters.ContainsKey("Optional"));
        Assert.Null(parameters["Optional"]);
    }

    [Theory(DisplayName = "Bind throws ArgumentException for empty or whitespace preferred names")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void BindThrowsForBlankPreferredName(string preferredName)
    {
        AgeParameterBinder binder = new();
        Assert.Throws<ArgumentException>(() => binder.Bind(preferredName, "value"));
    }

    [Fact(DisplayName = "Bind throws ArgumentNullException for a null preferred name")]
    public void BindThrowsForNullPreferredName()
    {
        AgeParameterBinder binder = new();
        Assert.Throws<ArgumentNullException>(() => binder.Bind(null!, "value"));
    }
}
