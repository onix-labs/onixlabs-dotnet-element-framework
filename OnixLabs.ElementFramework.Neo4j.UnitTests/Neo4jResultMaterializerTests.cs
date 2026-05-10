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

using Neo4j.Driver;

namespace OnixLabs.ElementFramework.Neo4j.UnitTests;

public class Neo4jResultMaterializerTests
{
    private readonly GraphModel model = TestModel.Build();
    private readonly Neo4jResultMaterializer materializer = new();

    [Fact(DisplayName = "MaterializeNode converts a Bolt-stored Guid string back into a CLR Guid")]
    public void MaterializeNodeConvertsGuidString()
    {
        Guid id = Guid.NewGuid();
        IReadOnlyDictionary<string, object?> row = NodeRow(new() { ["Id"] = id.ToString(), ["Name"] = "Alice" });

        TypedNode result = materializer.MaterializeNode<TypedNode>(model, row);

        Assert.Equal(id, result.Id);
        Assert.Equal("Alice", result.Name);
    }

    [Fact(DisplayName = "MaterializeNode converts a Bolt ZonedDateTime into a CLR DateTimeOffset preserving the offset")]
    public void MaterializeNodeConvertsZonedDateTime()
    {
        DateTimeOffset created = new(2026, 5, 11, 13, 30, 0, TimeSpan.FromHours(2));
        IReadOnlyDictionary<string, object?> row = NodeRow(new()
        {
            ["Id"] = Guid.NewGuid().ToString(),
            ["CreatedAt"] = new ZonedDateTime(created)
        });

        TypedNode result = materializer.MaterializeNode<TypedNode>(model, row);

        Assert.Equal(created, result.CreatedAt);
    }

    [Fact(DisplayName = "MaterializeNode parses an enum member by name")]
    public void MaterializeNodeParsesEnumByName()
    {
        IReadOnlyDictionary<string, object?> row = NodeRow(new()
        {
            ["Id"] = Guid.NewGuid().ToString(),
            ["Status"] = nameof(SampleStatus.Submitted)
        });

        TypedNode result = materializer.MaterializeNode<TypedNode>(model, row);

        Assert.Equal(SampleStatus.Submitted, result.Status);
    }

    [Fact(DisplayName = "MaterializeNode passes through primitive int and bool values")]
    public void MaterializeNodePassesThroughPrimitives()
    {
        IReadOnlyDictionary<string, object?> row = NodeRow(new()
        {
            ["Id"] = Guid.NewGuid().ToString(),
            ["Age"] = 30,
            ["IsActive"] = true
        });

        TypedNode result = materializer.MaterializeNode<TypedNode>(model, row);

        Assert.Equal(30, result.Age);
        Assert.True(result.IsActive);
    }

    [Fact(DisplayName = "MaterializeNode converts a long-typed Bolt value into a CLR int via ChangeType")]
    public void MaterializeNodeConvertsLongToInt()
    {
        IReadOnlyDictionary<string, object?> row = NodeRow(new()
        {
            ["Id"] = Guid.NewGuid().ToString(),
            ["Age"] = 30L
        });

        TypedNode result = materializer.MaterializeNode<TypedNode>(model, row);

        Assert.Equal(30, result.Age);
    }

    [Fact(DisplayName = "MaterializeNode converts a string-typed Bolt value into a nullable Guid")]
    public void MaterializeNodeConvertsNullableGuid()
    {
        Guid optional = Guid.NewGuid();
        IReadOnlyDictionary<string, object?> row = NodeRow(new()
        {
            ["Id"] = Guid.NewGuid().ToString(),
            ["OptionalId"] = optional.ToString()
        });

        TypedNode result = materializer.MaterializeNode<TypedNode>(model, row);

        Assert.Equal(optional, result.OptionalId);
    }

    [Fact(DisplayName = "MaterializeNode leaves properties at their default when absent from the Bolt entity")]
    public void MaterializeNodeLeavesMissingPropertiesAtDefault()
    {
        IReadOnlyDictionary<string, object?> row = NodeRow(new()
        {
            ["Id"] = Guid.NewGuid().ToString()
        });

        TypedNode result = materializer.MaterializeNode<TypedNode>(model, row);

        Assert.Equal("", result.Name);
        Assert.Equal(0, result.Age);
        Assert.Equal(default, result.CreatedAt);
        Assert.Null(result.OptionalId);
    }

    [Fact(DisplayName = "MaterializeNode throws ResultMaterializationException when the row alias does not carry an INode")]
    public void MaterializeNodeThrowsWhenNotAnINode()
    {
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["n"] = "not-an-INode" };
        Assert.Throws<ResultMaterializationException>(() => materializer.MaterializeNode<TypedNode>(model, row));
    }

    [Fact(DisplayName = "MaterializeNodeAt reads the entity at the supplied alias")]
    public void MaterializeNodeAtReadsSuppliedAlias()
    {
        Guid id = Guid.NewGuid();
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?>
        {
            ["a"] = new FakeNode(new Dictionary<string, object> { ["Id"] = id.ToString(), ["Name"] = "Alice" })
        };

        TypedNode result = materializer.MaterializeNodeAt<TypedNode>(model, row, "a");

        Assert.Equal(id, result.Id);
        Assert.Equal("Alice", result.Name);
    }

    [Fact(DisplayName = "MaterializeEdge reads relationship properties at the conventional 'r' alias")]
    public void MaterializeEdgeReadsRelationship()
    {
        DateTimeOffset writtenAt = new(2026, 5, 11, 13, 0, 0, TimeSpan.Zero);
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?>
        {
            ["r"] = new FakeRelationship(new Dictionary<string, object> { ["WrittenAt"] = new ZonedDateTime(writtenAt) })
        };

        Wrote result = materializer.MaterializeEdge<Wrote>(model, row);

        Assert.Equal(writtenAt, result.WrittenAt);
    }

    [Fact(DisplayName = "MaterializeEdge throws ResultMaterializationException when the row alias does not carry an IRelationship")]
    public void MaterializeEdgeThrowsWhenNotAnIRelationship()
    {
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["r"] = 42 };
        Assert.Throws<ResultMaterializationException>(() => materializer.MaterializeEdge<Wrote>(model, row));
    }

    [Fact(DisplayName = "MaterializeEdgeAt reads the relationship at the supplied alias")]
    public void MaterializeEdgeAtReadsSuppliedAlias()
    {
        DateTimeOffset writtenAt = new(2026, 5, 11, 13, 0, 0, TimeSpan.Zero);
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?>
        {
            ["w"] = new FakeRelationship(new Dictionary<string, object> { ["WrittenAt"] = new ZonedDateTime(writtenAt) })
        };

        Wrote result = materializer.MaterializeEdgeAt<Wrote>(model, row, "w");

        Assert.Equal(writtenAt, result.WrittenAt);
    }

    [Theory(DisplayName = "ReadExists returns true when the 'count' alias is a long greater than zero")]
    [InlineData(1L)]
    [InlineData(2L)]
    [InlineData(long.MaxValue)]
    public void ReadExistsReturnsTrueForPositiveCount(long count)
    {
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["count"] = count };
        Assert.True(materializer.ReadExists(row));
    }

    [Theory(DisplayName = "ReadExists returns false when the 'count' alias is zero or negative")]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void ReadExistsReturnsFalseForZeroOrNegativeCount(long count)
    {
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["count"] = count };
        Assert.False(materializer.ReadExists(row));
    }

    [Fact(DisplayName = "ReadExists returns false when the 'count' value is not a long")]
    public void ReadExistsReturnsFalseForNonLongCount()
    {
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?> { ["count"] = 1 };
        Assert.False(materializer.ReadExists(row));
    }

    [Fact(DisplayName = "ReadExists throws ResultMaterializationException when the 'count' alias is missing")]
    public void ReadExistsThrowsWhenCountAliasMissing()
    {
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?>();
        Assert.Throws<ResultMaterializationException>(() => materializer.ReadExists(row));
    }

    private static IReadOnlyDictionary<string, object?> NodeRow(Dictionary<string, object> properties) =>
        new Dictionary<string, object?> { ["n"] = new FakeNode(properties) };

    private sealed class FakeNode(IReadOnlyDictionary<string, object> properties) : INode
    {
        public IReadOnlyDictionary<string, object> Properties { get; } = properties;
        public IReadOnlyList<string> Labels => throw new NotSupportedException();
        public long Id => throw new NotSupportedException();
        public string ElementId => throw new NotSupportedException();
        public object this[string key] => Properties[key];
        public T Get<T>(string key) => (T)Properties[key];
        public bool TryGet<T>(string key, out T value)
        {
            if (Properties.TryGetValue(key, out object? raw) && raw is T typed) { value = typed; return true; }
            value = default!;
            return false;
        }
        public bool Equals(INode? other) => ReferenceEquals(this, other);
        public bool Equals(IEntity? other) => ReferenceEquals(this, other);
    }

    private sealed class FakeRelationship(IReadOnlyDictionary<string, object> properties) : IRelationship
    {
        public IReadOnlyDictionary<string, object> Properties { get; } = properties;
        public string Type => throw new NotSupportedException();
        public long StartNodeId => throw new NotSupportedException();
        public long EndNodeId => throw new NotSupportedException();
        public string StartNodeElementId => throw new NotSupportedException();
        public string EndNodeElementId => throw new NotSupportedException();
        public long Id => throw new NotSupportedException();
        public string ElementId => throw new NotSupportedException();
        public object this[string key] => Properties[key];
        public T Get<T>(string key) => (T)Properties[key];
        public bool TryGet<T>(string key, out T value)
        {
            if (Properties.TryGetValue(key, out object? raw) && raw is T typed) { value = typed; return true; }
            value = default!;
            return false;
        }
        public bool Equals(IRelationship? other) => ReferenceEquals(this, other);
        public bool Equals(IEntity? other) => ReferenceEquals(this, other);
    }
}
