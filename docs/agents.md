# Agent Onboarding

The starting point for AI agents working in this repository. This file is the only documentation that lives in the repository — everything else (architecture, per-provider deep dives, the runnable example) lives in the project [wiki](https://github.com/onix-labs/onixlabs-dotnet-element-framework/wiki). Known gaps and future work live in the [issue tracker](https://github.com/onix-labs/onixlabs-dotnet-element-framework/issues) under the `tech-debt`, `roadmap`, and `production-readiness` labels — consult them before suggesting "improvements" that are already on the list.

## Contents

1. [Orientation](#1-orientation)
2. [Working in this repo](#2-working-in-this-repo)
3. [Coding style](#3-coding-style)
4. [XML documentation conventions](#4-xml-documentation-conventions)
5. [Common gotchas](#5-common-gotchas)
6. [Where things live](#6-where-things-live)

---

## 1. Orientation

**What this is.** An Object-Graph Mapper (OGM) for .NET. Consumer code declares CLR types as nodes/edges, the framework owns an identity map and a queue of pending changes, and `SaveChanges` translates them into provider-native statements wrapped in a transaction.

**Six production projects.** `Abstractions` (contracts only), `OnixLabs.ElementFramework` (default impl: change tracker, model, sets, traversal, DI), `OnixLabs.ElementFramework.Neo4j` (Cypher-over-Bolt provider), `OnixLabs.ElementFramework.AGE` (Cypher-over-Npgsql provider for Apache AGE on Postgres), `OnixLabs.ElementFramework.Arango` (AQL-over-HTTP provider for ArangoDB — the framework's first non-Cypher provider), `OnixLabs.ElementFramework.InMemory` (in-process provider for tests/demos).

**Eight test projects.** `UnitTests` (against the default impl), `Neo4j.UnitTests` / `AGE.UnitTests` / `Arango.UnitTests` (provider-pure functions), `Conformance` (a library of provider-agnostic integration tests, subclassed per provider), and `<Provider>.IntegrationTests` (one per provider, runs the conformance suite end-to-end).

**Dependency direction is strictly inward.** Providers reference `OnixLabs.ElementFramework` (which transitively pulls `Abstractions`). Provider-to-provider references do not exist. Don't introduce them.

---

## 2. Working in this repo

### Build & test commands

```bash
dotnet build onixlabs-dotnet-element-framework.slnx

# Unit tests (fast, no external deps)
dotnet test OnixLabs.ElementFramework.UnitTests/OnixLabs.ElementFramework.UnitTests.csproj --no-build
dotnet test OnixLabs.ElementFramework.Neo4j.UnitTests/OnixLabs.ElementFramework.Neo4j.UnitTests.csproj --no-build
dotnet test OnixLabs.ElementFramework.AGE.UnitTests/OnixLabs.ElementFramework.AGE.UnitTests.csproj --no-build
dotnet test OnixLabs.ElementFramework.Arango.UnitTests/OnixLabs.ElementFramework.Arango.UnitTests.csproj --no-build

# In-memory conformance (fast, no external deps)
dotnet test OnixLabs.ElementFramework.InMemory.IntegrationTests/OnixLabs.ElementFramework.InMemory.IntegrationTests.csproj --no-build

# Neo4j integration (needs Docker daemon for Testcontainers; slow)
dotnet test OnixLabs.ElementFramework.Neo4j.IntegrationTests/OnixLabs.ElementFramework.Neo4j.IntegrationTests.csproj --no-build

# Apache AGE integration (needs Docker daemon for Testcontainers; slow)
dotnet test OnixLabs.ElementFramework.AGE.IntegrationTests/OnixLabs.ElementFramework.AGE.IntegrationTests.csproj --no-build

# ArangoDB integration (needs Docker daemon for Testcontainers; slow)
dotnet test OnixLabs.ElementFramework.Arango.IntegrationTests/OnixLabs.ElementFramework.Arango.IntegrationTests.csproj --no-build
```

Run the in-memory conformance suite before the Dockerised ones — it catches abstraction-layer regressions without spinning up a container. The CI workflow does this automatically.

### Targets

Multi-targets **net8.0**, **net9.0**, **net10.0**. The build runs every target; a change that only compiles on one is a bug.

### Public surface discipline

Outside `Abstractions`, only these are public:

- `GraphContext` (abstract base for consumer contexts)
- `GraphContextOptionsBuilder` and its `Use*` extension methods (`UseNeo4j`, `UseAge`, `UseArango`, `UseInMemory`, …)
- `ServiceCollectionExtensions.AddGraphContext<T>`
- `InMemoryStoreRegistry` (process-wide store registry — public for test reset hooks)
- The full exception hierarchy

**Everything else is `internal sealed`.** When adding a class, default to `internal sealed`. Promote to `public` only with a concrete consumer-facing reason. Inheritance is rare — the framework's only inheritance hierarchy is `GraphContext` (and the exception types). Avoid adding more.

### Documentation discipline

The codebase has thorough xmldoc on every type and member (including internal ones — see [§4](#4-xml-documentation-conventions)). New code is expected to match. Don't merge a change that loses doc coverage.

---

## 3. Coding style

Distilled from Uncle Bob's *Clean Code*, scoped to what's prescriptive in this repo. Use this list when writing new code or reviewing changes.

### Naming

- **Intention-revealing.** `elapsedDays`, not `d`. `customer`, not `data`. The name should answer *why this exists* without a comment.
- **No disinformation.** Don't name something `List` if it's a `Dictionary`. Don't name something `Manager` if you can name what it actually does.
- **Class names are nouns.** `Customer`, `ChangeTracker`. Avoid `Manager`, `Handler`, `Helper`, `Data`, `Info`.
- **Method names are verbs.** `PostPayment`, `DeletePage`, `TrackAdd`. Booleans read as predicates: `IsActive`, `HasPendingChanges`.
- **Pronounceable and searchable.** `creationTimestamp`, not `genymdhms`. Single-letter identifiers are fine only as loop indices or generic-type parameters.

### Functions / methods

- **Small.** Aim for under ~20 lines. If a method needs comments to explain its sections, those sections should be methods.
- **Do one thing.** A method either does work, decides something, or fetches something — not all three.
- **One level of abstraction per method.** Don't mix high-level orchestration with low-level details (regex parsing, byte manipulation) in the same body.
- **Few arguments.** 0–2 is preferred. 3 needs a reason. 4+ almost always indicates a missing type.
- **No side effects in pure-sounding methods.** `CalculateTotal` should not mutate state. `Save` is allowed to.

### Classes

- **Single responsibility.** A class that has more than one reason to change has two responsibilities. Split it.
- **Stepdown rule / newspaper metaphor.** Public API at the top, helpers it calls below it, helpers those call below them. A reader scrolling top-to-bottom moves from "what" to "how".
- **Vertical density.** Related lines stay together; unrelated blocks get a blank line. Variables are declared at their first use, not all at the top.

### Comments

- **Don't comment bad code — rewrite it.** Most comments are a sign of failure to express ourselves in code.
- **Explain *why*, not *what*.** Names tell what; comments tell why. `// Workaround for driver bug #1234: the cursor stalls if we drain it eagerly.` is worth keeping. `// increment i` is not.
- **Acceptable comments:** legal headers, regex intent, intentional non-obvious behaviour, external-API quirks, `TODO` with a ticket reference.
- **Unacceptable:** redundant restatements, position markers (`// ---- helpers ----`), commented-out code, mumbling.

### Error handling

- **Throw, don't return error codes.** Exceptions keep the happy path clean.
- **Don't return null where a collection is expected.** Return an empty sequence.
- **Don't pass null where the API requires a value.** Validate at the boundary with `ArgumentNullException.ThrowIfNull` / `ArgumentException.ThrowIfNullOrWhiteSpace`.
- **Wrap provider exceptions at the seam.** A Neo4j driver exception thrown out of `Neo4jCypherExecutor.Execute` should surface as a `RawStatementException` whose inner is the driver's exception. Same pattern for `StatementEmissionException`, `TraversalTranslationException`, `GraphTransactionException`.

### Tests

- **F.I.R.S.T.**: *Fast* (the unit suite runs in seconds), *Independent* (no order coupling), *Repeatable* (no clock or network in unit tests), *Self-validating* (assertions, not prints), *Timely* (write the test before or alongside the code, not after).
- **Conformance tests cover behaviour every provider must satisfy.** When adding a feature that crosses the provider seam, write the conformance test before writing the provider code. Both `Neo4j.IntegrationTests` and `InMemory.IntegrationTests` should pick it up automatically.
- **Unit tests cover the abstraction layer.** Use the fakes in `TestFixtures.cs` (`FakeStatementEmitter`, `FakeRawStatementExecutor`, `FakeTraversalTranslator`, …) to inject any subset of the provider contract.

### Smells to flag

Rigidity (small change cascades widely), fragility (changes break unrelated tests), immobility (a useful piece can't be lifted out without dragging dependencies), needless complexity, needless repetition.

---

## 4. XML documentation conventions

Every public and internal member is documented. The conventions below match StyleCop's SA1600-series rules and the existing prose style in the codebase. New or modified code is expected to keep coverage and follow these patterns.

### General prose rules

- Every doc block begins with a capital letter and ends with a full stop.
- Use `<see cref="..."/>` for type references, `<paramref name="..."/>` for parameters, `<typeparamref name="..."/>` for type parameters, `<see langword="..."/>` for `null` / `true` / `false`.
- Don't paste identical summaries across unrelated members. If two members genuinely share docs, use `<inheritdoc/>`.
- High-level overview is enough for most members; reserve detail for hidden constraints, subtle invariants, or non-obvious behaviour.

```csharp
/// <summary>
/// Returns the customer matching <paramref name="id"/>, or <see langword="null"/> if none exists.
/// </summary>
```

### Types

Type summaries begin with a verb that signals the kind:

- Classes, structs, records → **"Represents..."**
- Interfaces and delegates → **"Defines..."**
- Enums → **"Specifies..."**

```csharp
/// <summary>
/// Represents a customer in the billing system.
/// </summary>
public sealed class Customer { }

/// <summary>
/// Defines the contract for a payment processor.
/// </summary>
public interface IPaymentProcessor { }

/// <summary>
/// Specifies the status of an order.
/// </summary>
public enum OrderStatus { }
```

### Constructors

Instance constructors begin with **"Initializes a new instance of the `<see cref="TypeName"/>` class."** (or `struct` for structs/record structs).

```csharp
/// <summary>
/// Initializes a new instance of the <see cref="Customer"/> class.
/// </summary>
public Customer() { }
```

Generic types use the curly-brace form: `<see cref="Repository{T}"/>`. Static constructors begin with **"Initializes static members of the `<see cref="TypeName"/>` class."**

### Finalizers

```csharp
/// <summary>
/// Finalizes an instance of the <see cref="Resource"/> class.
/// </summary>
~Resource() { }
```

### Properties

Property summaries match the visible accessors:

- Get only → **"Gets ..."**
- Set only → **"Sets ..."**
- Get and set → **"Gets or sets ..."**
- Get and init → **"Gets ..."** (acceptable) or **"Gets or initializes ..."**

Boolean properties use the special form **"Gets/Sets/Gets or sets a value indicating whether ..."**

If the setter is less accessible than the getter (`public get; private set;`), omit the setter from the wording.

A `<value>` tag describing what the property holds is preferred.

```csharp
/// <summary>
/// Gets or sets the display name of the customer.
/// </summary>
/// <value>The customer's full display name, never <see langword="null"/>.</value>
public string Name { get; set; }

/// <summary>
/// Gets a value indicating whether the customer is active.
/// </summary>
public bool IsActive { get; }
```

### Methods

- Every parameter gets a `<param name="...">` with non-empty prose. Names must match exactly.
- Every generic parameter gets a `<typeparam name="...">`.
- Non-void methods get a `<returns>` tag whose text **begins with "Returns"**. Void methods (and `Task`-returning methods with no result) get no `<returns>` tag.
- Thrown exceptions get `<exception cref="...">` tags.

```csharp
/// <summary>
/// Calculates the total price including tax.
/// </summary>
/// <param name="subtotal">The pre-tax subtotal.</param>
/// <param name="taxRate">The tax rate as a decimal between 0 and 1.</param>
/// <returns>Returns the total price including tax.</returns>
/// <exception cref="ArgumentOutOfRangeException">
/// Thrown when <paramref name="taxRate"/> is negative or greater than 1.
/// </exception>
public decimal CalculateTotal(decimal subtotal, decimal taxRate) { }
```

### Events

Begin with **"Occurs when ..."**

```csharp
/// <summary>
/// Occurs when the customer's name changes.
/// </summary>
public event EventHandler<NameChangedEventArgs> NameChanged;
```

### Enums

Every enum member is documented.

```csharp
/// <summary>
/// Specifies the status of an order.
/// </summary>
public enum OrderStatus
{
    /// <summary>The order has been created but not yet submitted.</summary>
    Pending,

    /// <summary>The order has been submitted and is awaiting fulfilment.</summary>
    Submitted,
}
```

### Inheriting documentation

Use `<inheritdoc/>` for interface implementations and overrides. Don't apply it to members that don't actually inherit or implement anything.

```csharp
/// <inheritdoc/>
public override string ToString() => name;
```

### Avoid

- Default Visual Studio placeholder text ("Summary description for …").
- Empty tags. Empty `<summary>` is worse than no `<summary>`.
- `<placeholder>` — it's the unwritten-documentation marker; StyleCop flags it.
- `///` for non-documentation comments.
- Identical summary text copy-pasted across unrelated members. If two members really do share docs, use `<inheritdoc/>`.

### Repo-specific rules

1. **All members including `private` members are documented.** This is stricter than StyleCop's defaults but it's the existing standard.
2. **`<returns>` text begins with "Returns".** Example: `<returns>Returns the matching customer, or <see langword="null"/> when none exists.</returns>`

---

## 5. Common gotchas

The architectural sharp edges that trip up first-time contributors. Background and rationale for the deeper architectural decisions live in the project [wiki](https://github.com/onix-labs/onixlabs-dotnet-element-framework/wiki).

- **`ModelSource` caches by CLR type for the lifetime of the process.** `OnModelCreating` is invoked exactly once per `GraphContext` subclass. Don't put non-pure logic in it. Tests that hot-reload context types will surprise themselves.
- **`ChangeTracker.TrackRemove` updates the identity map at track time, not flush time.** If a flush fails and rolls back, the identity map will report the node as gone while the database still has it. The pending queue is preserved on failure for retry; the identity map is not.
- **`Pending` is cleared only on a successful flush.** A failed flush leaves the queue intact so a corrected retry can replay the batch. Don't write code that assumes `pending` is empty after a `Flush` that threw.
- **Sync-over-async surface in the Neo4j and AGE providers deadlocks under captured sync contexts.** Safe under ASP.NET Core, console, and modern hosted-service consumers. Unsafe under ASP.NET Classic, WinForms, WPF. Use the async surface in those hosts. **The sync surface is intentional and is not being removed** — the abstraction must accommodate providers that ship sync-only, async-only, or both. The mitigation path is a Roslyn analyzer that flags the sync surface under sync-context-capturing project SDKs, not deletion.
- **Provider-author types and consumer types share `OnixLabs.ElementFramework.Abstractions`.** Do not propose a `.Provider` sub-namespace — that was considered and rejected. Hide provider-author types from consumer auto-complete via `[EditorBrowsable(EditorBrowsableState.Advanced)]` instead. The flat-namespace choice was driven by the cost of churning every provider and every using-statement in provider integration tests.
- **Async executors stream; sync executors materialize.** Both Neo4j's and AGE's `ExecuteAsync` are real async iterators — they hold the cursor / reader and the auto-commit session / connection via `await using` and dispose on enumerator dispose. Open-time failures wrap to `RawStatementException`; mid-stream failures during enumeration propagate raw. The sync `Execute` surface drains the same stream into a list before returning, so sync callers continue to see every failure wrapped at execute time. Don't reintroduce eager materialization in the async path — that's the change that fixed the OOM risk on large result sets.
- **`Neo4jDriverCache` is process-wide and never evicts.** Fine in production. In test runs against Testcontainers it accumulates one driver per random port until the process exits.
- **Typed-read row shape is provider-internal.** For framework-emitted reads (`FindById`, `Exists`, `AsEnumerable`), the framework calls `IResultMaterializer.MaterializeNode` / `MaterializeEdge` / `ReadExists` without an alias — the provider's emitter and materializer agree privately on what the row looks like. Don't reintroduce `"n"` / `"r"` / `"count"` hardcoding in framework code; if your work crosses the seam, look at how the Neo4j and in-memory materializers define `NodeAlias` / `EdgeAlias` / `CountAlias` constants alongside their emitter. Traversal returns use `MaterializeNodeAt` / `MaterializeEdgeAt` with the consumer's alias.
- **`InMemoryStore` is not thread-safe.** Concurrent contexts pointing at the same database name will race. The in-memory provider is for tests and small demos.
- **One ambient transaction at a time.** Calling `BeginTransaction` while another is active throws `GraphTransactionAlreadyActiveException`. Nested transactions are not supported.
- **`GraphContextOptions` is public but `[EditorBrowsable(Never)]`.** It exists only because the consumer subclass constructor must accept it. Don't hand-instantiate it — go through `AddGraphContext` and the options builder.
- **Logging is wired; respect the level conventions when adding more.** `ILoggerFactory` is consumed by `ChangeTracker` and every provider's executor / transaction-opener / transaction / connection cache. Convention: emitted statements log at `Debug` (with statement text and parameter count, never values), transaction open/commit/rollback and best-effort-rollback-on-dispose log at `Information`, failures and previously-swallowed dispose-path exceptions log at `Warning`. To inject a `LoggerFactory` from a host, use the SP-aware `AddGraphContext` overload and call `UseLoggerFactory` **before** the provider's `Use*` extension — providers read the factory at composition time.
- **AGE has provider-specific quirks the emitter handles internally.** `cypher()` and the `agtype` type are schema-qualified to `ag_catalog.*` because `search_path` is set via the connection string's `Options=-c search_path=ag_catalog,public` and qualifying isn't expensive. `count` is renamed to `cnt` everywhere — it's a SQL reserved word and AGE rejects it as a column alias in `AS (...)`. `MERGE ... ON CREATE SET / ON MATCH SET` is rewritten to plain `MERGE ... SET` because AGE 1.6.0 doesn't implement the conditional forms (semantics are equivalent when both branches' SET lists are identical, which is what Neo4j emits). Read rows arrive as `string` because `command.AllResultTypesAreUnknown = true` is the only reliable way to pull `agtype` through Npgsql; the materializer parses the JSON-ish body. Parameters flow as a single `@p` with `NpgsqlDbType.Unknown` so AGE's strict third-arg check passes.
- **Arango has provider-specific quirks driven by Newtonsoft.Json defaults and AQL semantics.** The provider installs a custom `IApiClientSerialization` (`ArangoSerialization`) to fix two foot-guns: (1) Newtonsoft's default `DateParseHandling.DateTime` auto-parses ISO-8601 strings into `DateTime` values with the offset lossily converted to local time — the provider pins `DateParseHandling.None` on the deserializer so date strings stay strings and the materializer can run `DateTimeOffset.Parse` with offset preserved; (2) `CamelCasePropertyNamesContractResolver` camel-cases dictionary keys by default, which would rewrite our bind-vars payload's `"WrittenAt"` to `"writtenAt"` — the provider sets `ProcessDictionaryKeys = false` on the naming strategy so dict keys reach the wire verbatim while C# property names still get camel-cased. AQL also has no built-in `ENDS_WITH` in 3.12; the traversal emitter falls back to `SUBSTRING(text, LENGTH(text) - LENGTH(@p)) == @p`. The `_key` / `_from` / `_to` document conventions are framework-mapped: node label = document collection, relationship type = edge collection, framework key property → `_key`, endpoints encoded as `Collection/_key` document IDs.
- **Arango Stream Transactions need collections declared at begin time, but the framework's `ChangeTracker` opens the transaction before the pending queue is drained.** The opener works around it by listing every non-system collection in the bound database at begin time and declaring them all as read+write (plus `AllowImplicit = true`). One extra HTTP call per transaction begin; avoids coupling the opener to `IGraphModel`.
- **Arango requires collections to exist before any write.** Unlike Cypher-family stores, ArangoDB does not auto-create them. Consumers (or test fixtures) call `ArangoSchemaBootstrap.EnsureCollectionsAsync(context, …)` once at startup; it walks `context.Model.Nodes` / `Relationships` and creates any missing collections of the matching document/edge type. This is the reason `IGraphModel.Nodes` / `Relationships` and `GraphContext.Model` are public (the latter marked `[EditorBrowsable(Advanced)]`).
- **`PredicateTranslator` supports a fixed grammar.** Comparison, boolean composition, null checks, string `Contains`/`StartsWith`/`EndsWith` only. Anything else throws `NotSupportedException` and consumers should use `RawStatement.Execute(...)`.
- **Diagnostics spans follow `<Layer>.<Operation>` naming, and span tags never carry PII.** The framework emits `ElementFramework.SaveChanges`, `ElementFramework.BeginTransaction`, `ElementFramework.Transaction.Commit`, `ElementFramework.Transaction.Rollback`. Providers emit `Neo4j.ExecuteStatement` / `Neo4j.TranslateTraversal` and `AGE.ExecuteStatement` / `AGE.TranslateTraversal`. Tags only carry structural metadata — operation counts, parameter counts, transaction mode (`ambient` / `auto`), traversal kind / segment count / predicate count / return alias. Never put parameter values, property values, predicate literals, or statement text into tags or counter dimensions. Statement text is also kept out of tags by design; the provider's wire-layer ActivitySource (Neo4j driver, Npgsql) is the right place for that.
- **Auto-flush does not emit `BeginTransaction` / `Commit` spans.** When `SaveChanges` is called without an ambient transaction, the framework opens, flushes, and commits through the provider's transaction surface directly — it does NOT go through `GraphTransactionFactory`, because auto-flush failure must preserve the pending queue while a factory-opened transaction's rollback would clear it. The `SaveChanges` span still fires (with `elementframework.transaction.mode=auto`); the begin/commit/rollback spans only fire on the explicit-transaction path. Conformance tests assert the full four-span set against an explicit `BeginTransactionAsync` + `SaveChangesAsync` + `CommitAsync` cycle.

---

## 6. Where things live

Quick reference for "I need to change X — what file?"

| If you're changing… | Look at |
| --- | --- |
| The public consumer surface | `GraphContext.cs` (Abstractions), `INodeSet`/`IEdgeSet`/`IGraphTraversal` |
| Identity map / pending queue / flush atomicity | `ChangeTracker.cs` |
| Model validation, label uniqueness, key resolution | `GraphModel.cs`, `GraphModelBuilder.cs`, `NodeBuilder.cs` |
| DI registration / context construction | `ServiceCollectionExtensions.cs`, `GraphContextOptionsBuilder.cs` |
| Transaction lifecycle / rollback semantics | `GraphTransactionFactory.cs`, `RollbackAwareGraphTransaction.cs` |
| Fluent traversal stages | `GraphTraversal.cs`, `PatternStart.cs`, `PatternNode.cs`, `PatternRelationship.cs`, `PatternRelationshipDirected.cs`, `TraversalState.cs` |
| Lambda → predicate tree compilation | `PredicateTranslator.cs` |
| Predicate tree shape / new predicate types | Abstractions: `TraversalPredicate.cs` and its subtypes (`PropertyComparisonPredicate.cs`, `StringComparisonPredicate.cs`, `NullPredicate.cs`, `AndPredicate.cs`, `OrPredicate.cs`, `NotPredicate.cs`) |
| Cypher emission | `CypherEmitter.cs`, `CypherIdentifier.cs`, `ParameterBinder.cs`, `PropertySerializer.cs` |
| Neo4j transactions / driver caching | `Neo4jGraphTransactionOpener.cs`, `Neo4jGraphTransaction.cs`, `Neo4jDriverCache.cs` |
| Neo4j result materialization | `Neo4jResultMaterializer.cs` |
| AGE Cypher emission (cypher() wrapping) | `AgeCypherEmitter.cs`, `CypherIdentifier.cs`, `AgeParameterBinder.cs`, `AgePropertySerializer.cs` |
| AGE Npgsql executor / data-source cache | `AgeRawStatementExecutor.cs`, `AgeDataSourceCache.cs` |
| AGE transactions | `AgeGraphTransactionOpener.cs`, `AgeGraphTransaction.cs` |
| AGE result materialization / agtype parser | `AgeResultMaterializer.cs`, `AgtypeReader.cs`, `AgtypeWriter.cs` |
| ArangoDB AQL emission | `ArangoStatementEmitter.cs`, `ArangoTraversalEmitter.cs`, `ArangoPropertySerializer.cs` |
| ArangoDB client + serializer | `ArangoClientCache.cs`, `ArangoSerialization.cs` |
| ArangoDB executor / transactions | `ArangoRawStatementExecutor.cs`, `ArangoGraphTransactionOpener.cs`, `ArangoGraphTransaction.cs` |
| ArangoDB result materialization | `ArangoResultMaterializer.cs`, `ArangoJsonReader.cs` |
| ArangoDB schema bootstrap | `ArangoSchemaBootstrap.cs` |
| In-memory store / snapshot transactions | `InMemoryStore.cs`, `InMemoryStoreRegistry.cs`, `InMemoryGraphTransaction.cs`, `InMemoryGraphTransactionOpener.cs` |
| In-memory op-coded statements | `InMemoryStatementEmitter.cs`, `InMemoryRawStatementExecutor.cs` |
| In-memory pattern walk / predicate evaluator | `InMemoryTraversalTranslator.cs` |
| Test fixtures (blog application) | `OnixLabs.ElementFramework.Conformance/TestFixtures/BlogApplication/` |
| Conformance suite (provider-agnostic tests) | `OnixLabs.ElementFramework.Conformance/AbstractGraphContextIntegrationTests.cs` |
| Unit-test fakes | `OnixLabs.ElementFramework.UnitTests/TestFixtures.cs` |
| CI pipeline | `.github/workflows/ci.yml` |
| Framework diagnostics surface | `ElementFrameworkDiagnostics.cs` |
| Provider diagnostics surface | `Neo4jDiagnostics.cs`, `AgeDiagnostics.cs`, `ArangoDiagnostics.cs` |

When adding a new provider, the wiki's per-provider pages are the best worked examples — Neo4j is the densest, AGE shows the SQL-wrap pattern, Arango shows the non-Cypher path, In-Memory is the minimum viable shape.
