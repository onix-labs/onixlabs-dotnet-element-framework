// All Rights Reserved License
//
// 1. Grant of License
// Subject to the terms and conditions of this License, ONIXLabs ("Licensor") hereby grants to you a limited, non-exclusive, non-transferable, non-sublicensable license to use the Software for commercial, private, and paid purposes. This license does not include any rights to modify, distribute, or create derivative works of the Software.
//
// 2. Permitted Uses
// You are permitted to:
//  - Use the Software for commercial purposes.
//  - Use the Software for private purposes.
//  - Use the Software for paid purposes.
//  - Exercise any patent rights associated with the Software, solely in connection with your use of the Software as permitted under this License.
//
// 3. Restrictions
// You are not permitted to:
//  - Modify, alter, or create any derivative works of the Software.
//  - Distribute, sublicense, lease, rent, or otherwise transfer the Software to any third party.
//  - Use the Software without obtaining a proper license for paid use.
//  - Use the Software in any way that infringes upon the trademarks, service marks, or trade names of the Licensor.
//  - Use the Software in any manner that could cause it to be considered open-source software or otherwise subject to an open-source license.
//
// 4. No Free Use
// This license does not permit any free use of the Software. Any use of the Software without a paid license is strictly prohibited.
//
// 5. No Liability
// To the maximum extent permitted by applicable law, the Software is provided "as is" and "as available" without warranty of any kind, express or implied, including but not limited to the implied warranties of merchantability, fitness for a particular purpose, and non-infringement. In no event shall the Licensor be liable for any damages whatsoever arising out of the use of or inability to use the Software, even if the Licensor has been advised of the possibility of such damages.
//
// 6. No Warranty
// The Licensor makes no warranty that the Software will meet your requirements, be uninterrupted, secure, or error-free. The Licensor disclaims all warranties with respect to the Software, whether express or implied, including but not limited to any warranties of merchantability, fitness for a particular purpose, and non-infringement.
//
// 7. Termination
// This license is effective until terminated. Your rights under this license will terminate automatically without notice if you fail to comply with any term of this license. Upon termination, you must immediately cease all use of the Software and destroy all copies of the Software in your possession or control.
//
// 8. Governing Law
// This license will be governed by and construed in accordance with the laws of [Your Jurisdiction], without regard to its conflict of laws principles.
//
// 9. Entire Agreement
// This license constitutes the entire agreement between you and the Licensor concerning the Software and supersedes all prior or contemporaneous communications, agreements, or understandings, whether oral or written, concerning the subject matter hereof.
//
// By using the Software, you acknowledge that you have read and understood this license and agree to be bound by its terms and conditions.

using Microsoft.Extensions.DependencyInjection;
using OnixLabs.ElementFramework.Neo4j.IntegrationTests.TestFixtures.BlogApplication;
using Xunit.Abstractions;

namespace OnixLabs.ElementFramework.Neo4j.IntegrationTests;

public abstract class AbstractGraphContextIntegrationTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private BlogGraphContext Context => Scope.GetRequiredService<BlogGraphContext>();

    [Fact(DisplayName = "Nodes.Add followed by SaveChanges should add a node")]
    public void NodesAddShouldAddANode()
    {
        Author author = Author.Create("Alice");

        Context.Nodes<Author>().Add(author);
        int saved = Context.SaveChanges();

        Assert.Equal(1, saved);
    }

    [Fact(DisplayName = "Nodes.AddAsync followed by SaveChangesAsync should add a node")]
    public async Task NodesAddAsyncShouldAddANode()
    {
        Author author = Author.Create("Alice");

        await Context.Nodes<Author>().AddAsync(author);
        int saved = await Context.SaveChangesAsync();

        Assert.Equal(1, saved);
    }

    [Fact(DisplayName = "Nodes.FindById should return the node when one with the specified ID exists")]
    public void NodesFindByIdShouldReturnTheNodeWhenOneExists()
    {
        Author author = Author.Create("Alice");
        Context.Nodes<Author>().Add(author);
        Context.SaveChanges();

        Author? found = Context.Nodes<Author>().FindById(author.Id);

        Assert.NotNull(found);
        Assert.Equal(author.Id, found.Id);
    }

    [Fact(DisplayName = "Nodes.FindById should return null when no node with the specified ID exists")]
    public void NodesFindByIdShouldReturnNullWhenNoneExists()
    {
        Guid unknownId = Guid.NewGuid();

        Author? found = Context.Nodes<Author>().FindById(unknownId);

        Assert.Null(found);
    }

    [Fact(DisplayName = "Nodes.Exists should return true when a node with the specified ID exists")]
    public void NodesExistsShouldReturnTrueWhenNodeExists()
    {
        Author author = Author.Create("Alice");
        Context.Nodes<Author>().Add(author);
        Context.SaveChanges();

        bool exists = Context.Nodes<Author>().Exists(author.Id);

        Assert.True(exists);
    }

    [Fact(DisplayName = "Nodes.Exists should return false when no node with the specified ID exists")]
    public void NodesExistsShouldReturnFalseWhenNoneExists()
    {
        Guid unknownId = Guid.NewGuid();

        bool exists = Context.Nodes<Author>().Exists(unknownId);

        Assert.False(exists);
    }

    [Fact(DisplayName = "Nodes.Update followed by SaveChanges should update a tracked node")]
    public void NodesUpdateShouldUpdateATrackedNode()
    {
        Author author = Author.Create("Alice");
        Context.Nodes<Author>().Add(author);
        Context.SaveChanges();

        author.Name = "Alice Liddell";
        Context.Nodes<Author>().Update(author);
        int saved = Context.SaveChanges();

        Assert.Equal(1, saved);
    }

    [Fact(DisplayName = "Nodes.Remove followed by SaveChanges should remove a node")]
    public void NodesRemoveShouldRemoveANode()
    {
        Author author = Author.Create("Alice");
        Context.Nodes<Author>().Add(author);
        Context.SaveChanges();

        Context.Nodes<Author>().Remove(author);
        int saved = Context.SaveChanges();

        Assert.Equal(1, saved);
    }

    [Fact(DisplayName = "Nodes.Merge should add the node when none exists with its key")]
    public void NodesMergeShouldAddWhenNew()
    {
        Author author = Author.Create("Alice");

        Context.Nodes<Author>().Merge(author);
        int saved = Context.SaveChanges();

        Assert.Equal(1, saved);
    }

    [Fact(DisplayName = "Nodes.AsEnumerable should yield previously added nodes")]
    public void NodesAsEnumerableShouldYieldAddedNodes()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Nodes<Author>().AsEnumerable()];

        Assert.Equal(2, materialized.Length);
        Assert.Contains(materialized, author => author.Id == alice.Id);
        Assert.Contains(materialized, author => author.Id == bob.Id);
    }

    [Fact(DisplayName = "Edges.Connect followed by SaveChanges should add an edge between two nodes")]
    public void EdgesConnectShouldAddAnEdge()
    {
        Author author = Author.Create("Alice");
        Post post = Post.Create("Hello", "First post.");
        Context.Nodes<Author>().Add(author);
        Context.Nodes<Post>().Add(post);
        Context.SaveChanges();

        Context.Edges<Wrote>().Connect(author, Wrote.Now(), post);
        int saved = Context.SaveChanges();

        Assert.Equal(1, saved);
    }

    [Fact(DisplayName = "Edges.ConnectAsync followed by SaveChangesAsync should add an edge between two nodes")]
    public async Task EdgesConnectAsyncShouldAddAnEdge()
    {
        Author author = Author.Create("Alice");
        Post post = Post.Create("Hello", "First post.");
        await Context.Nodes<Author>().AddAsync(author);
        await Context.Nodes<Post>().AddAsync(post);
        await Context.SaveChangesAsync();

        await Context.Edges<Wrote>().ConnectAsync(author, Wrote.Now(), post);
        int saved = await Context.SaveChangesAsync();

        Assert.Equal(1, saved);
    }

    [Fact(DisplayName = "Edges.Connect should add an edge with no payload (marker edge)")]
    public void EdgesConnectMarkerEdgeShouldAddAnEdge()
    {
        Comment comment = Comment.Create("Nice post.");
        Post post = Post.Create("Hello", "First post.");
        Context.Nodes<Comment>().Add(comment);
        Context.Nodes<Post>().Add(post);
        Context.SaveChanges();

        Context.Edges<CommentOn>().Connect(comment, new CommentOn(), post);
        int saved = Context.SaveChanges();

        Assert.Equal(1, saved);
    }

    [Fact(DisplayName = "Edges.Connect should add a reflexive edge between two nodes of the same type")]
    public void EdgesConnectReflexiveEdgeShouldAddAnEdge()
    {
        Comment original = Comment.Create("Nice post.");
        Comment reply = Comment.Create("Thanks!");
        Context.Nodes<Comment>().Add(original);
        Context.Nodes<Comment>().Add(reply);
        Context.SaveChanges();

        Context.Edges<ReplyTo>().Connect(reply, new ReplyTo(), original);
        int saved = Context.SaveChanges();

        Assert.Equal(1, saved);
    }

    [Fact(DisplayName = "Edges.Disconnect followed by SaveChanges should remove all edges of the type between two nodes")]
    public void EdgesDisconnectShouldRemoveTheEdge()
    {
        Author author = Author.Create("Alice");
        Post post = Post.Create("Hello", "First post.");
        Context.Nodes<Author>().Add(author);
        Context.Nodes<Post>().Add(post);
        Context.Edges<Wrote>().Connect(author, Wrote.Now(), post);
        Context.SaveChanges();

        Context.Edges<Wrote>().Disconnect(author, post);
        int saved = Context.SaveChanges();

        Assert.Equal(1, saved);
    }

    [Fact(DisplayName = "Edges.AsEnumerable should yield previously connected edges")]
    public void EdgesAsEnumerableShouldYieldConnectedEdges()
    {
        Author author = Author.Create("Alice");
        Post first = Post.Create("First", "First body.");
        Post second = Post.Create("Second", "Second body.");
        Context.Nodes<Author>().Add(author);
        Context.Nodes<Post>().Add(first);
        Context.Nodes<Post>().Add(second);
        Context.Edges<Wrote>().Connect(author, Wrote.Now(), first);
        Context.Edges<Wrote>().Connect(author, Wrote.Now(), second);
        Context.SaveChanges();

        Wrote[] materialized = [.. Context.Edges<Wrote>().AsEnumerable()];

        Assert.Equal(2, materialized.Length);
    }

    [Fact(DisplayName = "BeginTransaction should return a transaction that can be committed")]
    public void BeginTransactionShouldReturnACommittableTransaction()
    {
        Author author = Author.Create("Alice");

        using IGraphTransaction transaction = Context.BeginTransaction();
        Context.Nodes<Author>().Add(author);
        Context.SaveChanges();
        transaction.Commit();

        Assert.NotNull(Context.Nodes<Author>().FindById(author.Id));
    }

    [Fact(DisplayName = "BeginTransaction should return a transaction that can be rolled back")]
    public void BeginTransactionShouldReturnARollbackableTransaction()
    {
        Author author = Author.Create("Alice");

        using (IGraphTransaction transaction = Context.BeginTransaction())
        {
            Context.Nodes<Author>().Add(author);
            Context.SaveChanges();
            transaction.Rollback();
        }

        Assert.Null(Context.Nodes<Author>().FindById(author.Id));
    }

    [Fact(DisplayName = "Traversal Match should yield matching nodes")]
    public void TraversalMatchShouldYieldMatchingNodes()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal.Match().Node<Author>("a").Return<Author>("a")];

        Assert.Equal(2, materialized.Length);
    }

    [Fact(DisplayName = "RawStatement Execute should return result rows")]
    public void RawStatementExecuteShouldReturnResultRows()
    {
        Author alice = Author.Create("Alice");
        Context.Nodes<Author>().Add(alice);
        Context.SaveChanges();

        IReadOnlyDictionary<string, object?>[] rows = [.. Context.RawStatement.Execute(
            "MATCH (a:Author { Id: $id }) RETURN a.Name AS name",
            new Dictionary<string, object?> { ["id"] = alice.Id })];

        Assert.Single(rows);
        Assert.Equal("Alice", rows[0]["name"]);
    }

    [Fact(DisplayName = "Traversal Match with a 1-hop Outgoing relationship should yield the destination nodes")]
    public void TraversalMatch1HopOutgoingShouldReturnDestination()
    {
        Author alice = Author.Create("Alice");
        Post post = Post.Create("Hello", "First post.");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Post>().Add(post);
        Context.Edges<Wrote>().Connect(alice, Wrote.Now(), post);
        Context.SaveChanges();

        Post[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .RelatedBy<Wrote, Post>("w")
            .Outgoing()
            .To("p")
            .Return<Post>("p")];

        Assert.Single(materialized);
        Assert.Equal(post.Id, materialized[0].Id);
    }

    [Fact(DisplayName = "Traversal Match with a 1-hop Incoming relationship should yield the source nodes")]
    public void TraversalMatch1HopIncomingShouldReturnSource()
    {
        Author alice = Author.Create("Alice");
        Post post = Post.Create("Hello", "First post.");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Post>().Add(post);
        Context.Edges<Wrote>().Connect(alice, Wrote.Now(), post);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Post>("p")
            .RelatedBy<Wrote, Author>("w")
            .Incoming()
            .To("a")
            .Return<Author>("a")];

        Assert.Single(materialized);
        Assert.Equal(alice.Id, materialized[0].Id);
    }

    [Fact(DisplayName = "Traversal Match with an Either-direction relationship should yield connected nodes regardless of direction")]
    public void TraversalMatch1HopEitherShouldReturnConnectedRegardlessOfDirection()
    {
        Author alice = Author.Create("Alice");
        Post post = Post.Create("Hello", "First post.");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Post>().Add(post);
        Context.Edges<Wrote>().Connect(alice, Wrote.Now(), post);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Post>("p")
            .RelatedBy<Wrote, Author>("w")
            .Either()
            .To("a")
            .Return<Author>("a")];

        Assert.Single(materialized);
        Assert.Equal(alice.Id, materialized[0].Id);
    }

    [Fact(DisplayName = "Traversal Match with two hops across three node types should yield the terminal node")]
    public void TraversalMatch2HopsAcrossThreeNodeTypes()
    {
        Author alice = Author.Create("Alice");
        Post post = Post.Create("Hello", "First post.");
        Comment comment = Comment.Create("Nice post.");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Post>().Add(post);
        Context.Nodes<Comment>().Add(comment);
        Context.Edges<Wrote>().Connect(alice, Wrote.Now(), post);
        Context.Edges<CommentOn>().Connect(comment, new CommentOn(), post);
        Context.SaveChanges();

        Comment[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .RelatedBy<Wrote, Post>("w")
            .Outgoing()
            .To("p")
            .RelatedBy<CommentOn, Comment>("co")
            .Incoming()
            .To("c")
            .Return<Comment>("c")];

        Assert.Single(materialized);
        Assert.Equal(comment.Id, materialized[0].Id);
    }

    [Fact(DisplayName = "Traversal Match across a reflexive relationship should follow ReplyTo Comment->Comment")]
    public void TraversalMatchReflexiveRelationshipShouldFollowReplyTo()
    {
        Comment original = Comment.Create("Nice post.");
        Comment reply = Comment.Create("Thanks!");
        Context.Nodes<Comment>().Add(original);
        Context.Nodes<Comment>().Add(reply);
        Context.Edges<ReplyTo>().Connect(reply, new ReplyTo(), original);
        Context.SaveChanges();

        Comment[] materialized = [.. Context.Traversal
            .Match()
            .Node<Comment>("c1")
            .RelatedBy<ReplyTo, Comment>("r")
            .Outgoing()
            .To("c2")
            .Return<Comment>("c2")];

        Assert.Single(materialized);
        Assert.Equal(original.Id, materialized[0].Id);
    }

    [Fact(DisplayName = "Traversal Match with a Where predicate should filter by the bound property")]
    public void TraversalMatchWherePredicateShouldFilterByProperty()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .Where(a => a.Name == "Alice")
            .Return<Author>("a")];

        Assert.Single(materialized);
        Assert.Equal(alice.Id, materialized[0].Id);
        Assert.Equal("Alice", materialized[0].Name);
    }

    [Fact(DisplayName = "Traversal Match returning the edge alias should materialize edge properties")]
    public void TraversalMatchWithEdgeReturnShouldMaterializeEdgeProperties()
    {
        Author alice = Author.Create("Alice");
        Post post = Post.Create("Hello", "First post.");
        DateTimeOffset writtenAt = DateTimeOffset.UtcNow;
        Wrote wrote = new() { WrittenAt = writtenAt };
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Post>().Add(post);
        Context.Edges<Wrote>().Connect(alice, wrote, post);
        Context.SaveChanges();

        Wrote[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .RelatedBy<Wrote, Post>("w")
            .Outgoing()
            .To("p")
            .Return<Wrote>("w")];

        Assert.Single(materialized);
        Assert.Equal(writtenAt.ToUnixTimeMilliseconds(), materialized[0].WrittenAt.ToUnixTimeMilliseconds());
    }

    [Fact(DisplayName = "Traversal Match with no matching nodes should return an empty enumerable")]
    public void TraversalMatchShouldReturnEmptyWhenNoNodesMatch()
    {
        Author alice = Author.Create("Alice");
        Context.Nodes<Author>().Add(alice);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .Where(a => a.Name == "Nonexistent")
            .Return<Author>("a")];

        Assert.Empty(materialized);
    }

    [Fact(DisplayName = "Traversal Match with the async terminal should yield matching nodes")]
    public async Task TraversalMatchAsyncShouldYieldMatchingNodes()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        await Context.Nodes<Author>().AddAsync(alice);
        await Context.Nodes<Author>().AddAsync(bob);
        await Context.SaveChangesAsync();

        List<Author> materialized = [];
        await foreach (Author author in Context.Traversal.Match().Node<Author>("a").ReturnAsync<Author>("a"))
            materialized.Add(author);

        Assert.Equal(2, materialized.Count);
        Assert.Contains(materialized, author => author.Id == alice.Id);
        Assert.Contains(materialized, author => author.Id == bob.Id);
    }

    [Fact(DisplayName = "Traversal Merge on a relationship pattern should be idempotent (no duplicate edge on second invocation)")]
    public void TraversalMergeRelationshipShouldCreateIfAbsentAndNotDuplicate()
    {
        Author alice = Author.Create("Alice");
        Post post = Post.Create("Hello", "First post.");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Post>().Add(post);
        Context.SaveChanges();

        IEnumerable<Wrote> firstMerge = Context.Traversal
            .Merge()
            .Node<Author>("a")
            .RelatedBy<Wrote, Post>("w")
            .Outgoing()
            .To("p")
            .Return<Wrote>("w");
        _ = firstMerge.ToList();

        IEnumerable<Wrote> secondMerge = Context.Traversal
            .Merge()
            .Node<Author>("a")
            .RelatedBy<Wrote, Post>("w")
            .Outgoing()
            .To("p")
            .Return<Wrote>("w");
        _ = secondMerge.ToList();

        Wrote[] edges = [.. Context.Edges<Wrote>().AsEnumerable()];
        Assert.Single(edges);
    }

    [Fact(DisplayName = "Traversal Create on a relationship pattern should add a new edge")]
    public void TraversalCreateRelationshipShouldAddNewEdge()
    {
        Author alice = Author.Create("Alice");
        Post post = Post.Create("Hello", "First post.");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Post>().Add(post);
        Context.SaveChanges();

        IEnumerable<Wrote> created = Context.Traversal
            .Create()
            .Node<Author>("a")
            .RelatedBy<Wrote, Post>("w")
            .Outgoing()
            .To("p")
            .Return<Wrote>("w");
        _ = created.ToList();

        Wrote[] edges = [.. Context.Edges<Wrote>().AsEnumerable()];
        Assert.Single(edges);
    }

    [Fact(DisplayName = "RawStatement.ExecuteAsync should yield result rows")]
    public async Task RawStatementExecuteAsyncShouldYieldRows()
    {
        Author alice = Author.Create("Alice");
        await Context.Nodes<Author>().AddAsync(alice);
        await Context.SaveChangesAsync();

        List<IReadOnlyDictionary<string, object?>> rows = [];
        await foreach (IReadOnlyDictionary<string, object?> row in Context.RawStatement.ExecuteAsync(
            "MATCH (a:Author { Id: $id }) RETURN a.Name AS name",
            new Dictionary<string, object?> { ["id"] = alice.Id }))
            rows.Add(row);

        Assert.Single(rows);
        Assert.Equal("Alice", rows[0]["name"]);
    }

    [Fact(DisplayName = "RawStatement.Execute writes should be visible to the OGM read APIs")]
    public void RawStatementExecuteShouldPersistMutationVisibleToOgm()
    {
        Guid id = Guid.NewGuid();
        const string name = "Caroline";

        _ = Context.RawStatement.Execute(
            "CREATE (a:Author { Id: $id, Name: $name }) RETURN a.Id AS id",
            new Dictionary<string, object?> { ["id"] = id, ["name"] = name }).ToList();

        Author? found = Context.Nodes<Author>().FindById(id);
        Assert.NotNull(found);
        Assert.Equal(id, found.Id);
        Assert.Equal(name, found.Name);
    }

    [Fact(DisplayName = "RawStatement.Execute with a multi-row return should yield every row")]
    public void RawStatementExecuteShouldYieldMultipleRows()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Author carol = Author.Create("Carol");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.Nodes<Author>().Add(carol);
        Context.SaveChanges();

        IReadOnlyDictionary<string, object?>[] rows = [.. Context.RawStatement.Execute(
            "MATCH (a:Author) RETURN a.Name AS name",
            new Dictionary<string, object?>())];

        Assert.Equal(3, rows.Length);
        string[] names = [.. rows.Select(row => (string)row["name"]!)];
        Assert.Contains("Alice", names);
        Assert.Contains("Bob", names);
        Assert.Contains("Carol", names);
    }
}
