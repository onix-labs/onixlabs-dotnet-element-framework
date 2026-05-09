// All Rights Reserved License
//
// 1. Grant of License
// Subject to the terms and conditions of this License, ONIXLabs ("Licensor") hereby grants to you a limited, non-exclusive, non-transferable, non-sublicensable license to use the Software for commercial, private, and paid purposes. This license does not include any rights to modify, distribute, or create derivative works of the Software.
//
// 2. Permitted Uses
// You are permitted to:
//  - Use the Software for commercial purposes.
//  - Use the Software for private purposes.
//  - Use the Software for paid purposes.
//  - Exercise any patent rights associated with the Software, solely in connection with your use of the Software as permitted under this License.
//
// 3. Restrictions
// You are not permitted to:
//  - Modify, alter, or create any derivative works of the Software.
//  - Distribute, sublicense, lease, rent, or otherwise transfer the Software to any third party.
//  - Use the Software without obtaining a proper license for paid use.
//  - Use the Software in any way that infringes upon the trademarks, service marks, or trade names of the Licensor.
//  - Use the Software in any manner that could cause it to be considered open-source software or otherwise subject to an open-source license.
//
// 4. No Free Use
// This license does not permit any free use of the Software. Any use of the Software without a paid license is strictly prohibited.
//
// 5. No Liability
// To the maximum extent permitted by applicable law, the Software is provided "as is" and "as available" without warranty of any kind, express or implied, including but not limited to the implied warranties of merchantability, fitness for a particular purpose, and non-infringement. In no event shall the Licensor be liable for any damages whatsoever arising out of the use of or inability to use the Software, even if the Licensor has been advised of the possibility of such damages.
//
// 6. No Warranty
// The Licensor makes no warranty that the Software will meet your requirements, be uninterrupted, secure, or error-free. The Licensor disclaims all warranties with respect to the Software, whether express or implied, including but not limited to any warranties of merchantability, fitness for a particular purpose, and non-infringement.
//
// 7. Termination
// This license is effective until terminated. Your rights under this license will terminate automatically without notice if you fail to comply with any term of this license. Upon termination, you must immediately cease all use of the Software and destroy all copies of the Software in your possession or control.
//
// 8. Governing Law
// This license will be governed by and construed in accordance with the laws of [Your Jurisdiction], without regard to its conflict of laws principles.
//
// 9. Entire Agreement
// This license constitutes the entire agreement between you and the Licensor concerning the Software and supersedes all prior or contemporaneous communications, agreements, or understandings, whether oral or written, concerning the subject matter hereof.
//
// By using the Software, you acknowledge that you have read and understood this license and agree to be bound by its terms and conditions.

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
                AppendPredicate(builder, binder, model, ast, ast.Predicates[i]);
            }
        }

        builder.Append(" RETURN ").Append(CypherIdentifier.Escape(ast.ReturnAlias));
        return new DataStatement(builder.ToString(), binder.ToParameters());
    }

    private static void AppendPattern(StringBuilder builder, IGraphModel model, IReadOnlyList<PatternSegment> segments)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            switch (segments[i])
            {
                case NodePatternSegment node:
                    INodeMetadata nodeMeta = model.GetNode(node.NodeType);
                    builder.Append('(').Append(CypherIdentifier.Escape(node.Alias)).Append(':').Append(CypherIdentifier.Escape(nodeMeta.Label)).Append(')');
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

    private static void AppendPredicate(StringBuilder builder, ParameterBinder binder, IGraphModel model, TraversalAst ast, TraversalPredicate predicate)
    {
        IPropertyMetadata property = ResolvePredicateProperty(model, ast, predicate);
        string token = binder.Bind(property.Name, PropertySerializer.Serialize(predicate.Value));
        builder.Append(CypherIdentifier.Escape(predicate.Alias))
            .Append('.')
            .Append(CypherIdentifier.Escape(property.Name))
            .Append(" = ")
            .Append(token);
    }

    private static IPropertyMetadata ResolvePredicateProperty(IGraphModel model, TraversalAst ast, TraversalPredicate predicate)
    {
        PatternSegment segment = ast.Segments.FirstOrDefault(s => s.Alias == predicate.Alias)
                                 ?? throw new StatementEmissionException($"Predicate references unbound alias '{predicate.Alias}'.");

        IReadOnlyList<IPropertyMetadata> properties = segment switch
        {
            NodePatternSegment node => model.GetNode(node.NodeType).Properties,
            RelationshipPatternSegment relationship => model.GetRelationship(relationship.EdgeType).Properties,
            _ => throw new StatementEmissionException($"Unknown pattern segment type '{segment.GetType().FullName}'.")
        };

        return properties.FirstOrDefault(p => p.Property.Name == predicate.ClrPropertyName)
               ?? throw new StatementEmissionException(
                   $"Predicate references property '{predicate.ClrPropertyName}' which is not mapped on alias '{predicate.Alias}'.");
    }

    private static string KeywordFor(TraversalKind kind) => kind switch
    {
        TraversalKind.Match => "MATCH",
        TraversalKind.Merge => "MERGE",
        TraversalKind.Create => "CREATE",
        _ => throw new StatementEmissionException($"Unknown traversal kind '{kind}'.")
    };
}
