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

/// <summary>
/// The AGE provider keeps its own internal copy of <c>CypherIdentifier</c> rather than depending
/// on the Neo4j provider's. Both copies are intentionally identical — the reserved-word set and
/// the bare-identifier rules are openCypher-shaped, and AGE accepts the same grammar inside its
/// <c>cypher()</c> envelope as Neo4j does over Bolt. This test class mirrors the Neo4j coverage
/// so a future divergence (intentional or otherwise) lights up against both providers' suites.
/// </summary>
public class CypherIdentifierTests
{
    [Theory(DisplayName = "Escape returns bare identifier when it is alphanumeric, starts with a letter or underscore, and is not reserved")]
    [InlineData("Author")]
    [InlineData("author")]
    [InlineData("Author_1")]
    [InlineData("_internal")]
    [InlineData("a")]
    [InlineData("X9_y")]
    public void EscapeReturnsBareIdentifierForValidNonReservedInput(string input)
    {
        Assert.Equal(input, CypherIdentifier.Escape(input));
    }

    [Theory(DisplayName = "Escape wraps a reserved Cypher word in backticks regardless of casing")]
    [InlineData("MATCH", "`MATCH`")]
    [InlineData("match", "`match`")]
    [InlineData("Match", "`Match`")]
    [InlineData("RETURN", "`RETURN`")]
    [InlineData("create", "`create`")]
    [InlineData("Where", "`Where`")]
    public void EscapeWrapsReservedWordsInBackticks(string input, string expected)
    {
        Assert.Equal(expected, CypherIdentifier.Escape(input));
    }

    [Theory(DisplayName = "Escape wraps non-bare identifiers in backticks")]
    [InlineData("1starts_with_digit", "`1starts_with_digit`")]
    [InlineData("with space", "`with space`")]
    [InlineData("with-dash", "`with-dash`")]
    [InlineData("with.dot", "`with.dot`")]
    [InlineData("emoji😀", "`emoji😀`")]
    public void EscapeWrapsNonBareIdentifiersInBackticks(string input, string expected)
    {
        Assert.Equal(expected, CypherIdentifier.Escape(input));
    }

    [Fact(DisplayName = "Escape doubles embedded backticks inside the quoted form")]
    public void EscapeDoublesEmbeddedBackticks()
    {
        Assert.Equal("`a``b`", CypherIdentifier.Escape("a`b"));
    }

    [Theory(DisplayName = "Escape throws ArgumentException for empty or whitespace input")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void EscapeThrowsForBlankInput(string input)
    {
        Assert.Throws<ArgumentException>(() => CypherIdentifier.Escape(input));
    }

    [Fact(DisplayName = "Escape throws ArgumentNullException for null input")]
    public void EscapeThrowsForNullInput()
    {
        Assert.Throws<ArgumentNullException>(() => CypherIdentifier.Escape(null!));
    }
}
