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

namespace OnixLabs.ElementFramework.Arango.UnitTests;

public class ArangoResultMaterializerTests
{
    private static readonly GraphModel Model = TestModel.Build();
    private static readonly ArangoResultMaterializer Materializer = new();

    private static IReadOnlyDictionary<string, object?> NodeRow(Dictionary<string, object?> inner) =>
        new Dictionary<string, object?> { [ArangoResultMaterializer.NodeAlias] = inner };

    private static IReadOnlyDictionary<string, object?> EdgeRow(Dictionary<string, object?> inner) =>
        new Dictionary<string, object?> { [ArangoResultMaterializer.EdgeAlias] = inner };

    [Fact(DisplayName = "MaterializeNode converts an ISO-8601 string into the target DateTimeOffset preserving the offset")]
    public void MaterializeNodeConvertsIsoStringToDateTimeOffset()
    {
        Guid id = Guid.NewGuid();
        TypedNode node = Materializer.MaterializeNode<TypedNode>(Model, NodeRow(new()
        {
            ["Id"] = id.ToString(),
            ["CreatedAt"] = "2026-05-11T13:30:00.0000000+02:00"
        }));

        Assert.Equal(id, node.Id);
        Assert.Equal(new DateTimeOffset(2026, 5, 11, 13, 30, 0, TimeSpan.FromHours(2)), node.CreatedAt);
    }

    [Fact(DisplayName = "MaterializeNode converts an enum member name back into its enum value")]
    public void MaterializeNodeConvertsEnumString()
    {
        TypedNode node = Materializer.MaterializeNode<TypedNode>(Model, NodeRow(new()
        {
            ["Id"] = Guid.NewGuid().ToString(),
            ["Status"] = "Shipped"
        }));

        Assert.Equal(SampleStatus.Shipped, node.Status);
    }

    [Fact(DisplayName = "MaterializeNode widens a long to an int property")]
    public void MaterializeNodeWidensLongToInt()
    {
        TypedNode node = Materializer.MaterializeNode<TypedNode>(Model, NodeRow(new()
        {
            ["Id"] = Guid.NewGuid().ToString(),
            ["Age"] = 42L
        }));

        Assert.Equal(42, node.Age);
    }

    [Fact(DisplayName = "MaterializeNode leaves a missing optional property at its CLR default")]
    public void MaterializeNodeIgnoresMissingProperties()
    {
        TypedNode node = Materializer.MaterializeNode<TypedNode>(Model, NodeRow(new()
        {
            ["Id"] = Guid.NewGuid().ToString()
        }));

        Assert.Equal(default, node.CreatedAt);
        Assert.Null(node.OptionalId);
    }

    [Fact(DisplayName = "MaterializeNodeAt reads the document under a consumer-supplied alias")]
    public void MaterializeNodeAtUsesCustomAlias()
    {
        Guid id = Guid.NewGuid();
        Dictionary<string, object?> row = new()
        {
            ["custom"] = new Dictionary<string, object?>
            {
                ["Id"] = id.ToString(),
                ["Name"] = "Ada"
            }
        };

        TypedNode node = Materializer.MaterializeNodeAt<TypedNode>(Model, row, "custom");

        Assert.Equal(id, node.Id);
        Assert.Equal("Ada", node.Name);
    }

    [Fact(DisplayName = "MaterializeEdge reads the document under the conventional edge alias")]
    public void MaterializeEdgeUsesConventionalAlias()
    {
        Wrote wrote = Materializer.MaterializeEdge<Wrote>(Model, EdgeRow(new()
        {
            ["WrittenAt"] = "2026-05-11T13:30:00.0000000+00:00"
        }));

        Assert.Equal(new DateTimeOffset(2026, 5, 11, 13, 30, 0, TimeSpan.Zero), wrote.WrittenAt);
    }

    [Fact(DisplayName = "ReadExists returns true when the cnt alias carries a positive count")]
    public void ReadExistsTrueOnPositiveCount()
    {
        bool exists = Materializer.ReadExists(new Dictionary<string, object?>
        {
            [ArangoResultMaterializer.CountAlias] = 1L
        });

        Assert.True(exists);
    }

    [Fact(DisplayName = "ReadExists returns false when the cnt alias carries zero")]
    public void ReadExistsFalseOnZero()
    {
        bool exists = Materializer.ReadExists(new Dictionary<string, object?>
        {
            [ArangoResultMaterializer.CountAlias] = 0L
        });

        Assert.False(exists);
    }

    [Fact(DisplayName = "ReadExists throws when the cnt alias is missing")]
    public void ReadExistsThrowsWhenAliasMissing()
    {
        Assert.Throws<ResultMaterializationException>(() => Materializer.ReadExists(new Dictionary<string, object?>()));
    }

    [Fact(DisplayName = "MaterializeNode throws when the node alias points at a non-object value")]
    public void MaterializeNodeThrowsOnNonObject()
    {
        Dictionary<string, object?> row = new()
        {
            [ArangoResultMaterializer.NodeAlias] = "not-an-object"
        };

        Assert.Throws<ResultMaterializationException>(() => Materializer.MaterializeNode<Author>(Model, row));
    }
}
