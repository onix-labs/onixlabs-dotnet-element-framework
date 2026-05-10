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

using System.Reflection;
using System.Runtime.CompilerServices;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the in-memory provider's <see cref="ITraversalTranslator"/> implementation.
/// </summary>
/// <remarks>
/// Walks the linear pattern in <see cref="TraversalAst"/> against the live in-memory store, materializing every binding
/// that satisfies the pattern and the predicate conjunction. Currently supports <see cref="TraversalKind.Match"/>; Merge
/// and Create traversals are out of scope for the v1 in-memory provider and throw <see cref="TraversalTranslationException"/>.
/// </remarks>
/// <param name="opener">The opener whose ambient slot decides which store the traversal walks.</param>
internal sealed class InMemoryTraversalTranslator(InMemoryGraphTransactionOpener opener) : ITraversalTranslator
{
    /// <inheritdoc/>
    public IEnumerable<TResult> Translate<TResult>(IGraphModel model, TraversalAst ast)
    {
        try
        {
            return ast.Kind switch
            {
                TraversalKind.Match => ExecuteMatch<TResult>(model, ast),
                TraversalKind.Merge => ExecuteMerge<TResult>(model, ast),
                TraversalKind.Create => ExecuteCreate<TResult>(model, ast),
                _ => throw new TraversalTranslationException($"Unknown traversal kind '{ast.Kind}'.")
            };
        }
        catch (TraversalTranslationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new TraversalTranslationException(
                "Failed to translate or execute the supplied fluent traversal against the in-memory store.", exception);
        }
    }

    /// <summary>
    /// Executes a <see cref="TraversalKind.Match"/> traversal against the active store.
    /// </summary>
    /// <typeparam name="TResult">The CLR type the return alias projects into.</typeparam>
    /// <param name="model">The frozen graph model.</param>
    /// <param name="ast">The traversal AST.</param>
    /// <returns>Returns every projected match.</returns>
    private IEnumerable<TResult> ExecuteMatch<TResult>(IGraphModel model, TraversalAst ast)
    {
        List<TResult> results = [];
        foreach (Dictionary<string, object> binding in EnumerateBindings(model, ast))
        {
            if (!PredicatesMatch(ast.Predicates, binding)) continue;
            results.Add(Project<TResult>(binding, ast.ReturnAlias));
        }
        return results;
    }

    /// <summary>
    /// Executes a <see cref="TraversalKind.Merge"/> traversal: returns existing matches when the pattern is satisfied,
    /// otherwise creates the pattern from scratch and returns the freshly-created binding.
    /// </summary>
    /// <typeparam name="TResult">The CLR type the return alias projects into.</typeparam>
    /// <param name="model">The frozen graph model.</param>
    /// <param name="ast">The traversal AST.</param>
    /// <returns>Returns every projected binding.</returns>
    private IEnumerable<TResult> ExecuteMerge<TResult>(IGraphModel model, TraversalAst ast)
    {
        List<TResult> matches = ExecuteMatch<TResult>(model, ast).ToList();
        return matches.Count > 0 ? matches : ExecuteCreate<TResult>(model, ast);
    }

    /// <summary>
    /// Executes a <see cref="TraversalKind.Create"/> traversal: synthesises fresh node and edge instances for each
    /// segment in the pattern, persists them, and returns the resulting binding.
    /// </summary>
    /// <typeparam name="TResult">The CLR type the return alias projects into.</typeparam>
    /// <param name="model">The frozen graph model.</param>
    /// <param name="ast">The traversal AST.</param>
    /// <returns>Returns the single binding produced by the creation.</returns>
    private IEnumerable<TResult> ExecuteCreate<TResult>(IGraphModel model, TraversalAst ast)
    {
        InMemoryStore store = Target;
        Dictionary<string, object> binding = [];
        Dictionary<string, object> keys = [];

        foreach (PatternSegment segment in ast.Segments)
        {
            if (segment is not NodePatternSegment nodeSegment) continue;
            object node = CreateInstance(nodeSegment.NodeType);
            object key = ResolveKey(model, nodeSegment.NodeType, node);
            store.UpsertNode(nodeSegment.NodeType, key, node);
            binding[nodeSegment.Alias] = node;
            keys[nodeSegment.Alias] = key;
        }

        for (int i = 0; i < ast.Segments.Count; i++)
        {
            if (ast.Segments[i] is not RelationshipPatternSegment relSegment) continue;
            NodePatternSegment previous = (NodePatternSegment)ast.Segments[i - 1];
            NodePatternSegment next = (NodePatternSegment)ast.Segments[i + 1];

            (NodePatternSegment startSegment, NodePatternSegment endSegment) = relSegment.Direction switch
            {
                RelationshipDirection.Incoming => (next, previous),
                _ => (previous, next)
            };

            object edge = CreateInstance(relSegment.EdgeType);
            store.AddEdge(new InMemoryEdge(
                startSegment.NodeType, keys[startSegment.Alias],
                relSegment.EdgeType, edge,
                endSegment.NodeType, keys[endSegment.Alias]));

            binding[relSegment.Alias] = edge;
        }

        return [Project<TResult>(binding, ast.ReturnAlias)];
    }

    /// <summary>
    /// Constructs a fresh instance of <paramref name="type"/>, preferring a parameterless constructor and falling back
    /// to <see cref="RuntimeHelpers.GetUninitializedObject(Type)"/> when none exists.
    /// </summary>
    /// <param name="type">The CLR type to instantiate.</param>
    /// <returns>Returns the freshly-constructed instance.</returns>
    private static object CreateInstance(Type type)
    {
        System.Reflection.ConstructorInfo? ctor = type.GetConstructor(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        return ctor is not null ? ctor.Invoke(null) : RuntimeHelpers.GetUninitializedObject(type);
    }

    /// <summary>
    /// Resolves the configured key value for the supplied instance.
    /// </summary>
    /// <param name="model">The frozen graph model.</param>
    /// <param name="type">The CLR type of the node.</param>
    /// <param name="node">The node instance whose key is being read.</param>
    /// <returns>Returns the resolved key value.</returns>
    /// <exception cref="TraversalTranslationException">Thrown when the node type has no key configured or the configured key returns <see langword="null"/>.</exception>
    private static object ResolveKey(IGraphModel model, Type type, object node)
    {
        INodeMetadata metadata = model.GetNode(type);
        if (metadata.Key is null)
            throw new TraversalTranslationException(
                $"Node type {type.FullName} has no key configured. Call builder.HasKey(...) in the node's configuration.");

        object? key = metadata.Key.Getter(node);
        if (key is null)
            throw new TraversalTranslationException(
                $"Synthesised node of type {type.FullName} has a null key value on property '{metadata.Key.Name}'.");

        return key;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<TResult> TranslateAsync<TResult>(
        IGraphModel model, TraversalAst ast, [EnumeratorCancellation] CancellationToken token = default)
    {
        foreach (TResult result in Translate<TResult>(model, ast))
        {
            token.ThrowIfCancellationRequested();
            yield return result;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Resolves the in-memory store the traversal should walk, honouring any ambient transaction.
    /// </summary>
    /// <value>The transaction-scoped clone when a transaction is active; otherwise the canonical store.</value>
    private InMemoryStore Target =>
        opener.Active is InMemoryGraphTransaction transaction ? transaction.Store : opener.Canonical;

    /// <summary>
    /// Enumerates every alias-to-payload binding satisfying the pattern segments of <paramref name="ast"/>.
    /// </summary>
    /// <param name="model">The frozen graph model used to resolve node and relationship metadata.</param>
    /// <param name="ast">The traversal AST.</param>
    /// <returns>Returns one dictionary per successful binding.</returns>
    /// <exception cref="TraversalTranslationException">Thrown when the AST is structurally invalid.</exception>
    private IEnumerable<Dictionary<string, object>> EnumerateBindings(IGraphModel model, TraversalAst ast)
    {
        if (ast.Segments.Count == 0)
            throw new TraversalTranslationException("TraversalAst has no pattern segments.");
        if (ast.Segments[0] is not NodePatternSegment startSegment)
            throw new TraversalTranslationException("TraversalAst pattern must begin with a node segment.");

        InMemoryStore store = Target;

        foreach (object node in store.NodesOfType(startSegment.NodeType))
        {
            Dictionary<string, object> binding = new() { [startSegment.Alias] = node };
            foreach (Dictionary<string, object> extended in Extend(model, ast.Segments, 1, binding, store))
                yield return extended;
        }
    }

    /// <summary>
    /// Recursively extends a partial binding through the remaining pattern segments.
    /// </summary>
    /// <param name="model">The frozen graph model.</param>
    /// <param name="segments">The full segment list from the AST.</param>
    /// <param name="index">The index of the next segment to bind.</param>
    /// <param name="binding">The accumulated binding so far.</param>
    /// <param name="store">The store to walk.</param>
    /// <returns>Returns every binding that completes the remaining pattern.</returns>
    /// <exception cref="TraversalTranslationException">Thrown when segments fall out of the expected node/relationship alternation.</exception>
    private static IEnumerable<Dictionary<string, object>> Extend(
        IGraphModel model,
        IReadOnlyList<PatternSegment> segments,
        int index,
        Dictionary<string, object> binding,
        InMemoryStore store)
    {
        if (index >= segments.Count)
        {
            yield return binding;
            yield break;
        }

        if (segments[index] is not RelationshipPatternSegment relSegment)
            throw new TraversalTranslationException(
                $"Expected a relationship segment at index {index}; got {segments[index].GetType().Name}.");

        if (index + 1 >= segments.Count || segments[index + 1] is not NodePatternSegment nodeSegment)
            throw new TraversalTranslationException(
                $"Expected a node segment at index {index + 1}; got {(index + 1 < segments.Count ? segments[index + 1].GetType().Name : "end of pattern")}.");

        NodePatternSegment previousSegment = (NodePatternSegment)segments[index - 1];
        object previousNode = binding[previousSegment.Alias];
        object previousKey = model.GetNode(previousSegment.NodeType).Key!.Getter(previousNode)!;

        foreach ((InMemoryEdge edge, Type farType, object farKey) in store.EdgesIncidentTo(previousSegment.NodeType, previousKey, relSegment.EdgeType, relSegment.Direction))
        {
            if (farType != nodeSegment.NodeType) continue;
            object? farNode = store.FindNode(farType, farKey);
            if (farNode is null) continue;

            Dictionary<string, object> nextBinding = new(binding)
            {
                [relSegment.Alias] = edge.Edge,
                [nodeSegment.Alias] = farNode
            };

            foreach (Dictionary<string, object> extended in Extend(model, segments, index + 2, nextBinding, store))
                yield return extended;
        }
    }

    /// <summary>
    /// Tests whether every predicate tree in <paramref name="predicates"/> evaluates to <see langword="true"/>
    /// against <paramref name="binding"/>. The top-level list is conjunctive.
    /// </summary>
    /// <param name="predicates">The predicate conjunction.</param>
    /// <param name="binding">The bindings produced by the pattern walk.</param>
    /// <returns>Returns <see langword="true"/> when every predicate matches; otherwise <see langword="false"/>.</returns>
    /// <exception cref="TraversalTranslationException">Thrown when a predicate references an unbound alias or unknown property.</exception>
    private static bool PredicatesMatch(IReadOnlyList<TraversalPredicate> predicates, Dictionary<string, object> binding)
    {
        foreach (TraversalPredicate predicate in predicates)
            if (!Evaluate(predicate, binding))
                return false;
        return true;
    }

    /// <summary>
    /// Recursively evaluates a single predicate tree node against <paramref name="binding"/>.
    /// </summary>
    /// <param name="predicate">The predicate tree node to evaluate.</param>
    /// <param name="binding">The bindings produced by the pattern walk.</param>
    /// <returns>Returns the boolean result of the predicate.</returns>
    /// <exception cref="TraversalTranslationException">Thrown when a predicate references an unbound alias or unknown property.</exception>
    private static bool Evaluate(TraversalPredicate predicate, Dictionary<string, object> binding) => predicate switch
    {
        PropertyComparisonPredicate comparison => EvaluatePropertyComparison(comparison, binding),
        StringComparisonPredicate stringComparison => EvaluateStringComparison(stringComparison, binding),
        NullPredicate nullPredicate => EvaluateNull(nullPredicate, binding),
        AndPredicate and => Evaluate(and.Left, binding) && Evaluate(and.Right, binding),
        OrPredicate or => Evaluate(or.Left, binding) || Evaluate(or.Right, binding),
        NotPredicate not => !Evaluate(not.Inner, binding),
        _ => throw new TraversalTranslationException($"Unknown predicate type '{predicate.GetType().FullName}'.")
    };

    private static bool EvaluatePropertyComparison(PropertyComparisonPredicate predicate, Dictionary<string, object> binding)
    {
        object? actual = ReadProperty(binding, predicate.Alias, predicate.ClrPropertyName);

        if (predicate.Operator is ComparisonOperator.Equal)
            return Equals(actual, predicate.Value);
        if (predicate.Operator is ComparisonOperator.NotEqual)
            return !Equals(actual, predicate.Value);

        if (actual is null || predicate.Value is null) return false;

        int comparison = Comparer<object>.Default.Compare(actual, predicate.Value);
        return predicate.Operator switch
        {
            ComparisonOperator.LessThan => comparison < 0,
            ComparisonOperator.LessThanOrEqual => comparison <= 0,
            ComparisonOperator.GreaterThan => comparison > 0,
            ComparisonOperator.GreaterThanOrEqual => comparison >= 0,
            _ => throw new TraversalTranslationException($"Unknown comparison operator '{predicate.Operator}'.")
        };
    }

    private static bool EvaluateStringComparison(StringComparisonPredicate predicate, Dictionary<string, object> binding)
    {
        object? actual = ReadProperty(binding, predicate.Alias, predicate.ClrPropertyName);
        if (actual is not string text) return false;
        return predicate.Operator switch
        {
            StringComparisonOperator.Contains => text.Contains(predicate.Value),
            StringComparisonOperator.StartsWith => text.StartsWith(predicate.Value),
            StringComparisonOperator.EndsWith => text.EndsWith(predicate.Value),
            _ => throw new TraversalTranslationException($"Unknown string comparison operator '{predicate.Operator}'.")
        };
    }

    private static bool EvaluateNull(NullPredicate predicate, Dictionary<string, object> binding)
    {
        object? actual = ReadProperty(binding, predicate.Alias, predicate.ClrPropertyName);
        return predicate.IsNull ? actual is null : actual is not null;
    }

    private static object? ReadProperty(Dictionary<string, object> binding, string alias, string clrPropertyName)
    {
        if (!binding.TryGetValue(alias, out object? bound))
            throw new TraversalTranslationException($"Predicate references unbound alias '{alias}'.");

        PropertyInfo? property = bound.GetType().GetProperty(clrPropertyName);
        if (property is null)
            throw new TraversalTranslationException(
                $"Predicate references property '{clrPropertyName}' which is not present on alias '{alias}'.");

        return property.GetValue(bound);
    }

    /// <summary>
    /// Projects the binding's value at <paramref name="returnAlias"/> into a <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">The CLR type the alias is being projected into.</typeparam>
    /// <param name="binding">The completed binding.</param>
    /// <param name="returnAlias">The alias whose bound value is being returned.</param>
    /// <returns>Returns the bound CLR instance cast to <typeparamref name="TResult"/>.</returns>
    /// <exception cref="TraversalTranslationException">Thrown when <paramref name="returnAlias"/> is not present in <paramref name="binding"/> or the bound value is not assignable to <typeparamref name="TResult"/>.</exception>
    private static TResult Project<TResult>(Dictionary<string, object> binding, string returnAlias)
    {
        if (!binding.TryGetValue(returnAlias, out object? value))
            throw new TraversalTranslationException($"Return alias '{returnAlias}' is not bound by the traversal pattern.");

        if (value is not TResult typed)
            throw new TraversalTranslationException(
                $"Bound value at '{returnAlias}' is not assignable to {typeof(TResult).FullName}; got {value.GetType().FullName}.");

        return typed;
    }
}
