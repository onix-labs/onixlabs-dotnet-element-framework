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

public class AgtypeReaderTests
{
    [Fact(DisplayName = "ParseScalar reads a JSON-quoted string as a CLR string")]
    public void ParseScalarReadsString()
    {
        Assert.Equal("Alice", AgtypeReader.ParseScalar("\"Alice\""));
    }

    [Fact(DisplayName = "ParseScalar reads an integer as System.Int64 — guards against the (object) cast bug that silently widened long to double")]
    public void ParseScalarReadsIntegerAsLong()
    {
        object? result = AgtypeReader.ParseScalar("42");

        Assert.IsType<long>(result);
        Assert.Equal(42L, result);
    }

    [Fact(DisplayName = "ParseScalar reads a fractional number as System.Double")]
    public void ParseScalarReadsFractionalAsDouble()
    {
        object? result = AgtypeReader.ParseScalar("3.14");

        Assert.IsType<double>(result);
        Assert.Equal(3.14, (double)result!, 6);
    }

    [Theory(DisplayName = "ParseScalar reads booleans as CLR booleans")]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void ParseScalarReadsBooleans(string text, bool expected)
    {
        Assert.Equal(expected, AgtypeReader.ParseScalar(text));
    }

    [Fact(DisplayName = "ParseScalar reads JSON null as null")]
    public void ParseScalarReadsNull()
    {
        Assert.Null(AgtypeReader.ParseScalar("null"));
    }

    [Fact(DisplayName = "ParseScalar throws ResultMaterializationException on malformed JSON")]
    public void ParseScalarThrowsOnMalformedJson()
    {
        Assert.Throws<ResultMaterializationException>(() => AgtypeReader.ParseScalar("not-json"));
    }

    [Fact(DisplayName = "ParseEntity reads a vertex literal with label and properties")]
    public void ParseEntityReadsVertex()
    {
        const string text = "{\"id\": 844424930131969, \"label\": \"Author\", \"properties\": {\"Name\": \"Alice\", \"Age\": 30}}::vertex";

        AgtypeEntity entity = AgtypeReader.ParseEntity(text);

        Assert.Equal("vertex", entity.Kind);
        Assert.Equal("Author", entity.Label);
        Assert.Equal("Alice", entity.Properties["Name"]);
        Assert.Equal(30L, entity.Properties["Age"]);
    }

    [Fact(DisplayName = "ParseEntity reads an edge literal with label and properties; start_id / end_id are ignored")]
    public void ParseEntityReadsEdge()
    {
        const string text = "{\"id\": 1407374883553281, \"label\": \"WROTE\", \"end_id\": 1125899906842625, \"start_id\": 844424930131969, \"properties\": {\"WrittenAt\": \"2026-01-01\"}}::edge";

        AgtypeEntity entity = AgtypeReader.ParseEntity(text);

        Assert.Equal("edge", entity.Kind);
        Assert.Equal("WROTE", entity.Label);
        Assert.Equal("2026-01-01", entity.Properties["WrittenAt"]);
        Assert.False(entity.Properties.ContainsKey("start_id"));
        Assert.False(entity.Properties.ContainsKey("end_id"));
    }

    [Fact(DisplayName = "ParseEntity reads an empty properties bag")]
    public void ParseEntityReadsEmptyProperties()
    {
        const string text = "{\"id\": 1, \"label\": \"Marker\", \"properties\": {}}::vertex";

        AgtypeEntity entity = AgtypeReader.ParseEntity(text);

        Assert.Empty(entity.Properties);
    }

    [Fact(DisplayName = "ParseEntity throws ResultMaterializationException when the kind tag is missing")]
    public void ParseEntityThrowsWhenKindTagMissing()
    {
        const string text = "{\"id\": 1, \"label\": \"Author\", \"properties\": {}}";

        Assert.Throws<ResultMaterializationException>(() => AgtypeReader.ParseEntity(text));
    }

    [Fact(DisplayName = "ParseEntity throws ResultMaterializationException for kind tags other than vertex or edge")]
    public void ParseEntityThrowsForUnsupportedKind()
    {
        const string text = "[]::path";

        Assert.Throws<ResultMaterializationException>(() => AgtypeReader.ParseEntity(text));
    }

    [Fact(DisplayName = "ParseEntity throws ResultMaterializationException when the body is malformed JSON")]
    public void ParseEntityThrowsOnMalformedBody()
    {
        const string text = "{not json}::vertex";

        Assert.Throws<ResultMaterializationException>(() => AgtypeReader.ParseEntity(text));
    }

    [Fact(DisplayName = "ParseEntity throws ResultMaterializationException when a property value is a nested object (not scalar)")]
    public void ParseEntityThrowsForNestedObjectProperty()
    {
        const string text = "{\"id\": 1, \"label\": \"Author\", \"properties\": {\"Address\": {\"City\": \"London\"}}}::vertex";

        Assert.Throws<ResultMaterializationException>(() => AgtypeReader.ParseEntity(text));
    }
}
