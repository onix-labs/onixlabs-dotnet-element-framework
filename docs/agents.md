# Agent Onboarding

The starting point for AI agents working in this repository. Read this first, then dive into [architecture.md](architecture.md) for the deep tour. [production-readiness.md](production-readiness.md) lists known gaps and their priorities — consult it before suggesting "improvements" that are already on the list.

## Contents

1. [Orientation](#1-orientation)
2. [Working in this repo](#2-working-in-this-repo)
3. [Coding style](#3-coding-style)
4. [XML documentation conventions](#4-xml-documentation-conventions)
5. [Common gotchas](#5-common-gotchas)
6. [Where things live](#6-where-things-live)

---

## 1. Orientation

**What this is.** An Object-Graph Mapper (OGM) for .NET. Consumer code declares CLR types as nodes/edges, the framework owns an identity map and a queue of pending changes, and `SaveChanges` translates them into provider-native statements wrapped in a transaction. See [architecture.md §1–3](architecture.md#1-10000-foot-view) for the mental model.

**Five production projects.** `Abstractions` (contracts only), `OnixLabs.ElementFramework` (default impl: change tracker, model, sets, traversal, DI), `OnixLabs.ElementFramework.Neo4j` (Cypher-over-Bolt provider), `OnixLabs.ElementFramework.AGE` (Cypher-over-Npgsql provider for Apache AGE on Postgres), `OnixLabs.ElementFramework.InMemory` (in-process provider for tests/demos).

**Six test projects.** `UnitTests` (against the default impl), `Neo4j.UnitTests` and `AGE.UnitTests` (provider-pure functions), `Conformance` (a library of provider-agnostic integration tests, subclassed per provider), and `<Provider>.IntegrationTests` (one per provider, runs the conformance suite end-to-end).

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

# In-memory conformance (fast, no external deps)
dotnet test OnixLabs.ElementFramework.InMemory.IntegrationTests/OnixLabs.ElementFramework.InMemory.IntegrationTests.csproj --no-build

# Neo4j integration (needs Docker daemon for Testcontainers; slow)
dotnet test OnixLabs.ElementFramework.Neo4j.IntegrationTests/OnixLabs.ElementFramework.Neo4j.IntegrationTests.csproj --no-build

# Apache AGE integration (needs Docker daemon for Testcontainers; slow)
dotnet test OnixLabs.ElementFramework.AGE.IntegrationTests/OnixLabs.ElementFramework.AGE.IntegrationTests.csproj --no-build
```

Run the in-memory conformance suite before the Dockerised ones — it catches abstraction-layer regressions without spinning up a container. The CI workflow does this automatically.

### Targets

Multi-targets **net8.0**, **net9.0**, **net10.0**. The build runs every target; a change that only compiles on one is a bug.

### Public surface discipline

Outside `Abstractions`, only these are public:

- `GraphContext` (abstract base for consumer contexts)
- `GraphContextOptionsBuilder` and its `Use*` extension methods (`UseNeo4j`, `UseAge`, `UseInMemory`, …)
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

The architectural sharp edges that trip up first-time contributors. Most are explained in more depth in [architecture.md §14](architecture.md#14-known-constraints-and-non-goals) and [production-readiness.md](production-readiness.md).

- **`ModelSource` caches by CLR type for the lifetime of the process.** `OnModelCreating` is invoked exactly once per `GraphContext` subclass. Don't put non-pure logic in it. Tests that hot-reload context types will surprise themselves.
- **`ChangeTracker.TrackRemove` updates the identity map at track time, not flush time.** If a flush fails and rolls back, the identity map will report the node as gone while the database still has it. The pending queue is preserved on failure for retry; the identity map is not.
- **`Pending` is cleared only on a successful flush.** A failed flush leaves the queue intact so a corrected retry can replay the batch. Don't write code that assumes `pending` is empty after a `Flush` that threw.
- **Sync-over-async surface in the Neo4j provider deadlocks under captured sync contexts.** Safe under ASP.NET Core, console, and modern hosted-service consumers. Unsafe under ASP.NET Classic, WinForms, WPF. Use the async surface in those hosts.
- **Async executors stream; sync executors materialize.** Both Neo4j's and AGE's `ExecuteAsync` are real async iterators — they hold the cursor / reader and the auto-commit session / connection via `await using` and dispose on enumerator dispose. Open-time failures wrap to `RawStatementException`; mid-stream failures during enumeration propagate raw. The sync `Execute` surface drains the same stream into a list before returning, so sync callers continue to see every failure wrapped at execute time. Don't reintroduce eager materialization in the async path — that's the change that fixed the OOM risk on large result sets.
- **`Neo4jDriverCache` is process-wide and never evicts.** Fine in production. In test runs against Testcontainers it accumulates one driver per random port until the process exits.
- **Typed-read row shape is provider-internal.** For framework-emitted reads (`FindById`, `Exists`, `AsEnumerable`), the framework calls `IResultMaterializer.MaterializeNode` / `MaterializeEdge` / `ReadExists` without an alias — the provider's emitter and materializer agree privately on what the row looks like. Don't reintroduce `"n"` / `"r"` / `"count"` hardcoding in framework code; if your work crosses the seam, look at how the Neo4j and in-memory materializers define `NodeAlias` / `EdgeAlias` / `CountAlias` constants alongside their emitter. Traversal returns use `MaterializeNodeAt` / `MaterializeEdgeAt` with the consumer's alias. See [architecture.md §6](architecture.md#6-provider-contract).
- **`InMemoryStore` is not thread-safe.** Concurrent contexts pointing at the same database name will race. The in-memory provider is for tests and small demos.
- **One ambient transaction at a time.** Calling `BeginTransaction` while another is active throws `GraphTransactionAlreadyActiveException`. Nested transactions are not supported.
- **`GraphContextOptions` is public but `[EditorBrowsable(Never)]`.** It exists only because the consumer subclass constructor must accept it. Don't hand-instantiate it — go through `AddGraphContext` and the options builder.
- **Logging is wired; respect the level conventions when adding more.** `ILoggerFactory` is consumed by `ChangeTracker` and every provider's executor / transaction-opener / transaction / connection cache. Convention: emitted statements log at `Debug` (with statement text and parameter count, never values), transaction open/commit/rollback and best-effort-rollback-on-dispose log at `Information`, failures and previously-swallowed dispose-path exceptions log at `Warning`. To inject a `LoggerFactory` from a host, use the SP-aware `AddGraphContext` overload and call `UseLoggerFactory` **before** the provider's `Use*` extension — providers read the factory at composition time.
- **AGE has provider-specific quirks the emitter handles internally.** `cypher()` and the `agtype` type are schema-qualified to `ag_catalog.*` because `search_path` is set via the connection string's `Options=-c search_path=ag_catalog,public` and qualifying isn't expensive. `count` is renamed to `cnt` everywhere — it's a SQL reserved word and AGE rejects it as a column alias in `AS (...)`. `MERGE ... ON CREATE SET / ON MATCH SET` is rewritten to plain `MERGE ... SET` because AGE 1.6.0 doesn't implement the conditional forms (semantics are equivalent when both branches' SET lists are identical, which is what Neo4j emits). Read rows arrive as `string` because `command.AllResultTypesAreUnknown = true` is the only reliable way to pull `agtype` through Npgsql; the materializer parses the JSON-ish body. Parameters flow as a single `@p` with `NpgsqlDbType.Unknown` so AGE's strict third-arg check passes.
- **`PredicateTranslator` supports a fixed grammar.** Comparison, boolean composition, null checks, string `Contains`/`StartsWith`/`EndsWith` only. Anything else throws `NotSupportedException` and consumers should use `RawStatement.Execute(...)`. See [architecture.md §7.3–7.4](architecture.md#73-the-predicate-tree).

---

## 6. Where things live

Quick reference for "I need to change X — what file?"

| If you're changing… | Look at | Cross-reference |
| --- | --- | --- |
| The public consumer surface | `GraphContext.cs` (Abstractions), `INodeSet`/`IEdgeSet`/`IGraphTraversal` | [arch §4](architecture.md#4-consumer-surface) |
| Identity map / pending queue / flush atomicity | `ChangeTracker.cs` | [arch §5.4](architecture.md#54-change-tracking-changetracker) |
| Model validation, label uniqueness, key resolution | `GraphModel.cs`, `GraphModelBuilder.cs`, `NodeBuilder.cs` | [arch §5.1–5.2](architecture.md#51-model-graphmodel-nodemetadata-relationshipmetadata-propertymetadata) |
| DI registration / context construction | `ServiceCollectionExtensions.cs`, `GraphContextOptionsBuilder.cs` | [arch §5.3](architecture.md#53-context-construction-graphcontext--graphcontextoptions--graphcontextservices), [arch §8.1](architecture.md#81-registration--first-call) |
| Transaction lifecycle / rollback semantics | `GraphTransactionFactory.cs`, `RollbackAwareGraphTransaction.cs` | [arch §5.6](architecture.md#56-transaction-lifecycle-graphtransactionfactory-rollbackawaregraphtransaction), [arch §8.3](architecture.md#83-explicit-transaction--rollback-semantics) |
| Fluent traversal stages | `GraphTraversal.cs`, `PatternStart.cs`, `PatternNode.cs`, `PatternRelationship.cs`, `PatternRelationshipDirected.cs`, `TraversalState.cs` | [arch §7.1](architecture.md#71-fluent-stages) |
| Lambda → predicate tree compilation | `PredicateTranslator.cs` | [arch §7.4](architecture.md#74-lambda-translation-predicatetranslator) |
| Predicate tree shape / new predicate types | Abstractions: `TraversalPredicate.cs` and its subtypes (`PropertyComparisonPredicate.cs`, `StringComparisonPredicate.cs`, `NullPredicate.cs`, `AndPredicate.cs`, `OrPredicate.cs`, `NotPredicate.cs`) | [arch §7.3](architecture.md#73-the-predicate-tree) |
| Cypher emission | `CypherEmitter.cs`, `CypherIdentifier.cs`, `ParameterBinder.cs`, `PropertySerializer.cs` | [arch §9.3](architecture.md#93-cypher-emission-cypheremitter-cypheridentifier-parameterbinder-propertyserializer) |
| Neo4j transactions / driver caching | `Neo4jGraphTransactionOpener.cs`, `Neo4jGraphTransaction.cs`, `Neo4jDriverCache.cs` | [arch §9.2 and §9.5](architecture.md#92-driver-caching-neo4jdrivercache) |
| Neo4j result materialization | `Neo4jResultMaterializer.cs` | [arch §9.6](architecture.md#96-materialization-neo4jresultmaterializer) |
| AGE Cypher emission (cypher() wrapping) | `AgeCypherEmitter.cs`, `CypherIdentifier.cs`, `AgeParameterBinder.cs`, `AgePropertySerializer.cs` | [arch §10.3](architecture.md#103-cypher-emission-agecypheremitter) |
| AGE Npgsql executor / data-source cache | `AgeRawStatementExecutor.cs`, `AgeDataSourceCache.cs` | [arch §10.4 and §10.2](architecture.md#102-data-source-caching-agedatasourcecache) |
| AGE transactions | `AgeGraphTransactionOpener.cs`, `AgeGraphTransaction.cs` | [arch §10.5](architecture.md#105-transactions-agegraphtransactionopener-agegraphtransaction) |
| AGE result materialization / agtype parser | `AgeResultMaterializer.cs`, `AgtypeReader.cs`, `AgtypeWriter.cs` | [arch §10.6](architecture.md#106-materialization-ageresultmaterializer-agtypereader-agtypewriter) |
| In-memory store / snapshot transactions | `InMemoryStore.cs`, `InMemoryStoreRegistry.cs`, `InMemoryGraphTransaction.cs`, `InMemoryGraphTransactionOpener.cs` | [arch §11.2 and §11.5](architecture.md#112-storage-inmemorystore-inmemorystoreregistry-inmemoryedge) |
| In-memory op-coded statements | `InMemoryStatementEmitter.cs`, `InMemoryRawStatementExecutor.cs` | [arch §11.3](architecture.md#113-op-coded-statements-inmemorystatementemitter-inmemoryrawstatementexecutor) |
| In-memory pattern walk / predicate evaluator | `InMemoryTraversalTranslator.cs` | [arch §11.6](architecture.md#116-traversal-inmemorytraversaltranslator) |
| Test fixtures (blog application) | `OnixLabs.ElementFramework.Conformance/TestFixtures/BlogApplication/` | [arch §12.2](architecture.md#122-conformance-suite-onixlabselementframeworkconformance) |
| Conformance suite (provider-agnostic tests) | `OnixLabs.ElementFramework.Conformance/AbstractGraphContextIntegrationTests.cs` | [arch §12.2](architecture.md#122-conformance-suite-onixlabselementframeworkconformance) |
| Unit-test fakes | `OnixLabs.ElementFramework.UnitTests/TestFixtures.cs` | [arch §12.1](architecture.md#121-unit-tests) |
| CI pipeline | `.github/workflows/ci.yml` | [arch §12.4](architecture.md#124-ci) |

When adding a new provider, the checklist is in [architecture.md §13](architecture.md#13-adding-a-new-provider).
