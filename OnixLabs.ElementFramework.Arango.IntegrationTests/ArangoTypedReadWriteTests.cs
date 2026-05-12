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
/// End-to-end coverage of the typed read/write surface: <c>EmitAdd</c> + <c>EmitFindById</c> + <c>EmitExists</c> + <c>EmitUpdate</c> + <c>EmitRemove</c> + <c>EmitConnect</c> / <c>EmitDisconnect</c> against a real ArangoDB container. Bypasses the conformance suite for now (collection bootstrap lands in a follow-up task) by pre-creating collections by hand against the .NET client.
/// </summary>
public sealed class ArangoTypedReadWriteTests(ArangoFixture fixture) : IClassFixture<ArangoFixture>, IAsyncLifetime
{
    private const string Database = ArangoFixture.SystemDatabase;

    public async Task InitializeAsync()
    {
        using HttpApiTransport transport = HttpApiTransport.UsingBasicAuth(fixture.Endpoint, Database, fixture.Username, fixture.Password);
        using ArangoDBClient client = new(transport);

        // Document collections for each node label.
        foreach (string label in new[] { "Author", "Post", "Comment" })
            await EnsureCollectionAsync(client, label, CollectionType.Document);

        // Edge collections for each relationship label (defaults to CLR type name when not overridden).
        foreach (string label in new[] { "Wrote", "CommentOn", "ReplyTo" })
            await EnsureCollectionAsync(client, label, CollectionType.Edge);

        // Truncate everything so each test class run starts clean.
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

    [Fact(DisplayName = "TrackAdd + SaveChanges + FindById round-trips a node with all its properties")]
    public async Task TrackAddSaveAndFind()
    {
        await using ServiceProvider provider = CreateProvider();
        using IServiceScope scope = provider.CreateScope();
        BlogGraphContext context = scope.ServiceProvider.GetRequiredService<BlogGraphContext>();

        Author original = new()
        {
            Id = Guid.NewGuid(),
            Name = "Ada Lovelace",
            JoinedAt = new DateTimeOffset(1815, 12, 10, 0, 0, 0, TimeSpan.Zero),
            Bio = "First programmer"
        };
        context.Nodes<Author>().Add(original);
        await context.SaveChangesAsync();

        Author? loaded = context.Nodes<Author>().FindById(original.Id);

        Assert.NotNull(loaded);
        Assert.Equal(original.Id, loaded.Id);
        Assert.Equal(original.Name, loaded.Name);
        Assert.Equal(original.JoinedAt, loaded.JoinedAt);
        Assert.Equal(original.Bio, loaded.Bio);
    }

    [Fact(DisplayName = "Exists reports presence and absence accurately")]
    public async Task ExistsReports()
    {
        await using ServiceProvider provider = CreateProvider();
        using IServiceScope scope = provider.CreateScope();
        BlogGraphContext context = scope.ServiceProvider.GetRequiredService<BlogGraphContext>();

        Author author = Author.Create("Grace Hopper");
        context.Nodes<Author>().Add(author);
        await context.SaveChangesAsync();

        Assert.True(context.Nodes<Author>().Exists(author.Id));
        Assert.False(context.Nodes<Author>().Exists(Guid.NewGuid()));
    }

    [Fact(DisplayName = "Update rewrites mutable properties without changing the key")]
    public async Task UpdateRewritesProperties()
    {
        await using ServiceProvider provider = CreateProvider();
        using IServiceScope scope = provider.CreateScope();
        BlogGraphContext context = scope.ServiceProvider.GetRequiredService<BlogGraphContext>();

        Author author = Author.Create("Edsger Dijkstra");
        context.Nodes<Author>().Add(author);
        await context.SaveChangesAsync();

        author.Name = "Edsger W. Dijkstra";
        author.Bio = "Structured programming";
        context.Nodes<Author>().Update(author);
        await context.SaveChangesAsync();

        Author? loaded = context.Nodes<Author>().FindById(author.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Edsger W. Dijkstra", loaded.Name);
        Assert.Equal("Structured programming", loaded.Bio);
    }

    [Fact(DisplayName = "Remove deletes the document so subsequent FindById returns null")]
    public async Task RemoveDeletes()
    {
        await using ServiceProvider provider = CreateProvider();
        using IServiceScope scope = provider.CreateScope();
        BlogGraphContext context = scope.ServiceProvider.GetRequiredService<BlogGraphContext>();

        Author author = Author.Create("Alan Turing");
        context.Nodes<Author>().Add(author);
        await context.SaveChangesAsync();

        context.Nodes<Author>().Remove(author);
        await context.SaveChangesAsync();

        Assert.Null(context.Nodes<Author>().FindById(author.Id));
        Assert.False(context.Nodes<Author>().Exists(author.Id));
    }

    [Fact(DisplayName = "Connect + Disconnect round-trips an edge between two nodes")]
    public async Task ConnectDisconnectRoundTrips()
    {
        await using ServiceProvider provider = CreateProvider();
        using IServiceScope scope = provider.CreateScope();
        BlogGraphContext context = scope.ServiceProvider.GetRequiredService<BlogGraphContext>();

        Author author = Author.Create("Donald Knuth");
        Post post = new() { Id = Guid.NewGuid(), Title = "TAOCP", Body = "...", PublishedAt = DateTimeOffset.UtcNow };
        context.Nodes<Author>().Add(author);
        context.Nodes<Post>().Add(post);
        context.Edges<Wrote>().Connect(author, Wrote.Now(), post);
        await context.SaveChangesAsync();

        Assert.Single(context.Edges<Wrote>().AsEnumerable());

        context.Edges<Wrote>().Disconnect(author, post);
        await context.SaveChangesAsync();

        Assert.Empty(context.Edges<Wrote>().AsEnumerable());
    }
}
