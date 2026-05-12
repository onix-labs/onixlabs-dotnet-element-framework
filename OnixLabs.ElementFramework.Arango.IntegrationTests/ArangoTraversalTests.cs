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

using ArangoDBNetStandard;
using ArangoDBNetStandard.CollectionApi.Models;
using ArangoDBNetStandard.Transport.Http;
using Microsoft.Extensions.DependencyInjection;
using OnixLabs.ElementFramework.Conformance.TestFixtures.BlogApplication;

namespace OnixLabs.ElementFramework.Arango.IntegrationTests;

/// <summary>
/// End-to-end coverage of the traversal translator — the architectural validation we set out to prove with the Arango provider. The framework's Cypher-shaped <see cref="TraversalAst"/> (linear segment list) is translated into AQL's <c>FOR ... IN ... OUTBOUND</c> nested-loop graph traversal, executed, and materialized.
/// </summary>
public sealed class ArangoTraversalTests(ArangoFixture fixture) : IClassFixture<ArangoFixture>, IAsyncLifetime
{
    private const string Database = ArangoFixture.SystemDatabase;

    public async Task InitializeAsync()
    {
        using HttpApiTransport transport = HttpApiTransport.UsingBasicAuth(fixture.Endpoint, Database, fixture.Username, fixture.Password);
        using ArangoDBClient client = new(transport);

        foreach (string label in new[] { "Author", "Post", "Comment" })
            await EnsureCollectionAsync(client, label, CollectionType.Document);
        foreach (string label in new[] { "Wrote", "CommentOn", "ReplyTo" })
            await EnsureCollectionAsync(client, label, CollectionType.Edge);
        foreach (string label in new[] { "Author", "Post", "Comment", "Wrote", "CommentOn", "ReplyTo" })
            await client.Collection.TruncateCollectionAsync(label);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task EnsureCollectionAsync(ArangoDBClient client, string name, CollectionType type)
    {
        try
        {
            await client.Collection.GetCollectionAsync(name);
        }
        catch
        {
            await client.Collection.PostCollectionAsync(new PostCollectionBody { Name = name, Type = type });
        }
    }

    private ServiceProvider CreateProvider()
    {
        ServiceCollection services = new();
        services.AddGraphContext<BlogGraphContext>(builder =>
            builder.UseArango(() => fixture.Endpoint, Database, fixture.Username, fixture.Password));
        return services.BuildServiceProvider();
    }

    [Fact(DisplayName = "Single-segment node Match returns all nodes")]
    public async Task SingleSegmentNodeMatch()
    {
        await using ServiceProvider provider = CreateProvider();
        using IServiceScope scope = provider.CreateScope();
        BlogGraphContext context = scope.ServiceProvider.GetRequiredService<BlogGraphContext>();

        Author ada = Author.Create("Ada");
        Author grace = Author.Create("Grace");
        context.Nodes<Author>().Add(ada);
        context.Nodes<Author>().Add(grace);
        await context.SaveChangesAsync();

        Author[] authors = [.. context.Traversal.Match().Node<Author>("a").Return<Author>("a")];
        Assert.Equal(2, authors.Length);
        Assert.Contains(authors, author => author.Name == "Ada");
        Assert.Contains(authors, author => author.Name == "Grace");
    }

    [Fact(DisplayName = "Two-segment outgoing Match returns the connected end-node")]
    public async Task TwoSegmentOutgoingMatch()
    {
        await using ServiceProvider provider = CreateProvider();
        using IServiceScope scope = provider.CreateScope();
        BlogGraphContext context = scope.ServiceProvider.GetRequiredService<BlogGraphContext>();

        Author donald = Author.Create("Donald");
        Post taocp = new() { Id = Guid.NewGuid(), Title = "TAOCP", Body = "vol 1", PublishedAt = DateTimeOffset.UtcNow };
        Post concrete = new() { Id = Guid.NewGuid(), Title = "Concrete Math", Body = "...", PublishedAt = DateTimeOffset.UtcNow };
        context.Nodes<Author>().Add(donald);
        context.Nodes<Post>().Add(taocp);
        context.Nodes<Post>().Add(concrete);
        context.Edges<Wrote>().Connect(donald, Wrote.Now(), taocp);
        context.Edges<Wrote>().Connect(donald, Wrote.Now(), concrete);
        await context.SaveChangesAsync();

        Post[] posts = [.. context.Traversal.Match()
            .Node<Author>("a").Where(a => a.Name == "Donald")
            .RelatedBy<Wrote, Post>("w").Outgoing().To("p")
            .Return<Post>("p")];

        Assert.Equal(2, posts.Length);
        Assert.Contains(posts, p => p.Title == "TAOCP");
        Assert.Contains(posts, p => p.Title == "Concrete Math");
    }

    [Fact(DisplayName = "Traversal filters with composed predicates (StartsWith)")]
    public async Task TraversalWithComposedPredicates()
    {
        await using ServiceProvider provider = CreateProvider();
        using IServiceScope scope = provider.CreateScope();
        BlogGraphContext context = scope.ServiceProvider.GetRequiredService<BlogGraphContext>();

        Author donald = Author.Create("Donald");
        Post taocp = new() { Id = Guid.NewGuid(), Title = "TAOCP", Body = "v1", PublishedAt = DateTimeOffset.UtcNow };
        Post concrete = new() { Id = Guid.NewGuid(), Title = "Concrete Math", Body = "...", PublishedAt = DateTimeOffset.UtcNow };
        context.Nodes<Author>().Add(donald);
        context.Nodes<Post>().Add(taocp);
        context.Nodes<Post>().Add(concrete);
        context.Edges<Wrote>().Connect(donald, Wrote.Now(), taocp);
        context.Edges<Wrote>().Connect(donald, Wrote.Now(), concrete);
        await context.SaveChangesAsync();

        Post[] posts = [.. context.Traversal.Match()
            .Node<Author>("a").Where(a => a.Name == "Donald")
            .RelatedBy<Wrote, Post>("w").Outgoing().To("p")
            .Where(p => p.Title.StartsWith("T"))
            .Return<Post>("p")];

        Assert.Single(posts);
        Assert.Equal("TAOCP", posts[0].Title);
    }

    [Fact(DisplayName = "Traversal OrderBy + Take returns the top-N nodes in sorted order")]
    public async Task TraversalOrderByTake()
    {
        await using ServiceProvider provider = CreateProvider();
        using IServiceScope scope = provider.CreateScope();
        BlogGraphContext context = scope.ServiceProvider.GetRequiredService<BlogGraphContext>();

        for (int i = 0; i < 5; i++)
            context.Nodes<Author>().Add(new Author { Id = Guid.NewGuid(), Name = $"Author{i}", JoinedAt = DateTimeOffset.UtcNow });
        await context.SaveChangesAsync();

        Author[] authors = [.. context.Traversal.Match()
            .Node<Author>("a").OrderBy(a => a.Name).Take(2)
            .Return<Author>("a")];

        Assert.Equal(2, authors.Length);
        Assert.Equal("Author0", authors[0].Name);
        Assert.Equal("Author1", authors[1].Name);
    }

    [Fact(DisplayName = "Traversal returning an edge alias hydrates the edge document")]
    public async Task TraversalReturningEdge()
    {
        await using ServiceProvider provider = CreateProvider();
        using IServiceScope scope = provider.CreateScope();
        BlogGraphContext context = scope.ServiceProvider.GetRequiredService<BlogGraphContext>();

        Author donald = Author.Create("Donald");
        Post post = new() { Id = Guid.NewGuid(), Title = "X", Body = "...", PublishedAt = DateTimeOffset.UtcNow };
        context.Nodes<Author>().Add(donald);
        context.Nodes<Post>().Add(post);
        context.Edges<Wrote>().Connect(donald, Wrote.Now(), post);
        await context.SaveChangesAsync();

        Wrote[] edges = [.. context.Traversal.Match()
            .Node<Author>("a")
            .RelatedBy<Wrote, Post>("w").Outgoing().To("p")
            .Return<Wrote>("w")];

        Assert.Single(edges);
    }

    [Fact(DisplayName = "Incoming traversal walks the edge in reverse direction")]
    public async Task IncomingTraversal()
    {
        await using ServiceProvider provider = CreateProvider();
        using IServiceScope scope = provider.CreateScope();
        BlogGraphContext context = scope.ServiceProvider.GetRequiredService<BlogGraphContext>();

        Author author = Author.Create("Donald");
        Post post = new() { Id = Guid.NewGuid(), Title = "X", Body = "...", PublishedAt = DateTimeOffset.UtcNow };
        context.Nodes<Author>().Add(author);
        context.Nodes<Post>().Add(post);
        context.Edges<Wrote>().Connect(author, Wrote.Now(), post);
        await context.SaveChangesAsync();

        // Start at the post; walk INBOUND through Wrote to recover the author.
        Author[] authors = [.. context.Traversal.Match()
            .Node<Post>("p").Where(p => p.Title == "X")
            .RelatedBy<Wrote, Author>("w").Incoming().To("a")
            .Return<Author>("a")];

        Assert.Single(authors);
        Assert.Equal("Donald", authors[0].Name);
    }
}
