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
/// Represents the Apache AGE implementation of <see cref="IStatementEmitter"/> that emits the SQL form of a Cypher query — the openCypher body wrapped inside AGE's <c>cypher()</c> envelope so it can be executed by Npgsql.
/// </summary>
/// <remarks>
/// Each method is pure: it takes the frozen <see cref="IGraphModel"/> plus operands and returns a <see cref="DataStatement"/> whose <see cref="DataStatement.Statement"/> is a complete <c>SELECT * FROM cypher(...) AS (...)</c> expression. Cross-cutting rules: every CLR-side label or property name flows through <see cref="CypherIdentifier.Escape"/>; every value is bound via a per-call <see cref="AgeParameterBinder"/> (values are never inlined into the Cypher); every value is normalized via <see cref="AgePropertySerializer.Serialize"/> before binding so the executor's <see cref="AgtypeWriter"/> sees only JSON-friendly primitives; <see cref="EmitAdd"/> skips null-valued non-key properties from the property clause whereas <see cref="EmitUpdate"/> sets them explicitly so consumers can clear values; <see cref="EmitConnect"/> omits the property clause when the registered edge has no mapped properties. AGE returns the parameter payload as one <c>agtype</c> object passed via SQL parameter <c>@p</c>; the executor binds it as <see cref="NpgsqlTypes.NpgsqlDbType.Unknown"/>.
/// </remarks>
internal sealed class AgeCypherEmitter : IStatementEmitter
{
    /// <summary>
    /// The default alias the emitter uses for projected nodes ("n").
    /// </summary>
    internal const string NodeAlias = "n";

    /// <summary>
    /// The default alias the emitter uses for projected edges ("r").
    /// </summary>
    internal const string EdgeAlias = "r";

    /// <summary>
    /// The alias the emitter uses for existence-count projections. Named <c>cnt</c> (not <c>count</c>) because <c>count</c> is a reserved SQL keyword and AGE rejects it as a column alias in the <c>AS (...)</c> schema.
    /// </summary>
    internal const string CountAlias = "cnt";

    /// <summary>
    /// The escaped graph name the emitter targets in every <c>cypher()</c> call.
    /// </summary>
    private readonly string graphName;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgeCypherEmitter"/> class bound to the supplied graph name.
    /// </summary>
    /// <param name="graphName">The AGE graph name to target — must already exist (created via <c>SELECT create_graph('name')</c>). The name is wrapped in single quotes inside the <c>cypher()</c> call so embedded single quotes are doubled here.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="graphName"/> is null, empty, or whitespace.</exception>
    internal AgeCypherEmitter(string graphName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);
        this.graphName = graphName.Replace("'", "''");
    }

    /// <inheritdoc/>
    public DataStatement EmitAdd<T>(IGraphModel model, T node)
    {
        INodeMetadata metadata = model.GetNode(typeof(T));
        AgeParameterBinder binder = new();
        string label = CypherIdentifier.Escape(metadata.Label);

        StringBuilder properties = new();
        bool first = true;
        foreach (IPropertyMetadata property in metadata.Properties)
        {
            object? value = property.Getter(node!);
            if (value is null && !ReferenceEquals(property, metadata.Key)) continue;

            string token = binder.Bind(property.Name, AgePropertySerializer.Serialize(value));
            if (!first) properties.Append(", ");
            properties.Append(CypherIdentifier.Escape(property.Name)).Append(": ").Append(token);
            first = false;
        }

        string cypher = properties.Length == 0
            ? $"CREATE (n:{label}) RETURN n"
            : $"CREATE (n:{label} {{ {properties} }}) RETURN n";

        return Wrap(cypher, binder, NodeAlias);
    }

    /// <inheritdoc/>
    public DataStatement EmitUpdate<T>(IGraphModel model, T node)
    {
        INodeMetadata metadata = model.GetNode(typeof(T));
        IPropertyMetadata key = metadata.Key!;
        AgeParameterBinder binder = new();
        string label = CypherIdentifier.Escape(metadata.Label);

        object? keyValue = key.Getter(node!);
        string keyToken = binder.Bind(key.Name, AgePropertySerializer.Serialize(keyValue));
        string keyName = CypherIdentifier.Escape(key.Name);

        StringBuilder set = new();
        bool first = true;
        foreach (IPropertyMetadata property in metadata.Properties)
        {
            if (ReferenceEquals(property, key)) continue;
            object? value = property.Getter(node!);
            string token = binder.Bind(property.Name, AgePropertySerializer.Serialize(value));
            if (!first) set.Append(", ");
            set.Append("n.").Append(CypherIdentifier.Escape(property.Name)).Append(" = ").Append(token);
            first = false;
        }

        string cypher = set.Length == 0
            ? $"MATCH (n:{label} {{ {keyName}: {keyToken} }}) RETURN n"
            : $"MATCH (n:{label} {{ {keyName}: {keyToken} }}) SET {set} RETURN n";

        return Wrap(cypher, binder, NodeAlias);
    }

    /// <inheritdoc/>
    public DataStatement EmitRemove<T>(IGraphModel model, T node)
    {
        INodeMetadata metadata = model.GetNode(typeof(T));
        IPropertyMetadata key = metadata.Key!;
        AgeParameterBinder binder = new();
        string label = CypherIdentifier.Escape(metadata.Label);

        object? keyValue = key.Getter(node!);
        string keyToken = binder.Bind(key.Name, AgePropertySerializer.Serialize(keyValue));
        string keyName = CypherIdentifier.Escape(key.Name);

        string cypher = $"MATCH (n:{label} {{ {keyName}: {keyToken} }}) DETACH DELETE n";
        return Wrap(cypher, binder, NodeAlias);
    }

    /// <inheritdoc/>
    public DataStatement EmitMerge<T>(IGraphModel model, T node)
    {
        INodeMetadata metadata = model.GetNode(typeof(T));
        IPropertyMetadata key = metadata.Key!;
        AgeParameterBinder binder = new();
        string label = CypherIdentifier.Escape(metadata.Label);

        object? keyValue = key.Getter(node!);
        string keyToken = binder.Bind(key.Name, AgePropertySerializer.Serialize(keyValue));
        string keyName = CypherIdentifier.Escape(key.Name);

        StringBuilder set = new();
        bool first = true;
        foreach (IPropertyMetadata property in metadata.Properties)
        {
            if (ReferenceEquals(property, key)) continue;
            object? value = property.Getter(node!);
            string token = binder.Bind(property.Name, AgePropertySerializer.Serialize(value));
            if (!first) set.Append(", ");
            set.Append("n.").Append(CypherIdentifier.Escape(property.Name)).Append(" = ").Append(token);
            first = false;
        }

        // AGE 1.6.0's openCypher does not implement ON CREATE SET / ON MATCH SET. Neo4j uses both
        // clauses with identical SET lists to express "always set these properties whether the row
        // existed or not"; the unconditional SET form following MERGE has the same observable
        // semantics, so we emit that here.
        string cypher = set.Length == 0
            ? $"MERGE (n:{label} {{ {keyName}: {keyToken} }}) RETURN n"
            : $"MERGE (n:{label} {{ {keyName}: {keyToken} }}) SET {set} RETURN n";

        return Wrap(cypher, binder, NodeAlias);
    }

    /// <inheritdoc/>
    public DataStatement EmitConnect<TStart, TEdge, TEnd>(IGraphModel model, TStart start, TEdge edge, TEnd end)
    {
        IRelationshipMetadata relationship = model.GetRelationship(typeof(TEdge));
        INodeMetadata startNode = model.GetNode(relationship.StartType);
        INodeMetadata endNode = model.GetNode(relationship.EndType);
        IPropertyMetadata startKey = startNode.Key!;
        IPropertyMetadata endKey = endNode.Key!;

        AgeParameterBinder binder = new();
        string startLabel = CypherIdentifier.Escape(startNode.Label);
        string endLabel = CypherIdentifier.Escape(endNode.Label);
        string relType = CypherIdentifier.Escape(relationship.RelationshipType);

        object? startKeyValue = startKey.Getter(start!);
        object? endKeyValue = endKey.Getter(end!);
        string startKeyToken = binder.Bind($"start_{startKey.Name}", AgePropertySerializer.Serialize(startKeyValue));
        string endKeyToken = binder.Bind($"end_{endKey.Name}", AgePropertySerializer.Serialize(endKeyValue));
        string startKeyName = CypherIdentifier.Escape(startKey.Name);
        string endKeyName = CypherIdentifier.Escape(endKey.Name);

        StringBuilder edgeProperties = new();
        bool first = true;
        foreach (IPropertyMetadata property in relationship.Properties)
        {
            object? value = property.Getter(edge!);
            if (value is null) continue;
            string token = binder.Bind($"edge_{property.Name}", AgePropertySerializer.Serialize(value));
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
        return Wrap(cypher, binder, EdgeAlias);
    }

    /// <inheritdoc/>
    public DataStatement EmitDisconnect<TStart, TEdge, TEnd>(IGraphModel model, TStart start, TEnd end)
    {
        IRelationshipMetadata relationship = model.GetRelationship(typeof(TEdge));
        INodeMetadata startNode = model.GetNode(relationship.StartType);
        INodeMetadata endNode = model.GetNode(relationship.EndType);
        IPropertyMetadata startKey = startNode.Key!;
        IPropertyMetadata endKey = endNode.Key!;

        AgeParameterBinder binder = new();
        string startLabel = CypherIdentifier.Escape(startNode.Label);
        string endLabel = CypherIdentifier.Escape(endNode.Label);
        string relType = CypherIdentifier.Escape(relationship.RelationshipType);

        object? startKeyValue = startKey.Getter(start!);
        object? endKeyValue = endKey.Getter(end!);
        string startKeyToken = binder.Bind($"start_{startKey.Name}", AgePropertySerializer.Serialize(startKeyValue));
        string endKeyToken = binder.Bind($"end_{endKey.Name}", AgePropertySerializer.Serialize(endKeyValue));
        string startKeyName = CypherIdentifier.Escape(startKey.Name);
        string endKeyName = CypherIdentifier.Escape(endKey.Name);

        string cypher =
            $"MATCH (s:{startLabel} {{ {startKeyName}: {startKeyToken} }})-[r:{relType}]->(e:{endLabel} {{ {endKeyName}: {endKeyToken} }}) DELETE r";
        return Wrap(cypher, binder, EdgeAlias);
    }

    /// <inheritdoc/>
    public DataStatement EmitFindById<T>(IGraphModel model, object key)
    {
        INodeMetadata metadata = model.GetNode(typeof(T));
        IPropertyMetadata keyProperty = metadata.Key!;
        AgeParameterBinder binder = new();

        string label = CypherIdentifier.Escape(metadata.Label);
        string keyToken = binder.Bind(keyProperty.Name, AgePropertySerializer.Serialize(key));
        string keyName = CypherIdentifier.Escape(keyProperty.Name);

        string cypher = $"MATCH (n:{label} {{ {keyName}: {keyToken} }}) RETURN n LIMIT 1";
        return Wrap(cypher, binder, NodeAlias);
    }

    /// <inheritdoc/>
    public DataStatement EmitExists<T>(IGraphModel model, object key)
    {
        INodeMetadata metadata = model.GetNode(typeof(T));
        IPropertyMetadata keyProperty = metadata.Key!;
        AgeParameterBinder binder = new();

        string label = CypherIdentifier.Escape(metadata.Label);
        string keyToken = binder.Bind(keyProperty.Name, AgePropertySerializer.Serialize(key));
        string keyName = CypherIdentifier.Escape(keyProperty.Name);

        string cypher = $"MATCH (n:{label} {{ {keyName}: {keyToken} }}) RETURN count(n) AS cnt";
        return Wrap(cypher, binder, CountAlias);
    }

    /// <inheritdoc/>
    public DataStatement EmitAsEnumerableNodes<T>(IGraphModel model)
    {
        INodeMetadata metadata = model.GetNode(typeof(T));
        string label = CypherIdentifier.Escape(metadata.Label);
        return Wrap($"MATCH (n:{label}) RETURN n", new AgeParameterBinder(), NodeAlias);
    }

    /// <inheritdoc/>
    public DataStatement EmitAsEnumerableEdges<T>(IGraphModel model)
    {
        IRelationshipMetadata relationship = model.GetRelationship(typeof(T));
        string relType = CypherIdentifier.Escape(relationship.RelationshipType);
        return Wrap($"MATCH ()-[r:{relType}]->() RETURN r", new AgeParameterBinder(), EdgeAlias);
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

        AgeParameterBinder binder = new();
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
        return Wrap(builder.ToString(), binder, ast.ReturnAlias);
    }

    /// <summary>
    /// Wraps the supplied <paramref name="cypher"/> body in the SQL <c>SELECT * FROM cypher(...)</c> envelope that Apache AGE requires, declaring a single agtype column named <paramref name="returnAlias"/>. The parameter payload, if any, is bound as <c>@p</c>.
    /// </summary>
    /// <param name="cypher">The raw Cypher body to wrap.</param>
    /// <param name="binder">The parameter binder whose accumulated values will be JSON-encoded by the executor and sent as <c>@p</c>.</param>
    /// <param name="returnAlias">The single column alias declared in the SQL <c>AS (...)</c> schema.</param>
    /// <returns>Returns the wrapped SQL <see cref="DataStatement"/> ready for <see cref="AgeRawStatementExecutor"/>.</returns>
    private DataStatement Wrap(string cypher, AgeParameterBinder binder, string returnAlias)
    {
        IReadOnlyDictionary<string, object?> parameters = binder.ToParameters();
        string columnSchema = $"{CypherIdentifier.Escape(returnAlias)} agtype";
        string sql = parameters.Count == 0
            ? $"SELECT * FROM ag_catalog.cypher('{graphName}', $$ {cypher} $$) AS ({columnSchema})"
            : $"SELECT * FROM ag_catalog.cypher('{graphName}', $$ {cypher} $$, @p) AS ({columnSchema})";
        return new DataStatement(sql, parameters);
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

    private static void AppendPredicate(
        StringBuilder builder,
        AgeParameterBinder binder,
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
        AgeParameterBinder binder,
        IGraphModel model,
        TraversalAst ast,
        PropertyComparisonPredicate predicate)
    {
        IPropertyMetadata property = ResolvePredicateProperty(model, ast, predicate.Alias, predicate.ClrPropertyName);
        string token = binder.Bind(property.Name, AgePropertySerializer.Serialize(predicate.Value));
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
        AgeParameterBinder binder,
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

    private static string KeywordFor(TraversalKind kind) => kind switch
    {
        TraversalKind.Match => "MATCH",
        TraversalKind.Merge => "MERGE",
        TraversalKind.Create => "CREATE",
        _ => throw new StatementEmissionException($"Unknown traversal kind '{kind}'.")
    };
}
