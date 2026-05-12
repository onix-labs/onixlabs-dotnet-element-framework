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

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OnixLabs.ElementFramework.Conformance.TestFixtures.BlogApplication;
using Xunit.Abstractions;

namespace OnixLabs.ElementFramework.Conformance;

public abstract class AbstractGraphContextIntegrationTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private BlogGraphContext Context => Scope.GetRequiredService<BlogGraphContext>();

    /// <summary>
    /// Wipes the shared graph before each test so that tests in the same class do not bleed state into each other.
    /// </summary>
    /// <returns>Returns a task that completes once the graph has been reset.</returns>
    protected override Task OnInitializeAsync() => ResetGraphAsync();

    /// <summary>
    /// Resets the underlying graph store to an empty state. The default implementation executes the Cypher
    /// statement <c>MATCH (n) DETACH DELETE n</c> via <see cref="IRawStatementExecutor"/>; non-Cypher providers
    /// should override this to use a provider-native reset.
    /// </summary>
    /// <returns>Returns a task that completes once the graph has been reset.</returns>
    protected virtual Task ResetGraphAsync()
    {
        _ = Context.RawStatement.Execute("MATCH (n) DETACH DELETE n", new Dictionary<string, object?>());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the provider's <see cref="ActivitySource"/> name, or <see langword="null"/> when the provider does not emit its own spans. Used by the diagnostics conformance test to assert that provider-level spans surface alongside the framework's; the default of <see langword="null"/> matches the in-memory provider, which does not instrument.
    /// </summary>
    protected virtual string? ProviderSourceName => null;

    /// <summary>
    /// Gets the operation name of a provider-level execute span the conformance test expects to see when the provider source is set. Defaults to the convention used by both shipping Cypher providers (<c>{ProviderSourceName-suffix}.ExecuteStatement</c>); overrides are rarely needed.
    /// </summary>
    protected virtual string ProviderExecuteSpanName => ProviderSourceName switch
    {
        "OnixLabs.ElementFramework.Neo4j" => "Neo4j.ExecuteStatement",
        "OnixLabs.ElementFramework.AGE" => "AGE.ExecuteStatement",
        "OnixLabs.ElementFramework.Arango" => "Arango.ExecuteStatement",
        _ => string.Empty
    };

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

    [Fact(DisplayName = "Traversal Match with OrderBy should yield nodes in ascending order of the keyed property")]
    public void TraversalOrderByAscending()
    {
        Author charlie = Author.Create("Charlie");
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Context.Nodes<Author>().Add(charlie);
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .OrderBy(a => a.Name)
            .Return<Author>("a")];

        Assert.Equal(3, materialized.Length);
        Assert.Equal("Alice", materialized[0].Name);
        Assert.Equal("Bob", materialized[1].Name);
        Assert.Equal("Charlie", materialized[2].Name);
    }

    [Fact(DisplayName = "Traversal Match with OrderByDescending should yield nodes in descending order")]
    public void TraversalOrderByDescending()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Author charlie = Author.Create("Charlie");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.Nodes<Author>().Add(charlie);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .OrderByDescending(a => a.Name)
            .Return<Author>("a")];

        Assert.Equal("Charlie", materialized[0].Name);
        Assert.Equal("Bob", materialized[1].Name);
        Assert.Equal("Alice", materialized[2].Name);
    }

    [Fact(DisplayName = "Traversal Match with Skip should drop the leading rows")]
    public void TraversalSkip()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Author charlie = Author.Create("Charlie");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.Nodes<Author>().Add(charlie);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .OrderBy(a => a.Name)
            .Skip(1)
            .Return<Author>("a")];

        Assert.Equal(2, materialized.Length);
        Assert.Equal("Bob", materialized[0].Name);
        Assert.Equal("Charlie", materialized[1].Name);
    }

    [Fact(DisplayName = "Traversal Match with Take should cap the result count")]
    public void TraversalTake()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Author charlie = Author.Create("Charlie");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.Nodes<Author>().Add(charlie);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .OrderBy(a => a.Name)
            .Take(2)
            .Return<Author>("a")];

        Assert.Equal(2, materialized.Length);
        Assert.Equal("Alice", materialized[0].Name);
        Assert.Equal("Bob", materialized[1].Name);
    }

    [Fact(DisplayName = "Traversal Match with OrderBy + Skip + Take returns a paged window of the ordered set")]
    public void TraversalOrderByWithSkipAndTake()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Author charlie = Author.Create("Charlie");
        Author dora = Author.Create("Dora");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.Nodes<Author>().Add(charlie);
        Context.Nodes<Author>().Add(dora);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .OrderBy(a => a.Name)
            .Skip(1)
            .Take(2)
            .Return<Author>("a")];

        Assert.Equal(2, materialized.Length);
        Assert.Equal("Bob", materialized[0].Name);
        Assert.Equal("Charlie", materialized[1].Name);
    }

    [Fact(DisplayName = "Explicit BeginTransaction + SaveChanges + Commit emits the framework span hierarchy (plus provider spans when applicable)")]
    public async Task DiagnosticsSpansAreEmittedAcrossTheTransactionLifecycle()
    {
        // The auto-flush path inside ChangeTracker bypasses the instrumented GraphTransactionFactory
        // by design (so the framework can preserve pending operations across an auto-flush rollback),
        // so this test drives an explicit BeginTransaction / SaveChanges / Commit cycle to assert
        // every framework span fires once at its expected point.
        List<Activity> captured = [];
        HashSet<string> sources = ["OnixLabs.ElementFramework"];
        if (ProviderSourceName is not null) sources.Add(ProviderSourceName);

        using ActivityListener listener = new()
        {
            ShouldListenTo = source => sources.Contains(source.Name),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => captured.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        await using (IGraphTransaction transaction = await Context.BeginTransactionAsync())
        {
            Context.Nodes<Author>().Add(Author.Create("Alice"));
            await Context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        Assert.Contains(captured, a => a.OperationName == "ElementFramework.BeginTransaction" && a.Status == ActivityStatusCode.Ok);
        Assert.Contains(captured, a => a.OperationName == "ElementFramework.SaveChanges" && a.Status == ActivityStatusCode.Ok);
        Assert.Contains(captured, a => a.OperationName == "ElementFramework.Transaction.Commit" && a.Status == ActivityStatusCode.Ok);

        if (ProviderSourceName is not null)
        {
            Assert.Contains(captured, a => a.OperationName == ProviderExecuteSpanName && a.Status == ActivityStatusCode.Ok);
        }
    }

    [Fact(DisplayName = "ReturnAsync releases provider resources when the enumerator is disposed mid-stream")]
    public async Task ReturnAsyncReleasesResourcesOnEarlyBreak()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Author charlie = Author.Create("Charlie");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.Nodes<Author>().Add(charlie);
        await Context.SaveChangesAsync();

        // Exit after the first row — the executor must dispose its cursor / reader / session
        // (or connection) so a second async query against the same context completes successfully.
        // Under eager materialization this is trivially true. Under lazy streaming, this is a
        // genuine resource-management assertion: a broken implementation would either leak the
        // resource (eventually exhausting the pool) or hold the connection past the enumerator's
        // disposal (causing the second call to block or fail).
        await foreach (Author author in Context.Traversal
            .Match()
            .Node<Author>("a")
            .OrderBy(a => a.Name)
            .ReturnAsync<Author>("a"))
        {
            Assert.Equal("Alice", author.Name);
            break;
        }

        List<Author> drained = [];
        await foreach (Author author in Context.Traversal
            .Match()
            .Node<Author>("a")
            .OrderBy(a => a.Name)
            .ReturnAsync<Author>("a"))
        {
            drained.Add(author);
        }

        Assert.Equal(3, drained.Count);
        Assert.Equal("Alice", drained[0].Name);
        Assert.Equal("Bob", drained[1].Name);
        Assert.Equal("Charlie", drained[2].Name);
    }

    [Fact(DisplayName = "RawStatement Execute should return result rows")]
    public virtual void RawStatementExecuteShouldReturnResultRows()
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

    [Fact(DisplayName = "Traversal Match with Where '!=' should yield non-matching nodes")]
    public void TraversalMatchWhereNotEqualShouldYieldNonMatching()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .Where(a => a.Name != "Alice")
            .Return<Author>("a")];

        Assert.Single(materialized);
        Assert.Equal("Bob", materialized[0].Name);
    }

    [Fact(DisplayName = "Traversal Match with Where ordered comparison ('>=') on a date should filter accordingly")]
    public void TraversalMatchWhereOrderedComparisonShouldFilter()
    {
        DateTimeOffset cutoff = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Author early = new() { Id = Guid.NewGuid(), Name = "Early", JoinedAt = cutoff.AddDays(-1) };
        Author late = new() { Id = Guid.NewGuid(), Name = "Late", JoinedAt = cutoff.AddDays(1) };
        Context.Nodes<Author>().Add(early);
        Context.Nodes<Author>().Add(late);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .Where(a => a.JoinedAt >= cutoff)
            .Return<Author>("a")];

        Assert.Single(materialized);
        Assert.Equal("Late", materialized[0].Name);
    }

    [Fact(DisplayName = "Traversal Match with Where '||' should yield rows matching either branch")]
    public void TraversalMatchWhereOrShouldYieldEitherBranch()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Author carol = Author.Create("Carol");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.Nodes<Author>().Add(carol);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .Where(a => a.Name == "Alice" || a.Name == "Bob")
            .Return<Author>("a")];

        string[] names = [.. materialized.Select(a => a.Name).OrderBy(name => name)];
        Assert.Equal(["Alice", "Bob"], names);
    }

    [Fact(DisplayName = "Traversal Match with Where '&&' across two properties should yield rows matching both branches")]
    public void TraversalMatchWhereAndShouldYieldBothBranches()
    {
        DateTimeOffset cutoff = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Author aliceEarly = new() { Id = Guid.NewGuid(), Name = "Alice", JoinedAt = cutoff.AddDays(-1) };
        Author aliceLate = new() { Id = Guid.NewGuid(), Name = "Alice", JoinedAt = cutoff.AddDays(1) };
        Author bobLate = new() { Id = Guid.NewGuid(), Name = "Bob", JoinedAt = cutoff.AddDays(1) };
        Context.Nodes<Author>().Add(aliceEarly);
        Context.Nodes<Author>().Add(aliceLate);
        Context.Nodes<Author>().Add(bobLate);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .Where(a => a.Name == "Alice" && a.JoinedAt >= cutoff)
            .Return<Author>("a")];

        Assert.Single(materialized);
        Assert.Equal(aliceLate.Id, materialized[0].Id);
    }

    [Fact(DisplayName = "Traversal Match with Where '!' should negate the inner predicate")]
    public void TraversalMatchWhereNotShouldNegateInner()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .Where(a => !(a.Name == "Alice"))
            .Return<Author>("a")];

        Assert.Single(materialized);
        Assert.Equal("Bob", materialized[0].Name);
    }

    [Fact(DisplayName = "Traversal Match with Where Contains should yield rows whose property contains the substring")]
    public void TraversalMatchWhereStringContainsShouldYieldMatching()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .Where(a => a.Name.Contains("li"))
            .Return<Author>("a")];

        Assert.Single(materialized);
        Assert.Equal("Alice", materialized[0].Name);
    }

    [Fact(DisplayName = "Traversal Match with Where StartsWith should yield rows whose property has the given prefix")]
    public void TraversalMatchWhereStringStartsWithShouldYieldMatching()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .Where(a => a.Name.StartsWith("Al"))
            .Return<Author>("a")];

        Assert.Single(materialized);
        Assert.Equal("Alice", materialized[0].Name);
    }

    [Fact(DisplayName = "Traversal Match with Where EndsWith should yield rows whose property has the given suffix")]
    public void TraversalMatchWhereStringEndsWithShouldYieldMatching()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .Where(a => a.Name.EndsWith("ce"))
            .Return<Author>("a")];

        Assert.Single(materialized);
        Assert.Equal("Alice", materialized[0].Name);
    }

    [Fact(DisplayName = "Traversal Match with Where '!= null' on a required property should yield every row")]
    public void TraversalMatchWhereNotNullShouldYieldAllRows()
    {
        Author alice = Author.Create("Alice");
        Author bob = Author.Create("Bob");
        Context.Nodes<Author>().Add(alice);
        Context.Nodes<Author>().Add(bob);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .Where(a => a.Name != null)
            .Return<Author>("a")];

        Assert.Equal(2, materialized.Length);
    }

    [Fact(DisplayName = "Traversal Match with Where '== null' on a required property should yield zero rows")]
    public void TraversalMatchWhereNullShouldYieldNoRows()
    {
        Author alice = Author.Create("Alice");
        Context.Nodes<Author>().Add(alice);
        Context.SaveChanges();

        Author[] materialized = [.. Context.Traversal
            .Match()
            .Node<Author>("a")
            .Where(a => a.Name == null)
            .Return<Author>("a")];

        Assert.Empty(materialized);
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
    public virtual async Task RawStatementExecuteAsyncShouldYieldRows()
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
    public virtual void RawStatementExecuteShouldPersistMutationVisibleToOgm()
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
    public virtual void RawStatementExecuteShouldYieldMultipleRows()
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
