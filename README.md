![ONIX Labs](https://raw.githubusercontent.com/onix-labs/onixlabs-website/refs/heads/main/OnixLabs.Web/wwwroot/onixlabs/images/logo/logo-full-light.svg)

# ONIXLabs Element Framework

Element Framework is a modern Object-Graph Mapper (OGM) for .NET that brings the same ergonomics as Enity Framework to object-graph databases. Model your domain in plain CLR types, configure them fluently, and let the framework translate change tracking and graph traversals into provider-native statements.

Element Framework currently supports Neo4j and MemGraph with Cypher.

Targets **net8.0**, **net9.0**, and **net10.0**.

## Features

### Code-first Graph Modelling

Model your domain in plain CLR types, using ordinary classes for nodes and ordinary classes for edges, with no required base types or attributes. Wire them into the model through tiny per-type and per-relationship configuration objects that keep mapping concerns out of your domain types and let the same model be reused across providers.

### Context and Change Tracking

Element Framework gives you the familiar context rhythm of add, update, remove, merge, connect, and disconnect, backed by a per-context identity map and an ordered queue of pending operations. Read-after-write inside a context returns the same tracked instance, and operations stay queued until you explicitly flush them, so a single unit of work composes cleanly even when it spans many touches of the graph.

### Atomic Changes

Each save is atomic. When no consumer transaction is already active, the framework opens an ambient one for the duration of the flush, commits it on full success, and rolls it back on the first failure. Pending operations are cleared only on success, so a corrected retry can replay the full batch without losing any work that was queued before the failure.

### Fluent Traversal API

Build pattern queries fluently and type-safely. The traversal builder lets you bind nodes and edges to aliases, walk relationships in any direction, and project the result into a strongly-typed shape. Predicates are written directly as C# lambda expressions and compiled into the underlying query language, giving you static typing and refactor safety across the entire pattern.

### Raw Statement Escape Hatch

When the fluent API isn't enough, drop down to provider-native query text and execute it through the context. Parameters are still bound safely through the same provider abstractions, and result rows come back as a familiar dictionary shape, so you keep parameterisation and result materialisation without giving up direct control of the query.

### Provider Abstraction

A clean seam separates your domain code from the underlying store. Element Framework defines the contracts for statement emission, transaction lifecycle, traversal translation, and result materialisation, and providers implement them. The first-class implementation today targets Cypher-speaking graph databases via the official Neo4j .NET driver; additional providers can be added by implementing the abstraction surface.

### First-class Dependency Injection Integration

Element Framework plugs straight into the standard .NET dependency-injection container. A single registration call wires up your context, the chosen provider, and all of the per-context coordination services with the correct lifetimes, so your application code only ever sees its own context type and works with it through constructor injection.

## Packages

### OnixLabs.ElementFramework.Abstractions

The provider-agnostic surface of the framework. It defines the abstract `GraphContext` that consumer applications subclass, the contracts every provider must satisfy (`IStatementEmitter`, `IRawStatementExecutor`, `IGraphTransactionOpener`, `ITraversalTranslator`, `IResultMaterializer`), and the model and configuration contracts (`IGraphModel`, `INodeBuilder<T>`, `IRelationshipBuilder<TStart, TEdge, TEnd>`, `INodeTypeConfiguration<T>`, `IRelationshipConfiguration<TStart, TEdge, TEnd>`). Reference this package directly when writing a new provider or shared abstractions that must stay implementation-free.

### OnixLabs.ElementFramework

The default, provider-agnostic implementation of everything the abstractions describe. It contains the `ChangeTracker` that owns the identity map and pending-operation queue, the `GraphModel` / `GraphModelBuilder` that freeze your fluent registrations, the `NodeSet<T>` / `EdgeSet<T>` accessors returned by `context.Nodes<T>()` and `context.Edges<T>()`, the `GraphTraversal` pipeline that builds a `TraversalAst`, and the `services.AddGraphContext<TContext>(...)` registration extension. You will normally reference this package from your application alongside a concrete provider.

### OnixLabs.ElementFramework.Neo4j

The first-class Neo4j provider, built on the official Neo4j .NET driver. It supplies `CypherEmitter`, `Neo4jTraversalTranslator`, `Neo4jResultMaterializer`, `Neo4jCypherExecutor`, and the `Neo4jGraphTransaction` / `Neo4jGraphTransactionOpener` lifecycle, alongside internal helpers for identifier escaping, parameter binding, value serialisation, and driver caching. The whole stack is wired up by the `builder.UseNeo4j(...)` extension invoked inside `services.AddGraphContext<TContext>(...)`.

## Getting started

The walkthrough below mirrors the framework's own integration-test fixture: a tiny blog domain with `Author`, `Post`, and a `Wrote` edge that connects them.

### 1. Define your domain types

Nodes and edges are ordinary classes. There are no required base types or attributes.

```csharp
public sealed class Author
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public required DateTimeOffset JoinedAt { get; init; }
}

public sealed class Post
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Body { get; set; }
    public required DateTimeOffset PublishedAt { get; init; }
}

public sealed class Wrote
{
    public required DateTimeOffset WrittenAt { get; init; }
}
```

### 2. Configure each type

Implement `INodeTypeConfiguration<T>` for each node and `IRelationshipConfiguration<TStart, TEdge, TEnd>` for each edge. The fluent builder supports `HasKey`, `HasLabel`, `Property` (for name overrides) and `Ignore`.

```csharp
public sealed class AuthorConfiguration : INodeTypeConfiguration<Author>
{
    public void Configure(INodeBuilder<Author> builder) =>
        builder.HasKey(author => author.Id);
}

public sealed class PostConfiguration : INodeTypeConfiguration<Post>
{
    public void Configure(INodeBuilder<Post> builder) =>
        builder.HasKey(post => post.Id);
}

public sealed class WroteConfiguration : IRelationshipConfiguration<Author, Wrote, Post>
{
    public void Configure(IRelationshipBuilder<Author, Wrote, Post> builder)
    {
    }
}
```

### 3. Define a `GraphContext`

Subclass `GraphContext` and apply your configurations in `OnModelCreating`.

```csharp
public sealed class BlogGraphContext(GraphContextOptions options) : GraphContext(options)
{
    protected override void OnModelCreating(IGraphModelBuilder modelBuilder) => modelBuilder
        .ApplyConfiguration(new AuthorConfiguration())
        .ApplyConfiguration(new PostConfiguration())
        .ApplyConfiguration(new WroteConfiguration());
}
```

### 4. Register with DI and pick a provider

```csharp
services.AddGraphContext<BlogGraphContext>(builder =>
    builder.UseNeo4j(() => "bolt://localhost:7687", AuthTokens.Basic("neo4j", "password")));
```

### 5. Use the context

Resolve `BlogGraphContext` from DI and you're ready to read and write the graph.

```csharp
// Add nodes
Author alice = new() { Id = Guid.NewGuid(), Name = "Alice", JoinedAt = DateTimeOffset.UtcNow };
Post hello = new() { Id = Guid.NewGuid(), Title = "Hello", Body = "First post.", PublishedAt = DateTimeOffset.UtcNow };

context.Nodes<Author>().Add(alice);
context.Nodes<Post>().Add(hello);

// Connect them with an edge
context.Edges<Wrote>().Connect(alice, new Wrote { WrittenAt = DateTimeOffset.UtcNow }, hello);

// Atomic flush
int written = context.SaveChanges();

// Look up by key
Author? found = context.Nodes<Author>().FindById(alice.Id);
bool exists = context.Nodes<Author>().Exists(alice.Id);

// Update and remove
alice.Name = "Alice Liddell";
context.Nodes<Author>().Update(alice);
context.Nodes<Post>().Remove(hello);
context.SaveChanges();
```

Every operation has an async counterpart (`AddAsync`, `SaveChangesAsync`, `FindByIdAsync`, `ConnectAsync`, …) that accepts a `CancellationToken`.

### 6. Query with the traversal API

Build pattern queries fluently. Aliases (`"a"`, `"w"`, `"p"`) bind nodes and edges so they can be referenced in `Where(...)` and `Return<T>(...)`.

```csharp
// All authors named "Alice"
Author[] alices =
[
    .. context.Traversal
        .Match()
        .Node<Author>("a")
        .Where(a => a.Name == "Alice")
        .Return<Author>("a")
];

// Every post Alice has written
Post[] alicesPosts =
[
    .. context.Traversal
        .Match()
        .Node<Author>("a")
        .Where(a => a.Name == "Alice")
        .RelatedBy<Wrote, Post>("w")
        .Outgoing()
        .To("p")
        .Return<Post>("p")
];

// Async streaming
await foreach (Post post in context.Traversal
    .Match()
    .Node<Author>("a")
    .RelatedBy<Wrote, Post>("w")
    .Outgoing()
    .To("p")
    .ReturnAsync<Post>("p"))
{
    Console.WriteLine(post.Title);
}
```

### 7. Drop down to raw statements when needed

```csharp
IReadOnlyDictionary<string, object?>[] rows =
[
    .. context.RawStatement.Execute(
        "MATCH (a:Author { Id: $id }) RETURN a.Name AS name",
        new Dictionary<string, object?> { ["id"] = alice.Id })
];

string? name = (string?)rows[0]["name"];
```

### 8. Explicit transactions

`SaveChanges` is atomic on its own, but you can compose multiple flushes inside a single transaction:

```csharp
await using IGraphTransaction tx = await context.BeginTransactionAsync();
try
{
    context.Nodes<Author>().Add(alice);
    await context.SaveChangesAsync();

    context.Nodes<Post>().Add(hello);
    await context.SaveChangesAsync();

    await tx.CommitAsync();
}
catch
{
    await tx.RollbackAsync();
    throw;
}
```

## Project status

The framework is under active development. The Neo4j provider is the reference implementation and is exercised by an integration-test suite running against a real Neo4j 5.x container.

## License

Licensed under the [MIT License](LICENSE).
