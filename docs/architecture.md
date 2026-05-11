# Architecture

A comprehensive tour of the Element Framework, starting from the highest-level view and descending into per-component implementation detail. Reading top-to-bottom should give you a complete mental model; jumping to a section should give you enough context to find your way around the corresponding code without surprises.

## Contents

1. [10,000-foot view](#1-10000-foot-view)
2. [Solution layout](#2-solution-layout)
3. [Mental model](#3-mental-model)
4. [Consumer surface](#4-consumer-surface)
5. [Internal architecture](#5-internal-architecture)
6. [Provider contract](#6-provider-contract)
7. [Traversal layer](#7-traversal-layer)
8. [Lifecycles](#8-lifecycles)
9. [Neo4j provider](#9-neo4j-provider)
10. [Apache AGE provider](#10-apache-age-provider)
11. [In-memory provider](#11-in-memory-provider)
12. [Diagnostics surface](#12-diagnostics-surface)
13. [Testing architecture](#13-testing-architecture)
14. [Adding a new provider](#14-adding-a-new-provider)
15. [Known constraints and non-goals](#15-known-constraints-and-non-goals)

---

## 1. 10,000-foot view

Element Framework is an Object-Graph Mapper (OGM) for .NET that targets labelled-property-graph databases. It plays the role for graph stores that Entity Framework plays for relational stores: you declare your domain in plain CLR types, the framework owns an identity map and a queue of pending changes, and `SaveChanges` translates them into provider-native statements wrapped in a transaction.

The framework is **provider-agnostic at the abstraction layer** and **Cypher-shaped in its default first-class provider**. The architectural seam between the two — the *provider contract* in [section 6](#6-provider-contract) — is the most load-bearing design decision in the codebase: every concrete database lives behind it, and the framework's portability story stands or falls on how well that seam holds up.

Three big-picture qualities to keep in mind while reading the rest of this document:

- **Public API is small and disciplined.** Outside the abstractions assembly, only a handful of types are public: the `GraphContext` base class, the options builder, two extension methods (`AddGraphContext` and `Use*` per provider), and the exception hierarchy. Everything else is `internal sealed`.
- **State lives on the context.** A `GraphContext` is a unit-of-work: it owns an identity map, an ordered queue of pending operations, and at most one ambient transaction. Two contexts pointed at the same database don't share state.
- **Models are frozen and cached process-wide.** The first time a `GraphContext` subclass is constructed, its `OnModelCreating` is invoked, validated, and the resulting `GraphModel` is cached by CLR type for the lifetime of the process.

---

## 2. Solution layout

The solution is five production projects plus six test projects.

```
onixlabs-dotnet-element-framework/
├── OnixLabs.ElementFramework.Abstractions/   - Provider-agnostic contracts. Reference-only.
├── OnixLabs.ElementFramework/                - Default implementation: change tracker,
│                                               model builder, sets, traversal pipeline,
│                                               DI registration.
├── OnixLabs.ElementFramework.Neo4j/          - Cypher-over-Bolt provider (reference).
├── OnixLabs.ElementFramework.AGE/            - Cypher-over-Npgsql provider for Apache
│                                               AGE on PostgreSQL.
├── OnixLabs.ElementFramework.InMemory/       - In-process provider (tests, demos).
│
├── OnixLabs.ElementFramework.UnitTests/             - Unit tests for the default impl.
├── OnixLabs.ElementFramework.Neo4j.UnitTests/       - Unit tests for the Neo4j provider's
│                                                      pure functions.
├── OnixLabs.ElementFramework.AGE.UnitTests/         - Unit tests for the AGE provider's
│                                                      pure functions.
├── OnixLabs.ElementFramework.Conformance/           - Provider-agnostic integration
│                                                      tests; subclassed per provider.
├── OnixLabs.ElementFramework.InMemory.IntegrationTests/  - Conformance against in-mem.
├── OnixLabs.ElementFramework.Neo4j.IntegrationTests/     - Conformance against Neo4j
│                                                           via Testcontainers.
└── OnixLabs.ElementFramework.AGE.IntegrationTests/       - Conformance + reconnaissance
                                                            against Apache AGE via
                                                            Testcontainers.
```

**Dependency direction is strictly inward.** `OnixLabs.ElementFramework` references `Abstractions`. Each provider references `OnixLabs.ElementFramework` (which transitively pulls `Abstractions`). Provider-to-provider references do not exist; the conformance test project depends only on `OnixLabs.ElementFramework`.

Multi-targeting is **net8.0**, **net9.0**, and **net10.0**. CI runs the full test matrix against all three.

---

## 3. Mental model

Before reading the code, internalize four primitives:

- **Node.** A vertex in the graph, mapped to a CLR class. Every node has a configured *key* property (the value the framework uses to identify it across reads, writes, and the identity map) and a *label* (the string the database uses to discriminate types — defaults to the CLR type name).
- **Edge.** A relationship between two nodes, also mapped to a CLR class. An edge has *endpoints* (the start and end node CLR types), an optional payload of properties, and a *relationship type* (the database-side discriminator — defaults to the edge CLR type name).
- **Model.** The frozen description of every registered node and edge for a given context. Built once on first context construction, validated, and cached.
- **Context.** The runtime unit-of-work. Holds a reference to the frozen model, the provider services, an identity map, a queue of pending operations, and at most one ambient transaction.

A model is **purely a CLR-side description** — it never holds onto a connection, a transaction, or any provider state. The context is where CLR-side description meets provider-side reality.

---

## 4. Consumer surface

The public surface a consumer touches:

| Type / member | Purpose |
| --- | --- |
| `GraphContext` (abstract) | Subclassed by the consumer; declares the model via `OnModelCreating`. |
| `GraphContextOptions` | Options bag passed to the context's constructor. Built by DI; consumers don't instantiate it. |
| `INodeTypeConfiguration<T>` / `IRelationshipConfiguration<TStart, TEdge, TEnd>` | One-class-per-aggregate model configurations. |
| `INodeBuilder<T>` / `IRelationshipBuilder<TStart, TEdge, TEnd>` | Fluent surfaces inside configurations: `HasKey`, `HasLabel`, `Property`, `Ignore`. |
| `context.Nodes<T>()` / `context.Edges<T>()` | Typed accessors: `Add`, `Update`, `Remove`, `Merge`, `Connect`, `Disconnect`, `FindById`, `Exists`, `AsEnumerable`, async counterparts. |
| `context.SaveChanges()` / `SaveChangesAsync` | Atomic flush of all pending operations. |
| `context.BeginTransaction()` / `BeginTransactionAsync` | Explicit transaction scope; multiple `SaveChanges` inside it commit together. |
| `context.Traversal.Match()` / `Merge()` / `Create()` | Fluent traversal builder; chained with `Node<T>(alias)`, `RelatedBy<TEdge, TEnd>(alias)`, `Outgoing()/Incoming()/Either()`, `To(alias)`, `Where(lambda)`, `Return<T>(alias)` / `ReturnAsync<T>(alias)`. |
| `context.RawStatement.Execute(...)` / `ExecuteAsync(...)` | Provider-native statement escape hatch returning `IReadOnlyDictionary<string, object?>` rows. |
| `services.AddGraphContext<TContext>(builder => builder.UseNeo4j(...))` | DI registration. The provider is configured inside the builder; the context is registered with `Scoped` lifetime by default. |

The README has a worked walkthrough; this document focuses on what happens *behind* those calls.

---

## 5. Internal architecture

This section walks through the major internal types in dependency order: from the leaves (metadata) up to the orchestrator (`GraphContext`).

### 5.1 Model: `GraphModel`, `NodeMetadata`, `RelationshipMetadata`, `PropertyMetadata`

A model is a frozen graph description, built once per `GraphContext` CLR type and cached process-wide via `ModelSource`. The fluent builder accumulates configurations into `NodeBuilder<T>` / `RelationshipBuilder<TStart, TEdge, TEnd>` instances, then materializes each into a `NodeMetadata` / `RelationshipMetadata` record.

Validation runs at `GraphModel`'s constructor and enforces four invariants — see [`GraphModel.Validate`](../OnixLabs.ElementFramework/GraphModel.cs):

1. Every node has a configured key.
2. No two node types share a label.
3. Every relationship's start and end CLR type is a registered node.
4. No two relationships share a `(StartType, EndType, RelationshipType)` triple.

A node's `PropertyMetadata` carries:

- The reflected `PropertyInfo`.
- The mapped name (defaulting to the property name, or the override from `builder.Property(p => p.X, "x_alias")`).
- A nullability flag derived from C# 8 nullable annotations (`NullabilityInfoContext`).
- Compiled `Getter` and `Setter` delegates for cheap value access on the hot path (the framework never reflects on the property at runtime — it reflects once at build time and runs delegates thereafter).

Ignored properties (via `builder.Ignore(...)`) are filtered before metadata is built; they don't appear in `Properties`.

### 5.2 Model caching: `ModelSource`

`ModelSource` is a static `ConcurrentDictionary<Type, Lazy<GraphModel>>` keyed by `GraphContext` subclass type. The first construction of a given context type triggers a `Build()` that:

1. Instantiates a fresh `GraphModelBuilder`.
2. Invokes `OnModelCreating(builder)` (the consumer's override).
3. Calls `builder.Build()` to materialize and validate the model.

Subsequent context instances of the same type get the cached model immediately. This means **`OnModelCreating` is invoked exactly once per process per context subclass** — consumers must treat it as pure.

The cache is process-wide and never evicted. Test code that hot-reloads context types should be aware (tests in this repo don't do that).

### 5.3 Context construction: `GraphContext` → `GraphContextOptions` → `GraphContextServices`

The hand-off from DI to a constructed context is:

```
services.AddGraphContext<TContext>(b => b.UseNeo4j(...))
                 │
                 ▼
   GraphContextOptionsBuilder.Build(BuildGraphContextServices)
                 │
                 ▼
   GraphContextOptions { StatementEmitter, ResultMaterializer,
                         RawStatementExecutor,
                         GraphTransactionOpener,
                         TraversalTranslator,
                         LoggerFactory,
                         CreateServices }
                 │  (captured by ActivatorUtilities.CreateInstance)
                 ▼
   TContext(GraphContextOptions options)  →  base(options)
                 │
                 ▼
   GraphContext constructor invokes options.CreateServices(this, options)
                 │
                 ▼
   ServiceCollectionExtensions.BuildGraphContextServices:
       - ModelSource.ModelFor(this)         → frozen IGraphModel
       - new ChangeTracker(...)
       - new GraphSetFactory(...)
       - new GraphTransactionFactory(...)
       - new GraphTraversal(...)
                 │
                 ▼
   GraphContextServices record stored on the context.
```

`GraphContextOptions` is `[EditorBrowsable(Never)]` — it's public because the consumer subclass's constructor must accept it, but it's not meant to be hand-instantiated. The `required init` properties plus the internal `CreateServices` delegate together mean the only practical way to obtain one is via `GraphContextOptionsBuilder.Build(...)`, called from inside `AddGraphContext`.

`GraphContextServices` is the bundle of internal coordination services every `GraphContext` member delegates to: it's a positional record carrying the `IChangeTracker`, the `IGraphSetFactory`, the `IGraphTransactionFactory`, the `IGraphTraversal`, and the provider's `IRawStatementExecutor`.

### 5.4 Change tracking: `ChangeTracker`

`ChangeTracker` ([`/OnixLabs.ElementFramework/ChangeTracker.cs`](../OnixLabs.ElementFramework/ChangeTracker.cs)) is the heart of the unit-of-work. It owns two collections:

- An **identity map**: `Dictionary<(Type, object Key), object>` keyed by CLR type and the configured key value. The identity map's job is read-after-write coherence: once a node is tracked, `FindById` returns the same instance.
- A **pending queue**: `List<Func<IStatementEmitter, IGraphModel, DataStatement>>`. Each entry is a *deferred* statement-emission closure. Statements are emitted at flush time, not at track time — this means the emitter sees the latest mutation of a node, not its state at track time.

Every mutation method (`TrackAdd`, `TrackUpdate`, `TrackRemove`, `TrackMerge`, `TrackConnect`, `TrackDisconnect`) does the same two things:

1. Update the identity map appropriately (`Add`/`Update`/`Merge` insert-or-replace; `Remove` removes; `Connect`/`Disconnect` don't touch it because the identity map indexes nodes, not edges).
2. Append a closure to the pending queue that, when invoked with the emitter and model, returns a `DataStatement`.

`Attach` is a quieter cousin: it inserts into the identity map *without* queuing a statement, which is how `FindById` reconciles a fresh read with the tracker (see [§5.5](#55-typed-accessors-nodeset-edgeset-graphsetfactory)).

#### The flush

`Flush` (and its async twin `FlushAsync`) is the atomic boundary:

```
if (pending.Count == 0) return 0;
snapshot = [..pending]                          ← copy so a retry can replay

if (opener.Active is not null):
    ExecuteAll(snapshot)                        ← consumer owns the transaction
    pending.Clear()
    return count
else:
    transaction = opener.Open()                 ← auto-open ambient
    try:
        try:
            count = ExecuteAll(snapshot)
        catch:
            transaction.Rollback()
            throw                               ← pending is preserved for retry
        transaction.Commit()
        pending.Clear()
        return count
    finally:
        transaction.Dispose()
```

Two invariants worth memorizing:

- **Pending is cleared only on success.** A failed flush leaves the pending queue intact so a corrected retry can replay the entire batch. This is why the snapshot is taken before execution.
- **The flush opens its own transaction only when none is active.** A consumer who calls `BeginTransaction` before `SaveChanges` is signalling "I'll own the transaction lifecycle" — the tracker respects that and lets the consumer commit or rollback.

Auto-flush identity-map asymmetry: on `TrackRemove` the identity map removal happens *before* the flush. If a flush fails and rolls back, the identity map will report the node as gone while the database still has it. This is documented as a deliberate v1 trade-off in [`docs/production-readiness.md`](production-readiness.md) — the alternative is a deeper journaling design that v1 doesn't carry.

### 5.5 Typed accessors: `NodeSet`, `EdgeSet`, `GraphSetFactory`

`context.Nodes<T>()` returns a cached `INodeSet<T>`; `context.Edges<T>()` returns a cached `IEdgeSet<T>`. Both go through `GraphSetFactory` ([`/OnixLabs.ElementFramework/GraphSetFactory.cs`](../OnixLabs.ElementFramework/GraphSetFactory.cs)), which maintains two `ConcurrentDictionary<Type, object>` caches keyed by the requested CLR type.

The set instances themselves ([`NodeSet<T>`](../OnixLabs.ElementFramework/NodeSet.cs), [`EdgeSet<T>`](../OnixLabs.ElementFramework/EdgeSet.cs)) are thin orchestrators:

- **Mutations forward to the change tracker.** `Add` → `TrackAdd`, `Connect` → `TrackConnect`, etc. Async counterparts are sync underneath because tracking is in-process; they exist for API symmetry.
- **Reads route through the emitter → executor → materializer chain.** `AsEnumerable`/`AsAsyncEnumerable` go straight through. `Exists` interprets a single-row "count" projection. `FindById` first consults the identity map; on a miss, it queries, materializes, and `Attach`es the result so subsequent calls return the same instance.

Result rows from the executor carry the entity under whatever shape the provider's emitter chose to produce. The framework never names a specific alias for typed reads: it asks the materializer for `MaterializeNode`/`MaterializeEdge`/`ReadExists` and lets each provider pick the row shape its emitter agrees with internally. For consumer-supplied traversal aliases (the `alias` argument to `Return<T>(alias)`), the framework calls the materializer's `MaterializeNodeAt`/`MaterializeEdgeAt` and passes the consumer's alias straight through.

### 5.6 Transaction lifecycle: `GraphTransactionFactory`, `RollbackAwareGraphTransaction`

The user-visible `BeginTransaction` / `BeginTransactionAsync` route through `GraphTransactionFactory` ([`/OnixLabs.ElementFramework/GraphTransactionFactory.cs`](../OnixLabs.ElementFramework/GraphTransactionFactory.cs)), which:

1. Calls the provider's `IGraphTransactionOpener.Open()` (or `OpenAsync`) to get the underlying `IGraphTransaction`.
2. Wraps it in a `RollbackAwareGraphTransaction` ([`/OnixLabs.ElementFramework/RollbackAwareGraphTransaction.cs`](../OnixLabs.ElementFramework/RollbackAwareGraphTransaction.cs)).

The wrapper exists to keep the **identity map in sync with the underlying transaction's outcome**: when a transaction rolls back, the in-memory map must be cleared so `FindById` doesn't keep returning stale references to entities the database no longer has.

The wrapper's contract:

| Terminal | Inner transaction | Tracker reset |
| --- | --- | --- |
| `Commit` | committed | no — writes are durable, in-memory and DB match |
| `Rollback` | rolled back | yes — writes are gone, in-memory is stale |
| `Dispose` only | best-effort rollback | yes — outcome unknown, safest to clear |
| `Dispose` after `Commit` | already committed | no — flag cleared by `Commit` |
| `Dispose` after `Rollback` | already rolled back | no — flag cleared by `Rollback` (idempotent: the rollback's own reset already ran) |

The wrapper is symmetric across sync and async terminals; both branches set the same `resetOnDispose` flag.

The opener's *ambient* slot (`IGraphTransactionOpener.Active`) is the canonical "is a transaction open?" question and is owned by the provider. `ChangeTracker.Flush` consults that slot, not the wrapper.

---

## 6. Provider contract

The seam between the framework and any concrete database is six interfaces and one data record, all in `OnixLabs.ElementFramework.Abstractions`. A provider implements all six and supplies them to the options builder through a `Use<Provider>(...)` extension method.

| Contract | Role | Notes |
| --- | --- | --- |
| `IStatementEmitter` | Translates a domain operation (`EmitAdd`, `EmitConnect`, `EmitFindById`, `EmitTraversal`, etc.) into a `DataStatement`. Stateless, pure. | The single largest surface in the contract — 11 methods. The framework calls them via the `ChangeTracker.pending` closures. |
| `IResultMaterializer` | Projects a result-row dictionary into a CLR node or edge instance, plus reads existence outcomes. | Five methods: `MaterializeNode<T>` / `MaterializeEdge<T>` (typed reads, no alias — provider-internal convention) and `MaterializeNodeAt<T>` / `MaterializeEdgeAt<T>` (alias-bearing, for traversal returns), plus `ReadExists` for the existence check. |
| `IRawStatementExecutor` | Runs a `DataStatement` against the database (or escape-hatch raw text). Returns rows as `IReadOnlyDictionary<string, object?>` sequences. | Sync + async; the executor is the only place that talks to the database transport. |
| `IGraphTransactionOpener` | Opens new `IGraphTransaction` instances; exposes the currently-active ambient transaction. | "One ambient at a time" is the v1 invariant — a second `Open` while another is active throws `GraphTransactionAlreadyActiveException`. |
| `IGraphTransaction` | The transaction handle: `Commit`, `Rollback`, `Dispose` (sync + async). | One-shot: a second commit/rollback after the first is a no-op. |
| `ITraversalTranslator` | Translates and executes a `TraversalAst`, materializing each row to `TResult`. | Note: this is distinct from `IStatementEmitter.EmitTraversal`. The translator can either route through emitter→executor→materializer (Neo4j's choice) or interpret the AST directly (in-memory's choice). |
| `DataStatement` (record) | A `(Statement, Parameters)` pair: the provider-native statement text and a parameter dictionary. | Values are never inlined into `Statement`; everything flows through `Parameters` so the executor can bind them safely. |

The framework treats these as duck-typed: anything that conforms compiles. There is no provider base class to inherit from.

A few of the contract's load-bearing conventions:

- **Typed-read row shape is provider-internal.** For framework-emitted reads (`EmitFindById`, `EmitExists`, `EmitAsEnumerableNodes`, `EmitAsEnumerableEdges`), the provider's emitter and materializer agree privately on row shape — the framework never names `"n"` / `"r"` / `"count"`. A Cypher provider can return rich entities under an alias; a SQL-graph provider can return flat property cells. The framework just calls `MaterializeNode` / `MaterializeEdge` / `ReadExists`.
- **Traversal row aliases come from the consumer.** `Return<T>(alias)` passes the alias through `ITraversalTranslator` to the materializer's `MaterializeNodeAt` / `MaterializeEdgeAt`.
- **Parameter keys are unprefixed.** A provider statement that uses `$id` should appear in `DataStatement.Parameters` as `"id"`, not `"$id"`. The provider adds the prefix on the wire.

---

## 7. Traversal layer

The traversal pipeline is the framework's strongly-typed query DSL. End-to-end:

```
context.Traversal.Match().Node<Author>("a").Where(a => a.Name == "Alice")
                 .RelatedBy<Wrote, Post>("w").Outgoing().To("p").Return<Post>("p")

   │  fluent stages mutate a single TraversalState scratchpad
   ▼
TraversalAst { Kind, Segments [Node, Rel, Node], Predicates [tree], ReturnAlias }
   │  passed to the provider's ITraversalTranslator
   ▼
Neo4jTraversalTranslator         |   InMemoryTraversalTranslator
  emitter.EmitTraversal(...) →   |     walks pattern + evaluates
  executor.Execute(cypher) →     |     predicates against
  materializer.Materialize…      |     InMemoryStore directly
   │
   ▼
IEnumerable<TResult> / IAsyncEnumerable<TResult>
```

### 7.1 Fluent stages

Implemented in `OnixLabs.ElementFramework`:

| Interface | Role |
| --- | --- |
| `IGraphTraversal` | Entry. `Match()`, `Merge()`, `Create()` each return an `IPatternStart` and seed a fresh `TraversalState` with the chosen `TraversalKind`. |
| `IPatternStart` | First binding stage. `Node<T>(alias)` appends a `NodePatternSegment` and returns `IPatternNode<T>`. |
| `IPatternNode<T>` | Has bound a node. Offers `RelatedBy<TEdge, TEnd>(alias)`, `Where(lambda)`, and the terminals `Return<TResult>(alias)` / `ReturnAsync<TResult>(alias)`. |
| `IPatternRelationship<TStart, TEdge, TEnd>` | Has bound an edge but not yet chosen direction. Offers `Outgoing()`, `Incoming()`, `Either()` — each returns an `IPatternRelationshipDirected<...>`. |
| `IPatternRelationshipDirected<TStart, TEdge, TEnd>` | Direction pinned. `To(alias)` binds the end node and returns the next `IPatternNode<TEnd>`. |

State accumulates in a single shared `TraversalState` ([`/OnixLabs.ElementFramework/TraversalState.cs`](../OnixLabs.ElementFramework/TraversalState.cs)) that every stage holds a reference to. The terminal `Return*` calls snapshot the state into an immutable `TraversalAst` via `BuildAst(returnAlias)`.

`RelatedBy` validates at runtime that the registered relationship's `(StartType, EndType)` matches the `(TNode, TEnd)` types either naturally or in reverse; that's why the same edge type can be consumed in both `Author --Wrote--> Post` and `Post <--Wrote-- Author` traversals.

### 7.2 The AST: `TraversalAst`, segments, kinds

`TraversalAst` ([`/OnixLabs.ElementFramework.Abstractions/TraversalAst.cs`](../OnixLabs.ElementFramework.Abstractions/TraversalAst.cs)) is the contract object the fluent builder hands to the translator:

```csharp
public sealed record TraversalAst(
    TraversalKind Kind,                                // Match | Merge | Create
    IReadOnlyList<PatternSegment> Segments,            // linear pattern
    IReadOnlyList<TraversalPredicate> Predicates,      // implicit-AND list of trees
    string ReturnAlias);
```

The segments list alternates `NodePatternSegment` and `RelationshipPatternSegment` and always begins with a node. A `RelationshipPatternSegment` carries an alias, the edge CLR type, and a `RelationshipDirection` (`Outgoing | Incoming | Either`).

The AST is intentionally **linear, not tree-shaped**, because Cypher patterns are linear. This is one of the architectural choices that scopes the framework to Cypher-family stores by design — see [§15](#15-known-constraints-and-non-goals).

### 7.3 The predicate tree

`Where(lambda)` calls accumulate `TraversalPredicate` instances into the AST. Each is a tree rooted at the abstract `TraversalPredicate` base. Subtypes:

| Type | Carries | Translates to |
| --- | --- | --- |
| `PropertyComparisonPredicate` | Alias, property name, `ComparisonOperator` (`Equal`, `NotEqual`, `LessThan`, `LessThanOrEqual`, `GreaterThan`, `GreaterThanOrEqual`), constant value | `alias.prop OP $token` |
| `StringComparisonPredicate` | Alias, property name, `StringComparisonOperator` (`Contains`, `StartsWith`, `EndsWith`), string value | `alias.prop CONTAINS $token` etc. |
| `NullPredicate` | Alias, property name, `IsNull` flag | `alias.prop IS NULL` / `IS NOT NULL` |
| `AndPredicate` | Left, Right (each a `TraversalPredicate`) | `(L) AND (R)` |
| `OrPredicate` | Left, Right | `(L) OR (R)` |
| `NotPredicate` | Inner | `NOT (inner)` |

The top-level `TraversalAst.Predicates` is a list (multiple `Where(...)` calls implicit-AND), but each list entry is itself a tree, so a single `Where(a => (a.X == 1 || a.Y == 2) && !a.Z)` is one entry that emitters/translators walk recursively.

### 7.4 Lambda translation: `PredicateTranslator`

[`PredicateTranslator`](../OnixLabs.ElementFramework/PredicateTranslator.cs) is the lambda-to-tree compiler. It walks `Expression<Func<TNode, bool>>` and produces a `TraversalPredicate`. Supported shapes:

- `==`, `!=`, `<`, `<=`, `>`, `>=` — produce `PropertyComparisonPredicate`. The translator detects which side is the property access on the lambda parameter and **flips ordered operators** when the property is on the right (`30 < a.Age` → `a.Age > 30`).
- `==` / `!=` against a constant `null` — produce `NullPredicate` instead of a degenerate comparison; this lets emitters produce `IS NULL` / `IS NOT NULL` rather than the non-portable `= NULL`.
- `&&`, `||`, `!` — recurse into the operands and produce `AndPredicate`, `OrPredicate`, `NotPredicate`.
- `string.Contains(arg)`, `StartsWith(arg)`, `EndsWith(arg)` (single-argument overloads) — produce `StringComparisonPredicate`.
- Captured closures on the value side — constant-folded via `Expression.Lambda(side).Compile().DynamicInvoke()`, so `a.Name == capturedName` works.

Anything else throws `NotSupportedException` with a message pointing at `IRawStatementExecutor.Execute(...)` as the escape hatch.

The translator is internal — `Where` is the only call site, and the produced tree is what gets accumulated.

---

## 8. Lifecycles

### 8.1 Registration → first call

```
Application startup:
  services.AddGraphContext<BlogGraphContext>(b => b.UseNeo4j("bolt://...", auth))
       ↓
  GraphContextOptionsBuilder collects UseStatementEmitter, UseResultMaterializer,
  UseRawStatementExecutor, UseGraphTransactionOpener, UseTraversalTranslator
       ↓
  builder.Build(BuildGraphContextServices) → GraphContextOptions (immutable)
       ↓
  ServiceCollection registered with: sp =>
      ActivatorUtilities.CreateInstance<TContext>(sp, options)
  with ServiceLifetime.Scoped by default.

First resolve:
  scope.GetRequiredService<BlogGraphContext>()
       ↓
  BlogGraphContext(options) → base(options)
       ↓
  GraphContext ctor calls options.CreateServices(this, options)
       ↓
  BuildGraphContextServices:
      1. ModelSource.ModelFor(this)  → triggers OnModelCreating on first build,
                                       caches frozen GraphModel by CLR type.
      2. Construct ChangeTracker, GraphSetFactory, GraphTransactionFactory,
         GraphTraversal, wiring each to the model and the provider services.
       ↓
  GraphContext exposes the bundle through Nodes<T>(), Edges<T>(), Traversal,
  RawStatement, BeginTransaction*, SaveChanges*.
```

### 8.2 `SaveChanges` lifecycle

Mutations during a unit-of-work:

```
context.Nodes<Author>().Add(alice)        → tracker.identityMap[(Author, alice.Id)] = alice
                                          → tracker.pending += emit => emit.EmitAdd(model, alice)
context.Nodes<Post>().Add(hello)          → ...
context.Edges<Wrote>().Connect(alice,     → tracker.pending += emit => emit.EmitConnect(model,
                       wrote, hello)                                  alice, wrote, hello)

context.SaveChanges()
       ↓
ChangeTracker.Flush:
   snapshot = [..pending]                        ← preserved for retry on failure
   if opener.Active is null:
       tx = opener.Open()                        ← provider auto-opens an ambient
       try:
           ExecuteAll(snapshot):
               for each closure in snapshot:
                   statement = closure(emitter, model)      ← provider emits CREATE/MATCH...
                   executor.Execute(statement)              ← provider runs against ambient tx
           tx.Commit()
           pending.Clear()
       catch:
           tx.Rollback()
           throw                                            ← pending stays full
       finally:
           tx.Dispose()
```

If a consumer has already opened a transaction via `BeginTransaction()`, `opener.Active` is non-null on entry to `Flush` and the auto-open / commit branch is skipped — execution joins the existing ambient transaction and the consumer commits or rolls back on their own schedule.

### 8.3 Explicit transaction + rollback semantics

```
using IGraphTransaction tx = context.BeginTransaction();
    │
    ├─ opener.Open() → provider transaction, registers as ambient
    └─ wrapped in RollbackAwareGraphTransaction

context.Nodes<Author>().Add(alice); context.SaveChanges();   ← runs in tx
context.Nodes<Post>().Add(hello);   context.SaveChanges();   ← still in tx

tx.Commit();        // wrapper:
                    //   inner.Commit()
                    //   resetOnDispose = false
                    //   identityMap is left intact (writes are durable)

// alternative: tx.Rollback();
//   inner.Rollback()
//   tracker.Reset()  ← identityMap and pending are cleared
//   resetOnDispose = false

// on Dispose without explicit terminal:
//   inner.Dispose() → provider rolls back best-effort
//   resetOnDispose was still true → tracker.Reset()
```

The wrapper is what makes "rollback discards the identity map" a framework guarantee independent of how providers implement their transactions.

---

## 9. Neo4j provider

The reference provider. Lives in `OnixLabs.ElementFramework.Neo4j` and depends on `Neo4j.Driver` 6.x (async-only).

### 9.1 Wiring: `GraphContextOptionsBuilderExtensions.UseNeo4j`

Two overloads:

- `UseNeo4j(builder, string connectionString, IAuthToken? authToken)` — eager.
- `UseNeo4j(builder, Func<string> connectionStringFactory, IAuthToken? authToken)` — lazy. The factory is invoked once, on first driver resolution. Used by Testcontainers fixtures whose connection string is only known after the container starts.

Both forms construct the full set of provider services and supply them to the options builder:

```csharp
Lazy<IDriver> driver = new(...);
CypherEmitter emitter = new();
Neo4jResultMaterializer materializer = new();
Neo4jGraphTransactionOpener opener = new(driver);
Neo4jCypherExecutor executor = new(driver, opener);
Neo4jTraversalTranslator translator = new(emitter, executor, materializer);

builder.UseStatementEmitter(emitter)
       .UseResultMaterializer(materializer)
       .UseRawStatementExecutor(executor)
       .UseGraphTransactionOpener(opener)
       .UseTraversalTranslator(translator);
```

### 9.2 Driver caching: `Neo4jDriverCache`

A process-wide `ConcurrentDictionary<DriverKey, IDriver>` keyed by `(connectionString, authToken)`. Drivers are designed by the Neo4j team for app-singleton reuse — they manage their own pool. Caching them means every `AddGraphContext` call against the same endpoint shares one driver and its pool.

Auth-token equality is reference-based because `IAuthToken` doesn't override equality; two callers building separate `AuthTokens.Basic(...)` objects against the same credentials will create two drivers. In production this is fine (the connection string and auth are constructed once); in tests it's a non-issue because each Testcontainer has a unique connection string.

The cache has no eviction — see [§15](#15-known-constraints-and-non-goals).

### 9.3 Cypher emission: `CypherEmitter`, `CypherIdentifier`, `ParameterBinder`, `PropertySerializer`

`CypherEmitter` ([`/OnixLabs.ElementFramework.Neo4j/CypherEmitter.cs`](../OnixLabs.ElementFramework.Neo4j/CypherEmitter.cs)) is the largest single file in the codebase. It's stateless across calls — every method takes the model and operands, returns a `DataStatement`, holds no per-call state. Cross-cutting rules every method follows:

- Every CLR-side label or property name flows through `CypherIdentifier.Escape`, which backtick-quotes anything that isn't a bare identifier or that collides with a Cypher 5 reserved word. The reserved word list lives in `CypherIdentifier`.
- Every value is bound via a per-call `ParameterBinder` — values are never inlined into the Cypher string. The binder generates collision-free names (`$Body`, `$Body_1`, …) and exposes the accumulated bindings via `ToParameters()`.
- Every value passes through `PropertySerializer.Serialize` before binding so the Neo4j driver receives a Bolt-friendly form: `Guid` → string, `DateTimeOffset` → `ZonedDateTime`, enum → its name. Primitives, strings, byte arrays, and lists pass through unchanged.
- `EmitAdd` skips null-valued non-key properties from the property clause (write-time minimalism); `EmitUpdate` writes every non-key property explicitly so consumers can clear values; `EmitConnect` omits the property clause when the edge has no mapped properties (marker edges).

For `EmitTraversal`, the emitter walks the AST in three phases:

1. Append `MATCH` / `MERGE` / `CREATE` keyword and the pattern (`(a:Author)-[w:WROTE]->(p:Post)`).
2. If `Predicates` is non-empty, append `WHERE`, then for each list entry append `AND` and recursively walk the predicate tree, parenthesizing every top-level entry (defensive against `AND` having higher precedence than `OR` in Cypher).
3. Append `RETURN <alias>`.

Predicate emission is recursive across `PropertyComparisonPredicate` (operator translated via `CypherOperator`), `StringComparisonPredicate` (operator translated via `CypherStringOperator`), `NullPredicate` (no parameter binding — emits the bare property), and the boolean composers (parenthesized children).

### 9.4 Execution: `Neo4jCypherExecutor`

The executor is the only place in the provider that talks to Bolt. Its job:

1. Convert the incoming parameter dictionary's values through `PropertySerializer.Serialize`.
2. If `opener.Active` is a `Neo4jGraphTransaction`, run through that transaction's `IAsyncTransaction.RunAsync`. Otherwise, open a fresh `IAsyncSession`, run, close.
3. Drain the `IResultCursor` into a `List<IReadOnlyDictionary<string, object?>>` and return it.

**Lazy async streaming.** `ExecuteAsync` is a genuine async iterator — the `IResultCursor` is iterated as the consumer pulls rows, and the auto-commit `IAsyncSession` is held open via `await using` for the lifetime of the enumerator. Disposing the enumerator (`await foreach` completing, a `break` mid-stream, or a thrown exception unwinding past it) closes the session immediately. Open-time failures still surface as `RawStatementException`; mid-stream driver exceptions during enumeration propagate raw — the price the async surface pays for not OOMing on large result sets.

**Sync surface materializes eagerly.** `Execute` drains the entire stream into a list before returning, so sync consumers see the pre-streaming exception model (every failure surfaces at execute time, wrapped). Both surfaces route through the same `StreamAsync` primitive; `MaterializeAllAsync` is the eager adapter.

**Sync surface bridges via `GetAwaiter().GetResult()`.** The Neo4j driver is async-only; the sync methods bridge through a non-context-preserving block. Under hosts that capture a synchronization context (ASP.NET Classic, WinForms, WPF), this deadlocks. ASP.NET Core and console hosts are unaffected. The sync surface is intentional — see [§15 known constraints](#15-known-constraints-and-non-goals): the abstraction must accommodate providers that ship sync-only, async-only, or both, so the sync path is a documented footgun rather than a defect. The `UseNeo4j` xmldoc warns; a Roslyn analyzer that flags the sync surface under risky SDKs is the next mitigation.

### 9.5 Transactions: `Neo4jGraphTransactionOpener`, `Neo4jGraphTransaction`

The opener holds a single ambient slot (`active: Neo4jGraphTransaction?`). `Open()` opens a new session, begins a transaction on it, wraps both in a `Neo4jGraphTransaction`, and assigns the wrapper to the slot. A second `Open()` while the slot is non-null throws `GraphTransactionAlreadyActiveException`.

The transaction wrapper:

- `Commit`/`Rollback` (async) drive the underlying `IAsyncTransaction`, then call `CloseAsync` which closes the session and clears the opener's ambient slot exactly once.
- `Dispose` performs a best-effort `RollbackAsync` and then `CloseAsync`. Exceptions inside these dispose-path branches are caught (the dispose contract forbids letting them out), but they are now logged at `Warning` rather than swallowed silently — `Neo4jGraphTransaction` accepts an `ILoggerFactory?` and uses it to surface dispose-time failures.
- Sync paths bridge to async via `GetAwaiter().GetResult()` (same caveat as the executor).
- The terminal is one-shot: a second commit or rollback after the first is a no-op.

### 9.6 Materialization: `Neo4jResultMaterializer`

Reads the Bolt entity (`INode` for nodes, `IRelationship` for edges) and constructs a CLR `T`, walking the registered `IPropertyMetadata` list and copying each mapped property from the entity's `Properties` dictionary. For typed framework reads the alias is the provider's internal `NodeAlias` / `EdgeAlias` constant (`"n"` / `"r"`); for traversal returns the alias is passed through from the consumer's `Return<T>(alias)`. `ReadExists` reads `row["count"] is long and > 0`.

Instantiation strategy:

- If `T` has a parameterless constructor (public or non-public), a compiled `Expression.Lambda<Func<object>>(Expression.New(ctor))` is built and cached per type.
- Otherwise, falls back to `RuntimeHelpers.GetUninitializedObject(type)`, which bypasses field initializers and constructor invariants. The framework prefers a parameterless ctor; production-readiness recommends documenting it as a requirement.

Conversion is the inverse of `PropertySerializer`:

- `string` → `Guid` when the target is `Guid`.
- `ZonedDateTime` → `DateTimeOffset`.
- `string` → enum via `Enum.Parse`.
- Primitive coercion via `Convert.ChangeType` (which uses the current culture — production-readiness flags this; the doc recommends invariant culture).

### 9.7 Traversal: `Neo4jTraversalTranslator`

A thin orchestrator: emits the Cypher via `IStatementEmitter.EmitTraversal`, executes via `IRawStatementExecutor`, materializes each row.

The "is this row a node or an edge?" question is answered by inspecting the AST's return alias and finding the matching segment (`NodePatternSegment` → call `MaterializeNode`, `RelationshipPatternSegment` → call `MaterializeEdge`). This lets `context.Traversal.Match()...Return<Wrote>("w")` materialize the edge alias correctly even though the framework can't statically distinguish them from the type alone.

The sync path materializes eagerly; the async path is a proper async iterator that streams as the executor yields.

---

## 10. Apache AGE provider

The portability proof. Lives in `OnixLabs.ElementFramework.AGE` and depends on `Npgsql` 9.x. AGE is a PostgreSQL extension that adds an openCypher-compatible graph engine to a relational database; queries are issued as SQL that wraps Cypher inside a `cypher(graph, $$...$$, params)` table function. Where the Neo4j provider is "Cypher over Bolt", the AGE provider is "Cypher over Npgsql + agtype" — sharing no driver code with Neo4j and exercising the framework's seam against a fundamentally different wire format.

### 10.1 Wiring: `UseAge`

Two overloads, mirroring the Neo4j provider:

- `UseAge(builder, string connectionString, string graphName)` — eager.
- `UseAge(builder, Func<string> connectionStringFactory, string graphName)` — lazy. The factory is invoked once, on first data-source resolution. Used by Testcontainers fixtures whose connection string is only known after the container starts.

Both forms construct the full set of provider services and supply them to the options builder:

```csharp
Lazy<NpgsqlDataSource> dataSource = new(() => AgeDataSourceCache.GetOrCreate(...));
AgeCypherEmitter emitter = new(graphName);
AgeResultMaterializer materializer = new();
AgeGraphTransactionOpener opener = new(dataSource.Value, ...);
AgeRawStatementExecutor executor = new(dataSource.Value, opener, ...);
AgeTraversalTranslator translator = new(emitter, executor, materializer);
```

### 10.2 Data-source caching: `AgeDataSourceCache`

A process-wide `ConcurrentDictionary<(connectionString, graphName), NpgsqlDataSource>`. Npgsql's `NpgsqlDataSource` owns the connection pool — caching it process-wide means every `AddGraphContext` against the same endpoint shares one pool.

Each cached data source is built with two crucial pieces of configuration:

- **`Options=-c search_path=ag_catalog,public`** baked into the connection string. AGE installs its `cypher()` function and `agtype` type under the `ag_catalog` schema, and PostgreSQL's default `search_path` doesn't include it. Setting `search_path` via the connection string's startup-`Options` parameter applies before any query runs and survives across logical opens; doing the same via a `SET` in a physical-connection initializer does *not* — the setting doesn't always persist back to the pool. The emitter additionally schema-qualifies `ag_catalog.cypher(...)` so the SQL form is robust even when callers run with a different search path.
- **A physical-connection initializer** that runs `CREATE EXTENSION IF NOT EXISTS age` and `SELECT create_graph(graph_name)` if the graph doesn't already exist. AGE's `shared_preload_libraries = 'age'` in the upstream container image means `LOAD 'age'` is not needed per session.

The cache has no eviction — same trade-off as the Neo4j driver cache; flagged in [§15](#15-known-constraints-and-non-goals).

### 10.3 Cypher emission: `AgeCypherEmitter`

The emitter follows the same shape as Neo4j's `CypherEmitter` — pure, stateless, one method per provider-contract operation — but produces **SQL-wrapped Cypher** rather than bare Cypher:

```
SELECT * FROM ag_catalog.cypher('graph_name', $$ <cypher body> $$, @p) AS (n agtype)
```

Where `<cypher body>` is the same Cypher a Neo4j emit would produce. Cross-cutting rules:

- Cypher identifier escaping reuses the same `CypherIdentifier` (backticks, reserved-word table) — the openCypher dialect AGE accepts is the same as Neo4j's for the operations the framework emits.
- Values are accumulated by a per-call `AgeParameterBinder` (identical contract to the Neo4j `ParameterBinder`) and serialized via `AgePropertySerializer` to JSON-friendly primitives (`Guid` → string, `DateTimeOffset`/`DateTime` → ISO-8601 round-trip, enum → its name).
- The wrapped form takes parameters as a single `agtype` payload bound to SQL parameter `@p`. The executor JSON-serializes the binder's dictionary via `AgtypeWriter` and sends it as `NpgsqlDbType.Unknown`, which lets Postgres infer `agtype` (Npgsql doesn't ship an agtype writer) and satisfies AGE's strict "the third argument must be a bare parameter symbol" check.

Two dialect deltas the emitter handles internally:

- **`MERGE ... ON CREATE SET / ON MATCH SET` is not implemented in AGE 1.6.0.** Neo4j's emitter uses identical SET lists in both branches; the unconditional `MERGE ... SET ...` form has the same observable semantics and AGE accepts it.
- **`count` is a SQL reserved word.** AGE rejects it as a column alias in the SQL `AS (...)` schema. The emitter renames the existence-count alias to `cnt` (matched by `AgeResultMaterializer.CountAlias`).

### 10.4 Execution: `AgeRawStatementExecutor`

The executor is the only place in the provider that talks to Postgres. Its job:

1. Open a connection — the ambient `AgeGraphTransaction` if any, otherwise a fresh `NpgsqlConnection` borrowed from the cached data source.
2. Bind parameters: if the dictionary is non-empty, JSON-encode it via `AgtypeWriter` and add as a single `@p` parameter with `NpgsqlDbType.Unknown`.
3. **Set `command.AllResultTypesAreUnknown = true`** before executing. This is the single most important line in the provider: it tells Npgsql to return every column as a `string` at the protocol level rather than trying to map it to a CLR type. Without it, the `ag_catalog.agtype` columns AGE emits cannot be read at all — Npgsql doesn't ship an `agtype` reader, and `GetValue` / `GetFieldValue<string>` both throw.
4. Drain the `NpgsqlDataReader` into a `List<IReadOnlyDictionary<string, object?>>` where every cell is a string (or null).

Lazy async streaming and sync-over-async bridging both follow Neo4j's pattern. The async path yields rows as the consumer pulls them; the auto-commit `NpgsqlConnection` (and its command + reader) is held via `await using` for the lifetime of the enumerator and returned to the data-source pool on enumerator dispose. The sync path eagerly drains the same stream into a list before returning.

### 10.5 Transactions: `AgeGraphTransactionOpener`, `AgeGraphTransaction`

`AgeGraphTransactionOpener` borrows an `NpgsqlConnection` from the cached data source on `Open()`, begins an `NpgsqlTransaction` on it, wraps both in an `AgeGraphTransaction`, and assigns it to the single ambient slot. The transaction wrapper drives `NpgsqlTransaction.CommitAsync` / `RollbackAsync` on the terminal call, then disposes the transaction and returns the connection to the pool. Same one-ambient-at-a-time invariant and same dispose-path logging as Neo4j.

### 10.6 Materialization: `AgeResultMaterializer`, `AgtypeReader`, `AgtypeWriter`

Rows reach the materializer as `IReadOnlyDictionary<string, string?>` (Npgsql in text mode). The materializer parses each agtype value via `AgtypeReader`:

- **Vertex** and **edge** literals look like `{"id": 844424930131969, "label": "Author", "properties": {...}}::vertex` (edges include `start_id` / `end_id` and use `::edge`). The reader strips the suffix tag and parses the body as JSON via `JsonDocument`.
- **Scalars** come back as JSON literals: strings quoted (`"Alice"`), numbers bare (`42`, `3.14`), booleans / nulls bare. `JsonDocument` distinguishes these via `JsonValueKind`. Integers are detected with `TryGetInt64` — note the load-bearing `(object)` cast in `ParseJsonScalar`, without which the conditional's common type widens `long` to `double` and every integer agtype scalar arrives as `System.Double`, breaking `ReadExists`'s `is long` pattern match.

CLR conversion is the inverse of `AgePropertySerializer`: ISO-8601 string → `DateTimeOffset`/`DateTime`, hyphenated string → `Guid`, member-name string → enum, primitive coercion via `Convert.ChangeType` (under invariant culture to stay locale-safe).

`AgtypeWriter` is the symmetric send-side: it takes the binder's parameter dictionary and produces an agtype JSON object suitable for the `@p` parameter. Values are expected to have already been flattened by `AgePropertySerializer` so the writer only sees JSON-friendly primitives.

### 10.7 Traversal: `AgeTraversalTranslator`

Identical shape to `Neo4jTraversalTranslator` — emit the wrapped SQL via `IStatementEmitter.EmitTraversal`, execute via `IRawStatementExecutor`, dispatch per-row materialization to `MaterializeNodeAt` or `MaterializeEdgeAt` based on the segment kind that the AST's return alias resolves to.

### 10.8 What the conformance suite skips

The four raw-statement conformance tests are skipped against AGE — they send bare openCypher into `RawStatement.Execute`, and AGE's raw surface is SQL-wrapped Cypher. Same shape as the in-memory provider's skips. Every other conformance test passes against `apache/age:release_PG16_1.6.0`.

---

## 11. In-memory provider

A third concrete provider whose entire database is a process-resident `Dictionary` + `List` pair. Lives in `OnixLabs.ElementFramework.InMemory`. It serves two purposes: an integration-test double for downstream applications, and the in-process implementation that proves the seam works even when there is no query language to emit.

### 11.1 Wiring: `UseInMemory(string databaseName)`

```csharp
InMemoryStore store = InMemoryStoreRegistry.GetOrCreate(databaseName);
InMemoryGraphTransactionOpener opener = new(store);
InMemoryStatementEmitter emitter = new();
InMemoryRawStatementExecutor executor = new(opener);
InMemoryResultMaterializer materializer = new();
InMemoryTraversalTranslator translator = new(opener);

builder.UseStatementEmitter(emitter)
       .UseResultMaterializer(materializer)
       .UseRawStatementExecutor(executor)
       .UseGraphTransactionOpener(opener)
       .UseTraversalTranslator(translator);
```

Multiple contexts that bind the same `databaseName` share state — the store is fetched from a process-wide registry.

### 11.2 Storage: `InMemoryStore`, `InMemoryStoreRegistry`, `InMemoryEdge`

`InMemoryStore` is a CLR-private graph:

- `Dictionary<(Type, object Key), object> nodes` — node instances keyed by CLR type and key value.
- `List<InMemoryEdge> edges` — edges in insertion order. `InMemoryEdge` is a record carrying `(StartType, StartKey, EdgeType, Edge, EndType, EndKey)`.

The store exposes `UpsertNode`, `RemoveNode`, `FindNode`, `NodesOfType`, `AddEdge`, `RemoveEdges`, `EdgesOfType`, `EdgesIncidentTo`, `Clear`, and crucially `Clone` and `ReplaceWith` for transaction snapshot semantics.

`InMemoryStoreRegistry` is a `ConcurrentDictionary<string, InMemoryStore>` with three operations:

- `GetOrCreate(name)` — fetches or creates the store.
- `Reset(name)` — calls `store.Clear()`, keeping the registration. Tests use this as a per-test hook.
- `Drop(name)` — removes the registration entirely.

The store itself is **not thread-safe** — concurrent contexts pointing at the same name aren't protected. The provider is for tests and small demos, not production throughput.

### 11.3 Op-coded statements: `InMemoryStatementEmitter`, `InMemoryRawStatementExecutor`

Rather than emit a query-language string, the emitter encodes the operation as an **op-code** in `DataStatement.Statement` and packs the resolved type, key, and instance references into `DataStatement.Parameters`:

```csharp
EmitAdd → DataStatement(
    Statement: "ADD_NODE",
    Parameters: { "__type": typeof(T), "__key": key, "__node": node })

EmitConnect → DataStatement(
    Statement: "ADD_EDGE",
    Parameters: { "__startType": ..., "__startKey": ..., "__edgeType": ...,
                  "__edge": ..., "__endType": ..., "__endKey": ... })
```

`InMemoryRawStatementExecutor` switches on `statement` against the op-code constants exposed on `InMemoryStatementEmitter` and dispatches to the corresponding store method, projecting reads under the conventional `"n"` / `"r"` / `"count"` aliases that `NodeSet<T>` / `EdgeSet<T>` expect.

`EmitTraversal` deliberately throws — the in-memory provider's traversal does not go through `IStatementEmitter`, it goes through `ITraversalTranslator` directly (next section).

The raw-statement escape hatch isn't useful for the in-memory provider in the way it is for Neo4j (there's no Cypher to drop down to), so the conformance suite skips the four raw-Cypher tests when running against in-memory.

### 11.4 Materialization: `InMemoryResultMaterializer`

Trivial. Rows already carry the live CLR instance under the alias, so materialization is a typed cast. The five interface methods all funnel through one `Cast<T>` helper; the alias-free typed-read methods use the provider's internal `NodeAlias` / `EdgeAlias` / `CountAlias` constants. Returning the same instance the consumer stored means **reference identity is preserved** across reads — handy for tests that assert on shared mutable state.

### 11.5 Transactions: snapshot-based

`InMemoryGraphTransaction` holds a private `Clone()` of the canonical store. Every read and write inside the transaction targets the clone; on commit, the canonical store's contents are replaced (`ReplaceWith`) with the clone's. On rollback or dispose-without-commit, the clone is discarded.

`InMemoryRawStatementExecutor.Target` picks which store to operate on:

```csharp
private InMemoryStore Target =>
    opener.Active is InMemoryGraphTransaction transaction ? transaction.Store : opener.Canonical;
```

Same one-ambient-at-a-time invariant as the Neo4j provider.

### 11.6 Traversal: `InMemoryTraversalTranslator`

The in-memory provider does **not** emit a query and execute it — there is no query language. Instead, the translator interprets the `TraversalAst` directly:

1. **Walk the pattern.** Starting from the first segment (always a node), enumerate every node of that type. For each candidate, walk the linear segment list: a relationship segment plus its end-node segment together project to "edges incident on the currently-bound node, in the requested direction, with the right edge type, whose far endpoint is a node of the right type." Recursion produces a stream of *bindings* (`Dictionary<string, object> binding` of alias → CLR instance).
2. **Filter by predicates.** For each binding, evaluate `ast.Predicates` recursively. `PropertyComparisonPredicate` uses `Equals` for `==`/`!=` and `Comparer<object>.Default.Compare` for ordered operators. `StringComparisonPredicate` calls the native `string` methods. `NullPredicate` checks `is null`. `And`/`Or`/`Not` recurse with the obvious short-circuiting.
3. **Project the return alias.** Pull the value bound at `ast.ReturnAlias` from the surviving binding and cast to `TResult`.

`Match` returns the surviving bindings. `Merge` returns matches if any exist, otherwise creates the pattern from scratch and returns the freshly-created binding. `Create` always synthesises new node and edge instances along the pattern and persists them.

The translator deliberately doesn't go through `IStatementEmitter` — there'd be no benefit, since the emitted op-code would just be "here's the AST, evaluate it" and then the executor would call back into the translator. Bypassing the round-trip is one of the conveniences a provider gets when it owns its own translation pipeline end-to-end.

---

## 12. Diagnostics surface

The framework emits OpenTelemetry-compatible tracing and metrics via the .NET BCL's `System.Diagnostics.ActivitySource` and `System.Diagnostics.Metrics.Meter` APIs. There is no dependency on the OpenTelemetry SDK — consumers add `OpenTelemetry` packages on their side and subscribe to the framework's source and meter names. Three layers compose:

- **Framework spans** (`OnixLabs.ElementFramework`): SaveChanges, BeginTransaction, Commit, Rollback.
- **Provider spans** (`OnixLabs.ElementFramework.Neo4j`, `OnixLabs.ElementFramework.AGE`): ExecuteStatement, TranslateTraversal.
- **Driver spans** (Neo4j.Driver, Npgsql): wire-layer round-trips. Emitted by the underlying drivers, not by us.

The in-memory provider does not instrument — there is no IO to measure.

### 12.1 Source names and conventions

Every project that emits spans exposes a `*Diagnostics` static class. Its public `SourceName`, `MeterName`, and `Version` constants give consumers the strings to register:

```csharp
tracerProviderBuilder
    .AddSource(ElementFrameworkDiagnostics.SourceName)
    .AddSource(Neo4jDiagnostics.SourceName)
    .AddSource(AgeDiagnostics.SourceName);

meterProviderBuilder
    .AddMeter(ElementFrameworkDiagnostics.MeterName)
    .AddMeter(Neo4jDiagnostics.MeterName)
    .AddMeter(AgeDiagnostics.MeterName);
```

The `ActivitySource` and `Meter` instances themselves are `internal` — consumers don't construct them, just subscribe by name.

### 12.2 Framework spans

| Operation name | Kind | Tags |
| --- | --- | --- |
| `ElementFramework.SaveChanges` | Internal | `elementframework.operation.count`, `elementframework.transaction.mode` (`ambient` \| `auto`), `exception.type` (on error) |
| `ElementFramework.BeginTransaction` | Internal | `exception.type` (on error) |
| `ElementFramework.Transaction.Commit` | Internal | `exception.type` (on error) |
| `ElementFramework.Transaction.Rollback` | Internal | `exception.type` (on error) |

The auto-open transaction inside `ChangeTracker.Flush` is deliberately invisible to the BeginTransaction/Commit spans — it bypasses `GraphTransactionFactory` so the change tracker can preserve pending operations across an auto-flush rollback (see [§5.4](#54-change-tracking-changetracker)). Only consumer-initiated transactions show the explicit BeginTransaction → Commit/Rollback hierarchy.

### 12.3 Provider spans

| Provider | Operation name | Kind | Tags |
| --- | --- | --- | --- |
| Neo4j | `Neo4j.ExecuteStatement` | Client | `db.system=neo4j`, `elementframework.parameter.count`, `elementframework.transaction.mode`, `exception.type` (on error) |
| Neo4j | `Neo4j.TranslateTraversal` | Internal | `db.system=neo4j`, `elementframework.traversal.kind`, `elementframework.traversal.segment_count`, `elementframework.traversal.predicate_count`, `elementframework.traversal.return_alias` |
| AGE | `AGE.ExecuteStatement` | Client | `db.system=postgresql`, `elementframework.parameter.count`, `elementframework.transaction.mode`, `exception.type` (on error) |
| AGE | `AGE.TranslateTraversal` | Internal | `db.system=postgresql`, `elementframework.traversal.kind`, `elementframework.traversal.segment_count`, `elementframework.traversal.predicate_count`, `elementframework.traversal.return_alias` |

`ExecuteStatement` covers only the open-cursor / open-reader phase. Streaming continuation is intentionally outside the span — the driver's own `ActivitySource` emits the wire-layer fetches, and keeping our span tight to the actual round-trip avoids spans that hang for the duration of a long enumeration.

### 12.4 Counters

| Meter | Counter name | Tags |
| --- | --- | --- |
| Framework | `elementframework.savechanges.flushes` | `outcome` (`success` \| `failure` \| `noop`), `elementframework.transaction.mode` |
| Framework | `elementframework.savechanges.operations` | `elementframework.transaction.mode` |
| Framework | `elementframework.transactions.terminals` | `outcome` (`committed` \| `rolledback` \| `commit_failed` \| `rollback_failed` \| `disposed_without_terminal`) |
| Neo4j | `elementframework.neo4j.statements` | `elementframework.transaction.mode`, `outcome` |
| AGE | `elementframework.age.statements` | `elementframework.transaction.mode`, `outcome` |

### 12.5 PII discipline

Spans and counters attach only **structural metadata** — counts, kinds, modes, alias names, outcome tags. Never:

- Parameter values (they go through `IRawStatementExecutor.Execute` typed as `object?` and may contain PII).
- Full statement bodies (the framework's emitted Cypher and SQL is template-stable, but consumer-passed raw statements may contain user data).
- Property values returned from the database.

The xmldoc on every `*Diagnostics` class restates this discipline. New tags added to existing spans should be reviewed against it.

---

## 13. Testing architecture

The test projects mirror the places functionality lives — pure code lives in unit tests, behaviour every provider must satisfy lives in the conformance suite, and each provider has its own integration project that runs the suite end-to-end.

### 13.1 Unit tests

Two unit-test projects cover the pure code.

**`OnixLabs.ElementFramework.UnitTests`** — xUnit tests against the default implementation. Cover:

- `ChangeTracker` — staging, identity map, flush atomicity, auto-open vs ambient routing, reset, partial-failure pending preservation.
- `GraphModel` / `GraphModelBuilder` — validation, ignored properties, label collisions, key resolution.
- `NodeBuilder` / `RelationshipBuilder` — fluent surface, configuration application.
- `PredicateTranslator` — every operator path plus negative cases.
- `GraphTraversal` — pattern accumulation, segment ordering, predicate accumulation, alias scoping.
- `RollbackAwareGraphTransaction` — every reset-on-dispose path.
- DI registration via `ServiceCollectionExtensions`.

The fixture file `TestFixtures.cs` ships a tiny `Author`/`Post`/`Comment` plus `Wrote`/`CommentOn` model plus fakes for every provider seam (`FakeStatementEmitter`, `FakeRawStatementExecutor`, `FakeGraphTransactionOpener`, `FakeTraversalTranslator`, `FakeResultMaterializer`), letting every unit test inject any subset of the provider contract.

**`OnixLabs.ElementFramework.Neo4j.UnitTests`** — xUnit tests against the Neo4j provider's pure functions. Cover:

- `CypherIdentifier.Escape` — bare-identifier passthrough, reserved-word backtick-quoting (case-insensitive), non-bare quoting, embedded-backtick doubling, null/empty rejection.
- `PropertySerializer.Serialize` — `Guid` → string, `DateTimeOffset` → `ZonedDateTime`, enum → name, primitives and `DateTime` passthrough, null passthrough.
- `ParameterBinder` — `$`-prefixed token on first use, collision resolution with `_N` suffix, deterministic ordering, `ToParameters` snapshot, blank-name rejection.
- `CypherEmitter` — every emit method (`EmitAdd`/`EmitUpdate`/`EmitRemove`/`EmitMerge`/`EmitConnect`/`EmitDisconnect`/`EmitFindById`/`EmitExists`/`EmitAsEnumerableNodes`/`EmitAsEnumerableEdges`) plus full `EmitTraversal` coverage of pattern direction emission, every comparison and string-comparison operator, null predicates, And/Or/Not parenthesisation, top-level conjunction joining, and the AST-validation throws.
- `Neo4jResultMaterializer` — `Guid`/`DateTimeOffset`/enum conversions, primitive and long→int passthrough, nullable `Guid?`, default-on-missing properties, INode/IRelationship type assertions, alias-free and alias-bearing materialize methods, `ReadExists` with positive/zero/negative/non-long/missing counts. Uses inline fake `INode`/`IRelationship` implementations.

**`OnixLabs.ElementFramework.AGE.UnitTests`** — xUnit tests against the AGE provider's pure functions. Cover:

- `CypherIdentifier.Escape` — same coverage as the Neo4j provider, mirroring the duplicated code path.
- `AgePropertySerializer.Serialize` — `Guid` → string, `DateTimeOffset` / `DateTime` → ISO-8601 round-trip, enum → name, primitives passthrough, null passthrough.
- `AgeParameterBinder` — `$`-prefixed token on first use, collision resolution with `_N` suffix, deterministic ordering, blank-name rejection, `ToParameters` snapshot.
- `AgtypeWriter` — empty dict → `{}`, string / boolean / null / every integer-family primitive / double / decimal / insertion-order preservation, throws for CLR types that should have been pre-flattened by `AgePropertySerializer`.
- `AgtypeReader` — scalar string / integer / fractional / boolean / null parsing, vertex and edge entity parsing (label + properties), empty-properties handling, throws for malformed JSON / missing kind tag / unsupported kind / nested-object property. The integer test explicitly asserts `long` (not `double`) to lock in the `(object)` cast that the conformance suite uncovered.
- `AgeCypherEmitter` — every emit method, the `SELECT * FROM ag_catalog.cypher('graph', $$...$$, @p) AS (alias agtype)` wrapping, graph-name escaping (single-quote doubling), `cnt` alias for existence (the `count`-as-SQL-reserved-word workaround), `MERGE ... SET` without `ON CREATE SET / ON MATCH SET` (AGE 1.6.0 doesn't implement them), `@p` omission when no parameters are bound, ORDER BY / SKIP / LIMIT tail emission, every comparison and string-comparison operator, null predicate emission without binding values, AST-validation throws.
- `AgeResultMaterializer` — agtype-vertex and agtype-edge parsing under the conventional `n` / `r` aliases plus explicit `MaterializeNodeAt` / `MaterializeEdgeAt`, every Convert path (Guid, DateTimeOffset, DateTime, enum, long→int, nullable Guid, bool/double primitives), default-on-missing properties, kind-mismatch throws (vertex under edge alias, edge under node alias), and `ReadExists` with positive / zero / negative / fractional / missing / non-string `cnt` values.

All three unit-test projects run without any external dependency.

### 13.2 Conformance suite: `OnixLabs.ElementFramework.Conformance`

A non-test library that ships:

- `IntegrationTestBase` — abstract xUnit lifetime base wiring a DI container, scope creation, and async init/dispose hooks.
- `AbstractGraphContextIntegrationTests` — a provider-agnostic `[Fact]` suite covering every public consumer operation against the blog-application fixture (Author/Post/Comment plus Wrote/CommentOn/ReplyTo). Per-test reset routes through a `ResetGraphAsync` template method whose default implementation issues `MATCH (n) DETACH DELETE n` via the raw executor; non-Cypher providers override it.
- `TestFixtures/BlogApplication/...` — the canonical model: three node types, three relationship types (including a marker edge `CommentOn` with no properties and a reflexive edge `ReplyTo` between two `Comment`s).

Each concrete provider integration project (`InMemory.IntegrationTests`, `Neo4j.IntegrationTests`, `AGE.IntegrationTests`) subclasses `AbstractGraphContextIntegrationTests` once with a sealed class whose only responsibilities are `ConfigureServices` (DI registration with the provider's `Use*` extension) and overriding `ResetGraphAsync` if needed. Tests inherit; coverage is uniform; a new provider gets the full suite by adding ten lines.

The in-memory and AGE projects each skip four conformance tests that assert on raw-Cypher semantics — the in-memory provider doesn't speak Cypher at all, and AGE's raw surface is SQL-wrapped Cypher rather than bare Cypher.

### 13.3 Provider integration tests

- `OnixLabs.ElementFramework.InMemory.IntegrationTests` — runs the conformance suite in-process against a fresh in-memory store registered per test.
- `OnixLabs.ElementFramework.Neo4j.IntegrationTests` — runs the same conformance suite against a real Neo4j 5.x container managed by Testcontainers. Slow but real.
- `OnixLabs.ElementFramework.AGE.IntegrationTests` — runs the same conformance suite against an Apache AGE container (`apache/age:release_PG16_1.6.0`) managed by Testcontainers. The same project also hosts the agtype reconnaissance probes (`AgeReconnaissanceTests`, `AgeReconnaissanceFixture`) that established how the agtype wire format behaves through Npgsql — these are retained as living documentation of the decisions encoded in the provider.

### 13.4 CI

`.github/workflows/ci.yml` runs six test steps in sequence: build → abstraction unit tests → Neo4j-provider unit tests → AGE-provider unit tests → in-memory conformance → Neo4j conformance → AGE conformance (the unit and in-memory passes fail fast on a regression before Docker is even started, shaving minutes off failed PRs). The publish step packs all five NuGet artefacts.

---

## 14. Adding a new provider

A new provider needs:

1. **A project that depends on `OnixLabs.ElementFramework`.** The provider doesn't need to reference `OnixLabs.ElementFramework.Abstractions` directly — `OnixLabs.ElementFramework` transitively brings it.
2. **Five implementations of the provider contract** (see [§6](#6-provider-contract)): `IStatementEmitter`, `IResultMaterializer`, `IRawStatementExecutor`, `IGraphTransactionOpener` (which mints `IGraphTransaction` instances), and `ITraversalTranslator`. The translator can either route back through the emitter+executor+materializer pipeline (Neo4j and AGE follow this model) or interpret the AST directly (in-memory's model).
3. **A `Use<YourProvider>(GraphContextOptionsBuilder, ...)` extension** that constructs the five services and supplies them via `UseStatementEmitter`, `UseResultMaterializer`, `UseRawStatementExecutor`, `UseGraphTransactionOpener`, `UseTraversalTranslator`. The extension is the only consumer-visible API of the provider.
4. **A `<YourProvider>.IntegrationTests` project** that subclasses `AbstractGraphContextIntegrationTests` from the conformance suite. Two methods (`ConfigureServices` + `ResetGraphAsync`) is the typical footprint.

Conventions to honour:

- **Typed-read row shape is private to your provider.** The framework calls `MaterializeNode` / `MaterializeEdge` / `ReadExists` without an alias. Your emitter and materializer agree internally on what shape the row takes — a single-entity-under-alias style, a flat-property-cells style, or anything else that round-trips cleanly.
- **Traversal aliases are passed through.** `MaterializeNodeAt` / `MaterializeEdgeAt` receive the consumer's `Return<T>(alias)` argument and must look up the entity at that alias in the row.
- **Parameter naming:** values are referenced via `Parameters` keys without any provider prefix. The framework hands the dictionary back to your executor as-is.
- **Transactional routing:** the executor must consult `opener.Active` to decide whether to run inside the ambient transaction or open a fresh auto-commit scope. The framework will not pass the transaction handle through.
- **One ambient at a time:** the opener must throw `GraphTransactionAlreadyActiveException` when `Open()` is called and `Active` is non-null.

The architecture is well-shaped for Cypher-family stores — Neo4j (Bolt) and Apache AGE (Postgres + agtype) are both built against this seam. For non-Cypher backends (Gremlin, SPARQL), the predicate tree and traversal AST work, but the linear segment list assumes a Cypher-style path. See [§15](#15-known-constraints-and-non-goals).

---

## 15. Known constraints and non-goals

This is the architectural counterpart to [`docs/production-readiness.md`](production-readiness.md), which is the operational view. Read both together.

**Cypher-family by design.**
The traversal AST is a linear segment list; predicates are a tree that translates cleanly to Cypher's `WHERE`; the materializer reads the row shape its emitter agreed to. The AGE provider has demonstrated this works across wire formats as different as Bolt and Postgres-over-Npgsql. Gremlin (step-based) and SPARQL (triple-based) would still require generalising the AST shape. v1 scope is "Cypher-speaking stores."

**One ambient transaction at a time.**
Nested transactions are not supported. Concurrent transactions on a single context are not supported. A consumer that wants parallelism instantiates multiple contexts.

**No connection pooling abstraction.**
Each provider owns its own connection-management strategy (Neo4j caches drivers process-wide; AGE caches Npgsql data sources process-wide; in-memory has nothing to pool). The framework does not standardize lifetime, eviction, or health.

**Logging, tracing, and metrics; no health probes yet.**
The framework consumes `ILoggerFactory` (set via `GraphContextOptionsBuilder.UseLoggerFactory` or injected through the SP-aware `AddGraphContext` overload) and writes diagnostic events: every Cypher statement at `Debug` (with parameter count, not values), every transaction open / commit / rollback / best-effort-rollback-on-dispose at `Information`, every previously-swallowed dispose-path exception at `Warning`, every `ChangeTracker.Flush` start/end at `Debug` (rollback events at `Warning`). OpenTelemetry-compatible tracing and metrics are also shipped — see [§12](#12-diagnostics-surface). There are no health probes yet.

**Sync-over-async surface (Neo4j and AGE).**
Both the Neo4j driver and Npgsql are async-first; the providers' sync surfaces bridge via `GetAwaiter().GetResult()`. This deadlocks under hosts that capture a synchronization context (ASP.NET Classic, WinForms, WPF). Use the async surface in those hosts. ASP.NET Core, console, and modern hosted-service consumers are unaffected. **The sync surface is intentional and stays.** The abstraction must accommodate providers that ship sync-only, async-only, or both — removing sync would exclude an entire class of provider. The deadlock vector is therefore a documented footgun, not a defect; a Roslyn analyzer to flag the sync surface under sync-context-capturing project SDKs is the next planned mitigation.

**Provider-author types share the abstractions namespace with consumer types.**
`OnixLabs.ElementFramework.Abstractions` contains both consumer-facing types (`GraphContext`, `INodeSet<T>`, `IGraphTraversal`, …) and provider-author types (`IStatementEmitter`, `IResultMaterializer`, `TraversalAst`, …). There is no `.Provider` sub-namespace. The visibility burden is carried by `[EditorBrowsable(EditorBrowsableState.Advanced)]` on provider-author types, which hides them from default auto-complete while leaving the namespace flat. This is a deliberate choice: a sub-namespace would force every provider (and every using-statement in provider integration tests) into a churn we judged not worth the auto-complete win.

**Async path streams; sync path materializes eagerly (Neo4j and AGE).**
`ExecuteAsync` is a genuine async iterator — both providers yield rows as the consumer pulls them, and the underlying cursor / reader / session / connection is held via `await using` for the lifetime of the enumerator. The consequence is a behaviour split: open-time failures (the initial cursor / reader call) still surface as `RawStatementException`, but mid-stream failures during enumeration propagate as raw driver exceptions. The sync `Execute` surface drains the same stream into a list before returning, so sync consumers see the pre-streaming exception model.

**In-memory provider is not for production.**
No thread-safety on the store, no eviction in the registry, no persistence, no query optimization. It exists for unit-test scenarios in consumer code and for keeping the abstraction honest about non-Cypher providers.

**Identity-map / rollback asymmetry on auto-flush failure.**
`TrackRemove` updates the identity map at track time, not flush time. If the auto-opened transaction rolls back, the identity map will report a node as removed while the database still has it. The pending queue is preserved on failure for retry; the identity map is not.
