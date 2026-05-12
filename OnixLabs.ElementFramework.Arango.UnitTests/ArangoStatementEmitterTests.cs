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

public class ArangoStatementEmitterTests
{
    private static readonly GraphModel Model = TestModel.Build();
    private static readonly ArangoStatementEmitter Emitter = new();

    private static Author SampleAuthor() => new()
    {
        Id = Guid.Parse("0c2c8a8a-4d6b-4c2d-9d1f-1b9d6a5d3e2a"),
        Name = "Ada Lovelace",
        JoinedAt = new DateTimeOffset(2026, 5, 11, 12, 0, 0, TimeSpan.Zero)
    };

    [Fact(DisplayName = "EmitAdd produces an INSERT statement with the full document including _key")]
    public void EmitAddProducesInsertStatement()
    {
        DataStatement statement = Emitter.EmitAdd(Model, SampleAuthor());

        Assert.Equal("INSERT @doc INTO @@col", statement.Statement);
        Assert.Equal("Author", statement.Parameters["@col"]);

        IReadOnlyDictionary<string, object?> doc = Assert.IsType<Dictionary<string, object?>>(statement.Parameters["doc"]);
        Assert.Equal("0c2c8a8a-4d6b-4c2d-9d1f-1b9d6a5d3e2a", doc["_key"]);
        Assert.Equal("0c2c8a8a-4d6b-4c2d-9d1f-1b9d6a5d3e2a", doc["Id"]);
        Assert.Equal("Ada Lovelace", doc["Name"]);
    }

    [Fact(DisplayName = "EmitUpdate produces an UPDATE statement scoped by _key without rewriting the key property")]
    public void EmitUpdateOmitsKeyProperty()
    {
        DataStatement statement = Emitter.EmitUpdate(Model, SampleAuthor());

        Assert.Equal("UPDATE @key WITH @doc IN @@col", statement.Statement);
        Assert.Equal("0c2c8a8a-4d6b-4c2d-9d1f-1b9d6a5d3e2a", statement.Parameters["key"]);
        Assert.Equal("Author", statement.Parameters["@col"]);

        IReadOnlyDictionary<string, object?> doc = Assert.IsType<Dictionary<string, object?>>(statement.Parameters["doc"]);
        Assert.False(doc.ContainsKey("_key"));
        Assert.False(doc.ContainsKey("Id"));
        Assert.Equal("Ada Lovelace", doc["Name"]);
    }

    [Fact(DisplayName = "EmitRemove produces a REMOVE statement with just the key")]
    public void EmitRemoveStatement()
    {
        DataStatement statement = Emitter.EmitRemove(Model, SampleAuthor());

        Assert.Equal("REMOVE @key IN @@col", statement.Statement);
        Assert.Equal("0c2c8a8a-4d6b-4c2d-9d1f-1b9d6a5d3e2a", statement.Parameters["key"]);
        Assert.Equal("Author", statement.Parameters["@col"]);
    }

    [Fact(DisplayName = "EmitMerge produces an UPSERT that inserts on miss and updates on match")]
    public void EmitMergeProducesUpsertStatement()
    {
        DataStatement statement = Emitter.EmitMerge(Model, SampleAuthor());

        Assert.Equal("UPSERT { _key: @key } INSERT @insert UPDATE @update IN @@col", statement.Statement);
        Assert.Equal("0c2c8a8a-4d6b-4c2d-9d1f-1b9d6a5d3e2a", statement.Parameters["key"]);
        Assert.True(((Dictionary<string, object?>)statement.Parameters["insert"]!).ContainsKey("_key"));
        Assert.False(((Dictionary<string, object?>)statement.Parameters["update"]!).ContainsKey("_key"));
    }

    [Fact(DisplayName = "EmitFindById produces a single-row read keyed by _key")]
    public void EmitFindByIdStatement()
    {
        Guid id = Guid.NewGuid();

        DataStatement statement = Emitter.EmitFindById<Author>(Model, id);

        Assert.Equal("FOR n IN @@col FILTER n._key == @key LIMIT 1 RETURN { n: n }", statement.Statement);
        Assert.Equal(id.ToString(), statement.Parameters["key"]);
        Assert.Equal("Author", statement.Parameters["@col"]);
    }

    [Fact(DisplayName = "EmitExists produces a LENGTH(...)-shaped existence check returning under the 'cnt' alias")]
    public void EmitExistsStatement()
    {
        Guid id = Guid.NewGuid();

        DataStatement statement = Emitter.EmitExists<Author>(Model, id);

        Assert.Contains("LENGTH(FOR n IN @@col FILTER n._key == @key LIMIT 1 RETURN 1)", statement.Statement);
        Assert.Contains("{ cnt:", statement.Statement);
        Assert.Equal(id.ToString(), statement.Parameters["key"]);
    }

    [Fact(DisplayName = "EmitAsEnumerableNodes produces a FOR ... RETURN { n: n }")]
    public void EmitAsEnumerableNodesStatement()
    {
        DataStatement statement = Emitter.EmitAsEnumerableNodes<Author>(Model);

        Assert.Equal("FOR n IN @@col RETURN { n: n }", statement.Statement);
        Assert.Equal("Author", statement.Parameters["@col"]);
    }

    [Fact(DisplayName = "EmitAsEnumerableEdges produces a FOR ... RETURN { r: r }")]
    public void EmitAsEnumerableEdgesStatement()
    {
        DataStatement statement = Emitter.EmitAsEnumerableEdges<Wrote>(Model);

        Assert.Equal("FOR r IN @@edge RETURN { r: r }", statement.Statement);
        Assert.Equal("Wrote", statement.Parameters["@edge"]);
    }

    [Fact(DisplayName = "EmitConnect produces an INSERT into the edge collection with _from / _to document IDs")]
    public void EmitConnectStatement()
    {
        Author author = SampleAuthor();
        Post post = new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Title = "X", Body = "Y", PublishedAt = DateTimeOffset.UtcNow };
        Wrote wrote = new() { WrittenAt = DateTimeOffset.UtcNow };

        DataStatement statement = Emitter.EmitConnect(Model, author, wrote, post);

        Assert.Equal("INSERT @doc INTO @@edge", statement.Statement);
        Assert.Equal("Wrote", statement.Parameters["@edge"]);
        IReadOnlyDictionary<string, object?> doc = Assert.IsType<Dictionary<string, object?>>(statement.Parameters["doc"]);
        Assert.Equal($"Author/{author.Id}", doc["_from"]);
        Assert.Equal($"Post/{post.Id}", doc["_to"]);
    }

    [Fact(DisplayName = "EmitDisconnect produces a FOR ... FILTER ... REMOVE for the matching endpoint pair")]
    public void EmitDisconnectStatement()
    {
        Author author = SampleAuthor();
        Post post = new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Title = "X", Body = "Y", PublishedAt = DateTimeOffset.UtcNow };

        DataStatement statement = Emitter.EmitDisconnect<Author, Wrote, Post>(Model, author, post);

        Assert.Equal(
            "FOR e IN @@edge FILTER e._from == @from AND e._to == @to REMOVE e IN @@edge",
            statement.Statement);
        Assert.Equal($"Author/{author.Id}", statement.Parameters["from"]);
        Assert.Equal($"Post/{post.Id}", statement.Parameters["to"]);
    }
}
