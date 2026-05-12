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

using System.Text;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the Cypher implementation of <see cref="IStatementEmitter"/> for the Neo4j provider.
/// </summary>
/// <remarks>
/// Each method is pure: it takes the frozen <see cref="IGraphModel"/> plus the operands, returns a <see cref="DataStatement"/>, and holds no per-call state across invocations. Cross-cutting rules every emitter method follows: every CLR-side label or property name flows through <see cref="CypherIdentifier.Escape"/>; every value is bound via a per-call <see cref="ParameterBinder"/> (values are never inlined into the Cypher string); every value is normalized via <see cref="PropertySerializer.Serialize"/> before binding so the driver receives a Bolt-friendly type; <see cref="EmitAdd"/> skips null-valued non-key properties from the property clause whereas <see cref="EmitUpdate"/> sets them explicitly so consumers can clear values; <see cref="EmitConnect"/> omits the property clause when the registered edge has no mapped properties.
/// </remarks>
internal sealed class CypherEmitter : IStatementEmitter
{
    /// <inheritdoc/>
    public DataStatement EmitAdd<T>(IGraphModel model, T node)
    {
        INodeMetadata metadata = model.GetNode(typeof(T));
        ParameterBinder binder = new();
        string label = CypherIdentifier.Escape(metadata.Label);

        StringBuilder properties = new();
        bool first = true;
        foreach (IPropertyMetadata property in metadata.Properties)
        {
            object? value = property.Getter(node!);
            if (value is null && !ReferenceEquals(property, metadata.Key)) continue;

            string token = binder.Bind(property.Name, PropertySerializer.Serialize(value));
            if (!first) properties.Append(", ");
            properties.Append(CypherIdentifier.Escape(property.Name)).Append(": ").Append(token);
            first = false;
        }

        string cypher = properties.Length == 0
            ? $"CREATE (n:{label}) RETURN n"
            : $"CREATE (n:{label} {{ {properties} }}) RETURN n";

        return new DataStatement(cypher, binder.ToParameters());
    }

    /// <inheritdoc/>
    public DataStatement EmitUpdate<T>(IGraphModel model, T node)
    {
        INodeMetadata metadata = model.GetNode(typeof(T));
        IPropertyMetadata key = metadata.Key!;
        ParameterBinder binder = new();
        string label = CypherIdentifier.Escape(metadata.Label);

        object? keyValue = key.Getter(node!);
        string keyToken = binder.Bind(key.Name, PropertySerializer.Serialize(keyValue));
        string keyName = CypherIdentifier.Escape(key.Name);

        StringBuilder set = new();
        bool first = true;
        foreach (IPropertyMetadata property in metadata.Properties)
        {
            if (ReferenceEquals(property, key)) continue;
            object? value = property.Getter(node!);
            string token = binder.Bind(property.Name, PropertySerializer.Serialize(value));
            if (!first) set.Append(", ");
            set.Append("n.").Append(CypherIdentifier.Escape(property.Name)).Append(" = ").Append(token);
            first = false;
        }

        string cypher = set.Length == 0
            ? $"MATCH (n:{label} {{ {keyName}: {keyToken} }}) RETURN n"
            : $"MATCH (n:{label} {{ {keyName}: {keyToken} }}) SET {set} RETURN n";

        return new DataStatement(cypher, binder.ToParameters());
    }

    /// <inheritdoc/>
    public DataStatement EmitRemove<T>(IGraphModel model, T node)
    {
        INodeMetadata metadata = model.GetNode(typeof(T));
        IPropertyMetadata key = metadata.Key!;
        ParameterBinder binder = new();
        string label = CypherIdentifier.Escape(metadata.Label);

        object? keyValue = key.Getter(node!);
        string keyToken = binder.Bind(key.Name, PropertySerializer.Serialize(keyValue));
        string keyName = CypherIdentifier.Escape(key.Name);

        string cypher = $"MATCH (n:{label} {{ {keyName}: {keyToken} }}) DETACH DELETE n";
        return new DataStatement(cypher, binder.ToParameters());
    }

    /// <inheritdoc/>
    public DataStatement EmitMerge<T>(IGraphModel model, T node)
    {
        INodeMetadata metadata = model.GetNode(typeof(T));
        IPropertyMetadata key = metadata.Key!;
        ParameterBinder binder = new();
        string label = CypherIdentifier.Escape(metadata.Label);

        object? keyValue = key.Getter(node!);
        string keyToken = binder.Bind(key.Name, PropertySerializer.Serialize(keyValue));
        string keyName = CypherIdentifier.Escape(key.Name);

        StringBuilder set = new();
        bool first = true;
        foreach (IPropertyMetadata property in metadata.Properties)
        {
            if (ReferenceEquals(property, key)) continue;
            object? value = property.Getter(node!);
            string token = binder.Bind(property.Name, PropertySerializer.Serialize(value));
            if (!first) set.Append(", ");
            set.Append("n.").Append(CypherIdentifier.Escape(property.Name)).Append(" = ").Append(token);
            first = false;
        }

        string cypher = set.Length == 0
            ? $"MERGE (n:{label} {{ {keyName}: {keyToken} }}) RETURN n"
            : $"MERGE (n:{label} {{ {keyName}: {keyToken} }}) ON CREATE SET {set} ON MATCH SET {set} RETURN n";

        return new DataStatement(cypher, binder.ToParameters());
    }

    /// <inheritdoc/>
    public DataStatement EmitConnect<TStart, TEdge, TEnd>(IGraphModel model, TStart start, TEdge edge, TEnd end)
    {
        IRelationshipMetadata relationship = model.GetRelationship(typeof(TEdge));
        INodeMetadata startNode = model.GetNode(relationship.StartType);
        INodeMetadata endNode = model.GetNode(relationship.EndType);
        IPropertyMetadata startKey = startNode.Key!;
        IPropertyMetadata endKey = endNode.Key!;

        ParameterBinder binder = new();
        string startLabel = CypherIdentifier.Escape(startNode.Label);
        string endLabel = CypherIdentifier.Escape(endNode.Label);
        string relType = CypherIdentifier.Escape(relationship.RelationshipType);

        object? startKeyValue = startKey.Getter(start!);
        object? endKeyValue = endKey.Getter(end!);
        string startKeyToken = binder.Bind($"start_{startKey.Name}", PropertySerializer.Serialize(startKeyValue));
        string endKeyToken = binder.Bind($"end_{endKey.Name}", PropertySerializer.Serialize(endKeyValue));
        string startKeyName = CypherIdentifier.Escape(startKey.Name);
        string endKeyName = CypherIdentifier.Escape(endKey.Name);

        StringBuilder edgeProperties = new();
        bool first = true;
        foreach (IPropertyMetadata property in relationship.Properties)
        {
            object? value = property.Getter(edge!);
            if (value is null) continue;
            string token = binder.Bind($"edge_{property.Name}", PropertySerializer.Serialize(value));
            if (!first) edgeProperties.Append(", ");
            edgeProperties.Append(CypherIdentifier.Escape(property.Name)).Append(": ").Append(token);
            first = false;
        }

        string startMatch = $"(s:{startLabel} {{ {startKeyName}: {startKeyToken} }})";
        string endMatch = $"(e:{endLabel} {{ {endKeyName}: {endKeyToken} }})";
        string edgePart = edgeProperties.Length == 0
            ? $"(s)-[r:{relType}]->(e)"
            : $"(s)-[r:{relType} {{ {edgeProperties} }}]->(e)";

        string cypher = $"MATCH {startMatch}, {endMatch} CREATE {edgePart} RETURN r";
        return new DataStatement(cypher, binder.ToParameters());
    }

    /// <inheritdoc/>
    public DataStatement EmitDisconnect<TStart, TEdge, TEnd>(IGraphModel model, TStart start, TEnd end)
    {
        IRelationshipMetadata relationship = model.GetRelationship(typeof(TEdge));
        INodeMetadata startNode = model.GetNode(relationship.StartType);
        INodeMetadata endNode = model.GetNode(relationship.EndType);
        IPropertyMetadata startKey = startNode.Key!;
        IPropertyMetadata endKey = endNode.Key!;

        ParameterBinder binder = new();
        string startLabel = CypherIdentifier.Escape(startNode.Label);
        string endLabel = CypherIdentifier.Escape(endNode.Label);
        string relType = CypherIdentifier.Escape(relationship.RelationshipType);

        object? startKeyValue = startKey.Getter(start!);
        object? endKeyValue = endKey.Getter(end!);
        string startKeyToken = binder.Bind($"start_{startKey.Name}", PropertySerializer.Serialize(startKeyValue));
        string endKeyToken = binder.Bind($"end_{endKey.Name}", PropertySerializer.Serialize(endKeyValue));
        string startKeyName = CypherIdentifier.Escape(startKey.Name);
        string endKeyName = CypherIdentifier.Escape(endKey.Name);

        string cypher =
            $"MATCH (s:{startLabel} {{ {startKeyName}: {startKeyToken} }})-[r:{relType}]->(e:{endLabel} {{ {endKeyName}: {endKeyToken} }}) DELETE r";
        return new DataStatement(cypher, binder.ToParameters());
    }

    /// <inheritdoc/>
    public DataStatement EmitFindById<T>(IGraphModel model, object key)
    {
        INodeMetadata metadata = model.GetNode(typeof(T));
        IPropertyMetadata keyProperty = metadata.Key!;
        ParameterBinder binder = new();

        string label = CypherIdentifier.Escape(metadata.Label);
        string keyToken = binder.Bind(keyProperty.Name, PropertySerializer.Serialize(key));
        string keyName = CypherIdentifier.Escape(keyProperty.Name);

        string cypher = $"MATCH (n:{label} {{ {keyName}: {keyToken} }}) RETURN n LIMIT 1";
        return new DataStatement(cypher, binder.ToParameters());
    }

    /// <inheritdoc/>
    public DataStatement EmitExists<T>(IGraphModel model, object key)
    {
        INodeMetadata metadata = model.GetNode(typeof(T));
        IPropertyMetadata keyProperty = metadata.Key!;
        ParameterBinder binder = new();

        string label = CypherIdentifier.Escape(metadata.Label);
        string keyToken = binder.Bind(keyProperty.Name, PropertySerializer.Serialize(key));
        string keyName = CypherIdentifier.Escape(keyProperty.Name);

        string cypher = $"MATCH (n:{label} {{ {keyName}: {keyToken} }}) RETURN count(n) AS count";
        return new DataStatement(cypher, binder.ToParameters());
    }

    /// <inheritdoc/>
    public DataStatement EmitAsEnumerableNodes<T>(IGraphModel model)
    {
        INodeMetadata metadata = model.GetNode(typeof(T));
        string label = CypherIdentifier.Escape(metadata.Label);
        return new DataStatement($"MATCH (n:{label}) RETURN n", new Dictionary<string, object?>());
    }

    /// <inheritdoc/>
    public DataStatement EmitAsEnumerableEdges<T>(IGraphModel model)
    {
        IRelationshipMetadata relationship = model.GetRelationship(typeof(T));
        string relType = CypherIdentifier.Escape(relationship.RelationshipType);
        return new DataStatement($"MATCH ()-[r:{relType}]->() RETURN r", new Dictionary<string, object?>());
    }

    /// <inheritdoc/>
    public DataStatement EmitTraversal(IGraphModel model, TraversalAst ast)
    {
        if (ast.Segments.Count == 0)
            throw new StatementEmissionException(
                "TraversalAst has no pattern segments. The fluent builder must bind at least one node before Return is invoked.");
        if (ast.Segments[0] is not NodePatternSegment)
            throw new StatementEmissionException(
                "TraversalAst pattern must begin with a node segment.");

        ParameterBinder binder = new();
        StringBuilder builder = new();
        builder.Append(KeywordFor(ast.Kind)).Append(' ');
        AppendPattern(builder, model, ast.Segments);

        if (ast.Predicates.Count > 0)
        {
            builder.Append(" WHERE ");
            for (int i = 0; i < ast.Predicates.Count; i++)
            {
                if (i > 0) builder.Append(" AND ");
                builder.Append('(');
                AppendPredicate(builder, binder, model, ast, ast.Predicates[i]);
                builder.Append(')');
            }
        }

        builder.Append(" RETURN ").Append(CypherIdentifier.Escape(ast.ReturnAlias));
        AppendOrderingSkipLimit(builder, model, ast);
        return new DataStatement(builder.ToString(), binder.ToParameters());
    }

    /// <summary>
    /// Appends the optional <c>ORDER BY</c> / <c>SKIP</c> / <c>LIMIT</c> tail clauses to <paramref name="builder"/> in that order, when the AST has them set. Cypher requires the canonical order ORDER BY → SKIP → LIMIT and applies SKIP / LIMIT against the ordered result; the framework's fluent builder accepts these in any order at the call site, so this method is the single source of truth for emission ordering.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> that receives the appended tail.</param>
    /// <param name="model">The graph model used to resolve the ordering property's storage name.</param>
    /// <param name="ast">The traversal AST whose tail clauses are being emitted.</param>
    private static void AppendOrderingSkipLimit(StringBuilder builder, IGraphModel model, TraversalAst ast)
    {
        foreach (TraversalOrdering ordering in ast.Orderings)
        {
            IPropertyMetadata property = ResolvePredicateProperty(model, ast, ordering.Alias, ordering.ClrPropertyName);
            builder.Append(" ORDER BY ")
                .Append(CypherIdentifier.Escape(ordering.Alias))
                .Append('.')
                .Append(CypherIdentifier.Escape(property.Name))
                .Append(ordering.Direction == OrderDirection.Descending ? " DESC" : " ASC");
        }

        if (ast.Skip is int skip) builder.Append(" SKIP ").Append(skip);
        if (ast.Take is int take) builder.Append(" LIMIT ").Append(take);
    }

    /// <summary>
    /// Appends the pattern segments of <paramref name="segments"/> to <paramref name="builder"/> as Cypher node and relationship syntax.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> that receives the emitted pattern.</param>
    /// <param name="model">The graph model used to resolve labels and relationship types.</param>
    /// <param name="segments">The pattern segments to emit, in order.</param>
    private static void AppendPattern(StringBuilder builder, IGraphModel model, IReadOnlyList<PatternSegment> segments)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            switch (segments[i])
            {
                case NodePatternSegment node:
                    INodeMetadata nodeMeta = model.GetNode(node.NodeType);
                    builder.Append('(')
                        .Append(CypherIdentifier.Escape(node.Alias))
                        .Append(':')
                        .Append(CypherIdentifier.Escape(nodeMeta.Label))
                        .Append(')');
                    break;
                case RelationshipPatternSegment relationship:
                    IRelationshipMetadata relMeta = model.GetRelationship(relationship.EdgeType);
                    string body = $"[{CypherIdentifier.Escape(relationship.Alias)}:{CypherIdentifier.Escape(relMeta.RelationshipType)}]";
                    builder.Append(relationship.Direction switch
                    {
                        RelationshipDirection.Outgoing => $"-{body}->",
                        RelationshipDirection.Incoming => $"<-{body}-",
                        RelationshipDirection.Either => $"-{body}-",
                        _ => throw new StatementEmissionException($"Unknown relationship direction '{relationship.Direction}'.")
                    });
                    break;
                default:
                    throw new StatementEmissionException($"Unknown pattern segment type '{segments[i].GetType().FullName}'.");
            }
        }
    }

    /// <summary>
    /// Appends a traversal predicate tree to <paramref name="builder"/> as a Cypher boolean expression, binding any
    /// leaf values to <paramref name="binder"/>. Recurses into <see cref="AndPredicate"/>, <see cref="OrPredicate"/>,
    /// and <see cref="NotPredicate"/>.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> that receives the emitted predicate.</param>
    /// <param name="binder">The parameter binder that captures predicate values.</param>
    /// <param name="model">The graph model used to resolve the predicate properties.</param>
    /// <param name="ast">The traversal AST that owns the predicate.</param>
    /// <param name="predicate">The predicate tree to emit.</param>
    private static void AppendPredicate(
        StringBuilder builder,
        ParameterBinder binder,
        IGraphModel model,
        TraversalAst ast,
        TraversalPredicate predicate)
    {
        switch (predicate)
        {
            case PropertyComparisonPredicate comparison:
                AppendPropertyComparison(builder, binder, model, ast, comparison);
                break;
            case StringComparisonPredicate stringComparison:
                AppendStringComparison(builder, binder, model, ast, stringComparison);
                break;
            case NullPredicate nullPredicate:
                AppendNull(builder, model, ast, nullPredicate);
                break;
            case AndPredicate and:
                builder.Append('(');
                AppendPredicate(builder, binder, model, ast, and.Left);
                builder.Append(" AND ");
                AppendPredicate(builder, binder, model, ast, and.Right);
                builder.Append(')');
                break;
            case OrPredicate or:
                builder.Append('(');
                AppendPredicate(builder, binder, model, ast, or.Left);
                builder.Append(" OR ");
                AppendPredicate(builder, binder, model, ast, or.Right);
                builder.Append(')');
                break;
            case NotPredicate not:
                builder.Append("NOT (");
                AppendPredicate(builder, binder, model, ast, not.Inner);
                builder.Append(')');
                break;
            default:
                throw new StatementEmissionException($"Unknown predicate type '{predicate.GetType().FullName}'.");
        }
    }

    private static void AppendPropertyComparison(
        StringBuilder builder,
        ParameterBinder binder,
        IGraphModel model,
        TraversalAst ast,
        PropertyComparisonPredicate predicate)
    {
        IPropertyMetadata property = ResolvePredicateProperty(model, ast, predicate.Alias, predicate.ClrPropertyName);
        string token = binder.Bind(property.Name, PropertySerializer.Serialize(predicate.Value));
        builder.Append(CypherIdentifier.Escape(predicate.Alias))
            .Append('.')
            .Append(CypherIdentifier.Escape(property.Name))
            .Append(' ')
            .Append(CypherOperator(predicate.Operator))
            .Append(' ')
            .Append(token);
    }

    private static void AppendStringComparison(
        StringBuilder builder,
        ParameterBinder binder,
        IGraphModel model,
        TraversalAst ast,
        StringComparisonPredicate predicate)
    {
        IPropertyMetadata property = ResolvePredicateProperty(model, ast, predicate.Alias, predicate.ClrPropertyName);
        string token = binder.Bind(property.Name, predicate.Value);
        builder.Append(CypherIdentifier.Escape(predicate.Alias))
            .Append('.')
            .Append(CypherIdentifier.Escape(property.Name))
            .Append(' ')
            .Append(CypherStringOperator(predicate.Operator))
            .Append(' ')
            .Append(token);
    }

    private static void AppendNull(
        StringBuilder builder,
        IGraphModel model,
        TraversalAst ast,
        NullPredicate predicate)
    {
        IPropertyMetadata property = ResolvePredicateProperty(model, ast, predicate.Alias, predicate.ClrPropertyName);
        builder.Append(CypherIdentifier.Escape(predicate.Alias))
            .Append('.')
            .Append(CypherIdentifier.Escape(property.Name))
            .Append(predicate.IsNull ? " IS NULL" : " IS NOT NULL");
    }

    private static string CypherOperator(ComparisonOperator op) => op switch
    {
        ComparisonOperator.Equal => "=",
        ComparisonOperator.NotEqual => "<>",
        ComparisonOperator.LessThan => "<",
        ComparisonOperator.LessThanOrEqual => "<=",
        ComparisonOperator.GreaterThan => ">",
        ComparisonOperator.GreaterThanOrEqual => ">=",
        _ => throw new StatementEmissionException($"Unknown comparison operator '{op}'.")
    };

    private static string CypherStringOperator(StringComparisonOperator op) => op switch
    {
        StringComparisonOperator.Contains => "CONTAINS",
        StringComparisonOperator.StartsWith => "STARTS WITH",
        StringComparisonOperator.EndsWith => "ENDS WITH",
        _ => throw new StatementEmissionException($"Unknown string comparison operator '{op}'.")
    };

    /// <summary>
    /// Resolves the <see cref="IPropertyMetadata"/> for <paramref name="clrPropertyName"/> on the segment bound to <paramref name="alias"/> within <paramref name="ast"/>.
    /// </summary>
    /// <param name="model">The graph model used to look up node and relationship metadata.</param>
    /// <param name="ast">The traversal AST that owns the predicate.</param>
    /// <param name="alias">The alias the predicate is scoped to.</param>
    /// <param name="clrPropertyName">The CLR property name referenced by the predicate.</param>
    /// <returns>Returns the <see cref="IPropertyMetadata"/> matching the predicate's CLR property name on its bound alias.</returns>
    /// <exception cref="StatementEmissionException">Thrown when the predicate references an unbound alias or a property that is not mapped on that alias.</exception>
    private static IPropertyMetadata ResolvePredicateProperty(IGraphModel model, TraversalAst ast, string alias, string clrPropertyName)
    {
        PatternSegment segment = ast.Segments.FirstOrDefault(s => s.Alias == alias)
                                 ?? throw new StatementEmissionException($"Predicate references unbound alias '{alias}'.");

        IReadOnlyList<IPropertyMetadata> properties = segment switch
        {
            NodePatternSegment node => model.GetNode(node.NodeType).Properties,
            RelationshipPatternSegment relationship => model.GetRelationship(relationship.EdgeType).Properties,
            _ => throw new StatementEmissionException($"Unknown pattern segment type '{segment.GetType().FullName}'.")
        };

        return properties.FirstOrDefault(p => p.Property.Name == clrPropertyName)
               ?? throw new StatementEmissionException(
                   $"Predicate references property '{clrPropertyName}' which is not mapped on alias '{alias}'.");
    }

    /// <summary>
    /// Returns the Cypher keyword that opens a traversal of the supplied <paramref name="kind"/>.
    /// </summary>
    /// <param name="kind">The traversal kind to translate.</param>
    /// <returns>Returns the Cypher keyword for <paramref name="kind"/>.</returns>
    /// <exception cref="StatementEmissionException">Thrown when <paramref name="kind"/> is not a recognized traversal kind.</exception>
    private static string KeywordFor(TraversalKind kind) => kind switch
    {
        TraversalKind.Match => "MATCH",
        TraversalKind.Merge => "MERGE",
        TraversalKind.Create => "CREATE",
        _ => throw new StatementEmissionException($"Unknown traversal kind '{kind}'.")
    };
}
