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

using System.Globalization;

namespace OnixLabs.ElementFramework.AGE.UnitTests;

public class AgeResultMaterializerTests
{
    private readonly GraphModel model = TestModel.Build();
    private readonly AgeResultMaterializer materializer = new();

    [Fact(DisplayName = "MaterializeNode parses an agtype vertex string at the conventional 'n' alias")]
    public void MaterializeNodeParsesVertexAtConventionalAlias()
    {
        Guid id = Guid.NewGuid();
        string vertex = $"{{\"id\": 1, \"label\": \"TypedNode\", \"properties\": {{\"Id\": \"{id}\", \"Name\": \"Alice\"}}}}::vertex";
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["n"] = vertex };

        TypedNode result = materializer.MaterializeNode<TypedNode>(model, row);

        Assert.Equal(id, result.Id);
        Assert.Equal("Alice", result.Name);
    }

    [Fact(DisplayName = "MaterializeNodeAt reads the entity at the supplied alias")]
    public void MaterializeNodeAtReadsSuppliedAlias()
    {
        Guid id = Guid.NewGuid();
        string vertex = $"{{\"id\": 1, \"label\": \"TypedNode\", \"properties\": {{\"Id\": \"{id}\", \"Name\": \"Alice\"}}}}::vertex";
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["a"] = vertex };

        TypedNode result = materializer.MaterializeNodeAt<TypedNode>(model, row, "a");

        Assert.Equal(id, result.Id);
        Assert.Equal("Alice", result.Name);
    }

    [Fact(DisplayName = "MaterializeNode converts an integer agtype scalar (parsed as long) to a CLR int via ChangeType")]
    public void MaterializeNodeConvertsLongToInt()
    {
        string vertex = $"{{\"id\": 1, \"label\": \"TypedNode\", \"properties\": {{\"Id\": \"{Guid.NewGuid()}\", \"Age\": 30}}}}::vertex";
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["n"] = vertex };

        TypedNode result = materializer.MaterializeNode<TypedNode>(model, row);

        Assert.Equal(30, result.Age);
    }

    [Fact(DisplayName = "MaterializeNode parses an ISO-8601 string back into a DateTimeOffset preserving the offset")]
    public void MaterializeNodeParsesDateTimeOffsetRoundTrip()
    {
        DateTimeOffset created = new(2026, 5, 11, 13, 30, 0, TimeSpan.FromHours(2));
        string roundTrip = created.ToString("o", CultureInfo.InvariantCulture);
        string vertex = $"{{\"id\": 1, \"label\": \"TypedNode\", \"properties\": {{\"Id\": \"{Guid.NewGuid()}\", \"CreatedAt\": \"{roundTrip}\"}}}}::vertex";
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["n"] = vertex };

        TypedNode result = materializer.MaterializeNode<TypedNode>(model, row);

        Assert.Equal(created, result.CreatedAt);
    }

    [Fact(DisplayName = "MaterializeNode parses an ISO-8601 string back into a DateTime preserving Kind")]
    public void MaterializeNodeParsesDateTimeRoundTrip()
    {
        DateTime utc = new(2026, 5, 11, 13, 30, 0, DateTimeKind.Utc);
        string roundTrip = utc.ToString("o", CultureInfo.InvariantCulture);
        string vertex = $"{{\"id\": 1, \"label\": \"TypedNode\", \"properties\": {{\"Id\": \"{Guid.NewGuid()}\", \"CreatedAtLocal\": \"{roundTrip}\"}}}}::vertex";
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["n"] = vertex };

        TypedNode result = materializer.MaterializeNode<TypedNode>(model, row);

        Assert.Equal(utc, result.CreatedAtLocal);
    }

    [Fact(DisplayName = "MaterializeNode parses an enum member by name")]
    public void MaterializeNodeParsesEnumByName()
    {
        string vertex = $"{{\"id\": 1, \"label\": \"TypedNode\", \"properties\": {{\"Id\": \"{Guid.NewGuid()}\", \"Status\": \"Submitted\"}}}}::vertex";
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["n"] = vertex };

        TypedNode result = materializer.MaterializeNode<TypedNode>(model, row);

        Assert.Equal(SampleStatus.Submitted, result.Status);
    }

    [Fact(DisplayName = "MaterializeNode parses a nullable Guid from a hyphenated string")]
    public void MaterializeNodeConvertsNullableGuid()
    {
        Guid optional = Guid.NewGuid();
        string vertex = $"{{\"id\": 1, \"label\": \"TypedNode\", \"properties\": {{\"Id\": \"{Guid.NewGuid()}\", \"OptionalId\": \"{optional}\"}}}}::vertex";
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["n"] = vertex };

        TypedNode result = materializer.MaterializeNode<TypedNode>(model, row);

        Assert.Equal(optional, result.OptionalId);
    }

    [Fact(DisplayName = "MaterializeNode passes primitive bool / double / long through unchanged")]
    public void MaterializeNodePassesPrimitivesThrough()
    {
        string vertex = $"{{\"id\": 1, \"label\": \"TypedNode\", \"properties\": {{\"Id\": \"{Guid.NewGuid()}\", \"IsActive\": true, \"Score\": 3.14}}}}::vertex";
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["n"] = vertex };

        TypedNode result = materializer.MaterializeNode<TypedNode>(model, row);

        Assert.True(result.IsActive);
        Assert.Equal(3.14, result.Score, 6);
    }

    [Fact(DisplayName = "MaterializeNode leaves properties at their default when absent from the agtype payload")]
    public void MaterializeNodeLeavesMissingPropertiesAtDefault()
    {
        string vertex = $"{{\"id\": 1, \"label\": \"TypedNode\", \"properties\": {{\"Id\": \"{Guid.NewGuid()}\"}}}}::vertex";
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["n"] = vertex };

        TypedNode result = materializer.MaterializeNode<TypedNode>(model, row);

        Assert.Equal("", result.Name);
        Assert.Equal(0, result.Age);
        Assert.Equal(default, result.CreatedAt);
        Assert.Null(result.OptionalId);
    }

    [Fact(DisplayName = "MaterializeNode throws ResultMaterializationException when the row value is not a string")]
    public void MaterializeNodeThrowsWhenRowValueIsNotString()
    {
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["n"] = 42 };

        Assert.Throws<ResultMaterializationException>(() => materializer.MaterializeNode<TypedNode>(model, row));
    }

    [Fact(DisplayName = "MaterializeNode throws ResultMaterializationException when the alias is missing from the row")]
    public void MaterializeNodeThrowsWhenAliasMissing()
    {
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["x"] = "ignored" };

        Assert.Throws<ResultMaterializationException>(() => materializer.MaterializeNode<TypedNode>(model, row));
    }

    [Fact(DisplayName = "MaterializeNode throws ResultMaterializationException when the row carries an edge but a vertex is expected")]
    public void MaterializeNodeThrowsForEdgeUnderNodeAlias()
    {
        string edge = "{\"id\": 1, \"label\": \"Wrote\", \"end_id\": 2, \"start_id\": 3, \"properties\": {}}::edge";
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["n"] = edge };

        Assert.Throws<ResultMaterializationException>(() => materializer.MaterializeNode<TypedNode>(model, row));
    }

    [Fact(DisplayName = "MaterializeEdge reads the relationship at the conventional 'r' alias")]
    public void MaterializeEdgeReadsConventionalAlias()
    {
        DateTimeOffset writtenAt = new(2026, 5, 11, 13, 0, 0, TimeSpan.Zero);
        string roundTrip = writtenAt.ToString("o", CultureInfo.InvariantCulture);
        string edge = $"{{\"id\": 1, \"label\": \"Wrote\", \"end_id\": 2, \"start_id\": 3, \"properties\": {{\"WrittenAt\": \"{roundTrip}\"}}}}::edge";
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["r"] = edge };

        Wrote result = materializer.MaterializeEdge<Wrote>(model, row);

        Assert.Equal(writtenAt, result.WrittenAt);
    }

    [Fact(DisplayName = "MaterializeEdgeAt reads the relationship at the supplied alias")]
    public void MaterializeEdgeAtReadsSuppliedAlias()
    {
        DateTimeOffset writtenAt = new(2026, 5, 11, 13, 0, 0, TimeSpan.Zero);
        string roundTrip = writtenAt.ToString("o", CultureInfo.InvariantCulture);
        string edge = $"{{\"id\": 1, \"label\": \"Wrote\", \"end_id\": 2, \"start_id\": 3, \"properties\": {{\"WrittenAt\": \"{roundTrip}\"}}}}::edge";
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["w"] = edge };

        Wrote result = materializer.MaterializeEdgeAt<Wrote>(model, row, "w");

        Assert.Equal(writtenAt, result.WrittenAt);
    }

    [Fact(DisplayName = "MaterializeEdge throws ResultMaterializationException when the row carries a vertex but an edge is expected")]
    public void MaterializeEdgeThrowsForVertexUnderEdgeAlias()
    {
        string vertex = "{\"id\": 1, \"label\": \"Author\", \"properties\": {}}::vertex";
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["r"] = vertex };

        Assert.Throws<ResultMaterializationException>(() => materializer.MaterializeEdge<Wrote>(model, row));
    }

    [Theory(DisplayName = "ReadExists returns true when the 'cnt' alias carries a positive integer agtype")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("9999999999")]
    public void ReadExistsReturnsTrueForPositiveCount(string countText)
    {
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["cnt"] = countText };
        Assert.True(materializer.ReadExists(row));
    }

    [Theory(DisplayName = "ReadExists returns false when the 'cnt' alias is zero or negative")]
    [InlineData("0")]
    [InlineData("-1")]
    public void ReadExistsReturnsFalseForZeroOrNegativeCount(string countText)
    {
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["cnt"] = countText };
        Assert.False(materializer.ReadExists(row));
    }

    [Fact(DisplayName = "ReadExists returns false when the 'cnt' alias parses to a non-long (e.g. fractional)")]
    public void ReadExistsReturnsFalseForFractionalCount()
    {
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["cnt"] = "3.14" };
        Assert.False(materializer.ReadExists(row));
    }

    [Fact(DisplayName = "ReadExists throws ResultMaterializationException when the 'cnt' alias is missing")]
    public void ReadExistsThrowsWhenCntAliasMissing()
    {
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?>();
        Assert.Throws<ResultMaterializationException>(() => materializer.ReadExists(row));
    }

    [Fact(DisplayName = "ReadExists throws ResultMaterializationException when the 'cnt' value is not a text-encoded agtype")]
    public void ReadExistsThrowsWhenCntValueIsNotString()
    {
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["cnt"] = 1L };
        Assert.Throws<ResultMaterializationException>(() => materializer.ReadExists(row));
    }
}
