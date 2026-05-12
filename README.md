![ONIX Labs](https://raw.githubusercontent.com/onix-labs/onixlabs-website/refs/heads/main/OnixLabs.Web/wwwroot/onixlabs/images/logo/logo-full-light.svg)

# ONIXLabs Element Framework

Element Framework is a modern Object-Graph Mapper (OGM) for .NET that brings the same ergonomics as Entity Framework to labelled-property-graph databases. Model your domain in plain CLR types, configure them fluently, and let the framework translate change tracking and graph traversals into provider-native statements.

Targets **net8.0**, **net9.0**, and **net10.0**. Ships providers for **Neo4j** (Cypher over Bolt), **Apache AGE** (Cypher-wrapped-in-SQL over Postgres), **ArangoDB** (AQL over HTTP), and an **in-memory** store for tests.

## Documentation

The deep documentation lives in the project [wiki](https://github.com/onix-labs/onixlabs-dotnet-element-framework/wiki):

- [Architecture: Abstractions](https://github.com/onix-labs/onixlabs-dotnet-element-framework/wiki/Architecture:-Abstractions) — consumer surface, provider contract, traversal AST
- [Architecture: Implementation](https://github.com/onix-labs/onixlabs-dotnet-element-framework/wiki/Architecture:-Implementation) — change tracker, transactions, diagnostics
- [Provider: Neo4j and MemGraph](https://github.com/onix-labs/onixlabs-dotnet-element-framework/wiki/Provider:-Neo4j-and-MemGraph)
- [Provider: Apache AGE](https://github.com/onix-labs/onixlabs-dotnet-element-framework/wiki/Provider:-Apache-AGE)
- [Provider: ArangoDB](https://github.com/onix-labs/onixlabs-dotnet-element-framework/wiki/Provider:-ArangoDB)
- [Provider: In-Memory](https://github.com/onix-labs/onixlabs-dotnet-element-framework/wiki/Provider:-In-Memory)
- [Example: A minimal movie graph](https://github.com/onix-labs/onixlabs-dotnet-element-framework/wiki/Example:-A-minimal-movie-graph) — complete runnable example

## Quick start

Define a node and an edge, configure them, declare a context, register with DI, and start writing:

```csharp
public sealed class Author
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
}

public sealed class Post
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
}

public sealed class Wrote
{
    public required DateTimeOffset WrittenAt { get; init; }
}

public sealed class AuthorConfiguration : INodeTypeConfiguration<Author>
{
    public void Configure(INodeBuilder<Author> builder) => builder.HasKey(a => a.Id);
}

public sealed class PostConfiguration : INodeTypeConfiguration<Post>
{
    public void Configure(INodeBuilder<Post> builder) => builder.HasKey(p => p.Id);
}

public sealed class WroteConfiguration : IRelationshipConfiguration<Author, Wrote, Post>
{
    public void Configure(IRelationshipBuilder<Author, Wrote, Post> builder) { }
}

public sealed class BlogGraphContext(GraphContextOptions options) : GraphContext(options)
{
    protected override void OnModelCreating(IGraphModelBuilder builder) => builder
        .ApplyConfiguration(new AuthorConfiguration())
        .ApplyConfiguration(new PostConfiguration())
        .ApplyConfiguration(new WroteConfiguration());
}
```

```csharp
services.AddGraphContext<BlogGraphContext>(builder =>
    builder.UseNeo4j(new Uri("bolt://localhost:7687"), AuthTokens.Basic("neo4j", "password")));
```

```csharp
Author ada = new() { Id = Guid.NewGuid(), Name = "Ada Lovelace" };
Post post = new() { Id = Guid.NewGuid(), Title = "Notes on the Analytical Engine" };
context.Nodes<Author>().Add(ada);
context.Nodes<Post>().Add(post);
context.Edges<Wrote>().Connect(ada, new Wrote { WrittenAt = DateTimeOffset.UtcNow }, post);
await context.SaveChangesAsync();

Post[] adasPosts = [.. context.Traversal.Match()
    .Node<Author>("a").Where(a => a.Id == ada.Id)
    .RelatedBy<Wrote, Post>("w").Outgoing().To("p")
    .Return<Post>("p")];
```

For a complete runnable example, see [Example: A minimal movie graph](https://github.com/onix-labs/onixlabs-dotnet-element-framework/wiki/Example:-A-minimal-movie-graph).

## Packages

| Package | Purpose |
| --- | --- |
| `OnixLabs.ElementFramework.Abstractions` | Provider contracts and the abstract `GraphContext` |
| `OnixLabs.ElementFramework` | Default implementation (change tracker, model, sets, traversal, DI) |
| `OnixLabs.ElementFramework.Neo4j` | Neo4j (Cypher over Bolt) provider |
| `OnixLabs.ElementFramework.AGE` | Apache AGE (Cypher-wrapped-in-SQL over Postgres) provider |
| `OnixLabs.ElementFramework.Arango` | ArangoDB (AQL over HTTP) provider |
| `OnixLabs.ElementFramework.InMemory` | In-process provider for tests and demos |

## Project status

Under active development. All four providers pass the same provider-agnostic conformance suite. See the project's [open issues](https://github.com/onix-labs/onixlabs-dotnet-element-framework/issues) for the current work-in-progress and roadmap items.

## License

Licensed under the [MIT License](LICENSE).
