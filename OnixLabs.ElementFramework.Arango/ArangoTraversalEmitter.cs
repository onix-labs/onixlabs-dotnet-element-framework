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
/// Translates a <see cref="TraversalAst"/> into AQL. This is the architectural validation we set out to prove with the Arango provider — that the framework's linear segment-list AST, designed against Cypher patterns, can be re-expressed in a fundamentally different query language (AQL's nested <c>FOR ... IN ... OUTBOUND</c> graph traversal).
/// </summary>
/// <remarks>
/// The translation is straightforward because both AQL and Cypher are LPG-friendly: the linear AST maps to nested AQL <c>FOR</c> loops, one per (relationship, end-node) pair, with a single <c>FOR start IN startCollection</c> at the outermost. <see cref="RelationshipDirection.Outgoing"/> becomes <c>OUTBOUND</c>; <see cref="RelationshipDirection.Incoming"/> becomes <c>INBOUND</c>; <see cref="RelationshipDirection.Either"/> becomes <c>ANY</c>. Predicates lower to AQL <c>FILTER</c>, ordering to <c>SORT</c>, and skip/take to <c>LIMIT</c>. The return projection is <c>RETURN { alias: aliasValue }</c> so the materializer's existing row-by-alias reader works unchanged. Only <see cref="TraversalKind.Match"/> is supported; <c>Merge</c> / <c>Create</c> throw.
/// </remarks>
internal static class ArangoTraversalEmitter
{
    public static DataStatement Emit(IGraphModel model, TraversalAst ast)
    {
        if (ast.Segments.Count == 0)
            throw new StatementEmissionException("Traversal must contain at least one segment.");

        if (ast.Segments[0] is not NodePatternSegment)
            throw new StatementEmissionException("The first segment of a traversal must be a node segment.");

        return ast.Kind switch
        {
            TraversalKind.Match => EmitMatch(model, ast),
            TraversalKind.Create => EmitCreateOrMerge(model, ast, upsert: false),
            TraversalKind.Merge => EmitCreateOrMerge(model, ast, upsert: true),
            _ => throw new StatementEmissionException($"Unknown traversal kind '{ast.Kind}'.")
        };
    }

    private static DataStatement EmitMatch(IGraphModel model, TraversalAst ast)
    {
        ParameterBag parameters = new();
        StringBuilder aql = new();

        EmitPattern(model, ast.Segments, aql, parameters);
        EmitFilters(model, ast.Segments, ast.Predicates, aql, parameters);
        EmitSorts(model, ast.Segments, ast.Orderings, aql);
        EmitLimit(ast.Skip, ast.Take, aql, parameters);
        EmitReturn(ast.ReturnAlias, aql);

        return new DataStatement(aql.ToString(), parameters.Bindings);
    }

    /// <summary>
    /// Emits AQL for <see cref="TraversalKind.Create"/> and <see cref="TraversalKind.Merge"/> on a relationship pattern (Node–Rel–Node). The pattern walks every (start, end) pair matching the filters, then either unconditionally inserts a new edge (Create) or upserts one if absent (Merge). Longer patterns are not supported: Cypher's CREATE/MERGE on a chain extends ambiguously across multiple edges, and we'd need a separate AST shape to express that intent in AQL.
    /// </summary>
    private static DataStatement EmitCreateOrMerge(IGraphModel model, TraversalAst ast, bool upsert)
    {
        if (ast.Segments.Count != 3)
            throw new StatementEmissionException(
                $"Arango traversal Create/Merge requires exactly three segments (Node, Relationship, Node); got {ast.Segments.Count}.");
        if (ast.Segments[1] is not RelationshipPatternSegment relationship)
            throw new StatementEmissionException("The middle segment of a Create/Merge traversal must be a relationship segment.");
        if (ast.Segments[2] is not NodePatternSegment endNode)
            throw new StatementEmissionException("The final segment of a Create/Merge traversal must be a node segment.");

        NodePatternSegment startNode = (NodePatternSegment)ast.Segments[0];
        ParameterBag parameters = new();
        StringBuilder aql = new();

        string startCollection = parameters.AddCollection(model.GetNode(startNode.NodeType).Label);
        string endCollection = parameters.AddCollection(model.GetNode(endNode.NodeType).Label);
        string edgeCollection = parameters.AddCollection(model.GetRelationship(relationship.EdgeType).RelationshipType);

        // Resolve the wire direction: Outgoing means start->end (_from = start, _to = end).
        // Incoming flips. Either is ambiguous for write — default to Outgoing.
        (string fromAlias, string toAlias) = relationship.Direction == RelationshipDirection.Incoming
            ? (endNode.Alias, startNode.Alias)
            : (startNode.Alias, endNode.Alias);

        aql.Append("FOR ").Append(startNode.Alias).Append(" IN @@").Append(startCollection)
            .Append(" FOR ").Append(endNode.Alias).Append(" IN @@").Append(endCollection);

        EmitFilters(model, ast.Segments, ast.Predicates, aql, parameters);

        string edgeShape = $"{{ _from: {fromAlias}._id, _to: {toAlias}._id }}";
        if (upsert)
            aql.Append(' ').Append("UPSERT ").Append(edgeShape)
                .Append(" INSERT ").Append(edgeShape)
                .Append(" UPDATE {} IN @@").Append(edgeCollection);
        else
            aql.Append(' ').Append("INSERT ").Append(edgeShape)
                .Append(" INTO @@").Append(edgeCollection);

        // After INSERT / UPSERT the new (or merged) edge is bound to NEW. Surface it under the
        // requested return alias so the materializer reads it like any traversal-returned edge.
        aql.Append(" RETURN { ").Append(ast.ReturnAlias).Append(": NEW }");

        return new DataStatement(aql.ToString(), parameters.Bindings);
    }

    /// <summary>
    /// Emits the nested <c>FOR</c> loops that walk the pattern.
    /// </summary>
    private static void EmitPattern(IGraphModel model, IReadOnlyList<PatternSegment> segments, StringBuilder aql, ParameterBag parameters)
    {
        NodePatternSegment startNode = (NodePatternSegment)segments[0];
        string startCollectionParam = parameters.AddCollection(model.GetNode(startNode.NodeType).Label);
        aql.Append("FOR ").Append(startNode.Alias).Append(" IN @@").Append(startCollectionParam);

        for (int i = 1; i < segments.Count; i += 2)
        {
            if (segments[i] is not RelationshipPatternSegment relationship)
                throw new StatementEmissionException($"Expected a relationship segment at position {i}; got '{segments[i].GetType().Name}'.");
            if (i + 1 >= segments.Count || segments[i + 1] is not NodePatternSegment endNode)
                throw new StatementEmissionException($"Relationship segment at position {i} must be followed by a node segment.");

            string previousNodeAlias = segments[i - 1].Alias;
            string edgeCollectionParam = parameters.AddCollection(model.GetRelationship(relationship.EdgeType).RelationshipType);
            string direction = relationship.Direction switch
            {
                RelationshipDirection.Outgoing => "OUTBOUND",
                RelationshipDirection.Incoming => "INBOUND",
                RelationshipDirection.Either => "ANY",
                _ => throw new StatementEmissionException($"Unknown relationship direction '{relationship.Direction}'.")
            };

            aql.Append(" FOR ").Append(endNode.Alias).Append(", ").Append(relationship.Alias)
                .Append(" IN 1..1 ").Append(direction)
                .Append(' ').Append(previousNodeAlias)
                .Append(" @@").Append(edgeCollectionParam);
        }
    }

    private static void EmitFilters(
        IGraphModel model,
        IReadOnlyList<PatternSegment> segments,
        IReadOnlyList<TraversalPredicate> predicates,
        StringBuilder aql,
        ParameterBag parameters)
    {
        if (predicates.Count == 0) return;

        aql.Append(" FILTER ");
        for (int i = 0; i < predicates.Count; i++)
        {
            if (i > 0) aql.Append(" AND ");
            EmitPredicate(model, segments, predicates[i], aql, parameters);
        }
    }

    private static void EmitPredicate(
        IGraphModel model,
        IReadOnlyList<PatternSegment> segments,
        TraversalPredicate predicate,
        StringBuilder aql,
        ParameterBag parameters)
    {
        switch (predicate)
        {
            case PropertyComparisonPredicate property:
                EmitPropertyComparison(model, segments, property, aql, parameters);
                break;
            case StringComparisonPredicate stringComparison:
                EmitStringComparison(model, segments, stringComparison, aql, parameters);
                break;
            case NullPredicate nullCheck:
                EmitNullCheck(model, segments, nullCheck, aql);
                break;
            case AndPredicate and:
                aql.Append('(');
                EmitPredicate(model, segments, and.Left, aql, parameters);
                aql.Append(" AND ");
                EmitPredicate(model, segments, and.Right, aql, parameters);
                aql.Append(')');
                break;
            case OrPredicate or:
                aql.Append('(');
                EmitPredicate(model, segments, or.Left, aql, parameters);
                aql.Append(" OR ");
                EmitPredicate(model, segments, or.Right, aql, parameters);
                aql.Append(')');
                break;
            case NotPredicate not:
                aql.Append("NOT (");
                EmitPredicate(model, segments, not.Inner, aql, parameters);
                aql.Append(')');
                break;
            default:
                throw new StatementEmissionException($"Unknown traversal predicate '{predicate.GetType().Name}'.");
        }
    }

    private static void EmitPropertyComparison(
        IGraphModel model,
        IReadOnlyList<PatternSegment> segments,
        PropertyComparisonPredicate predicate,
        StringBuilder aql,
        ParameterBag parameters)
    {
        string storageName = ResolveStorageName(model, segments, predicate.Alias, predicate.ClrPropertyName);
        string op = predicate.Operator switch
        {
            ComparisonOperator.Equal => "==",
            ComparisonOperator.NotEqual => "!=",
            ComparisonOperator.LessThan => "<",
            ComparisonOperator.LessThanOrEqual => "<=",
            ComparisonOperator.GreaterThan => ">",
            ComparisonOperator.GreaterThanOrEqual => ">=",
            _ => throw new StatementEmissionException($"Unknown comparison operator '{predicate.Operator}'.")
        };
        string paramName = parameters.AddValue(ArangoPropertySerializer.Serialize(predicate.Value));
        aql.Append(predicate.Alias).Append('.').Append(storageName).Append(' ').Append(op).Append(" @").Append(paramName);
    }

    private static void EmitStringComparison(
        IGraphModel model,
        IReadOnlyList<PatternSegment> segments,
        StringComparisonPredicate predicate,
        StringBuilder aql,
        ParameterBag parameters)
    {
        string storageName = ResolveStorageName(model, segments, predicate.Alias, predicate.ClrPropertyName);
        string paramName = parameters.AddValue(predicate.Value);
        switch (predicate.Operator)
        {
            case StringComparisonOperator.Contains:
                aql.Append("CONTAINS(").Append(predicate.Alias).Append('.').Append(storageName).Append(", @").Append(paramName).Append(')');
                break;
            case StringComparisonOperator.StartsWith:
                aql.Append("STARTS_WITH(").Append(predicate.Alias).Append('.').Append(storageName).Append(", @").Append(paramName).Append(')');
                break;
            case StringComparisonOperator.EndsWith:
                // AQL has no built-in ENDS_WITH function in 3.12, so we fall back to a SUBSTRING comparison
                // anchored at the right-hand side. Safer than LIKE() with a "%..." pattern, which would
                // collide with literal % / _ characters in the suffix.
                aql.Append("SUBSTRING(").Append(predicate.Alias).Append('.').Append(storageName)
                    .Append(", LENGTH(").Append(predicate.Alias).Append('.').Append(storageName)
                    .Append(") - LENGTH(@").Append(paramName).Append(")) == @").Append(paramName);
                break;
            default:
                throw new StatementEmissionException($"Unknown string comparison operator '{predicate.Operator}'.");
        }
    }

    private static void EmitNullCheck(
        IGraphModel model,
        IReadOnlyList<PatternSegment> segments,
        NullPredicate predicate,
        StringBuilder aql)
    {
        string storageName = ResolveStorageName(model, segments, predicate.Alias, predicate.ClrPropertyName);
        aql.Append(predicate.Alias).Append('.').Append(storageName).Append(predicate.IsNull ? " == null" : " != null");
    }

    private static void EmitSorts(
        IGraphModel model,
        IReadOnlyList<PatternSegment> segments,
        IReadOnlyList<TraversalOrdering> orderings,
        StringBuilder aql)
    {
        if (orderings.Count == 0) return;

        aql.Append(" SORT ");
        for (int i = 0; i < orderings.Count; i++)
        {
            if (i > 0) aql.Append(", ");
            TraversalOrdering ordering = orderings[i];
            string storageName = ResolveStorageName(model, segments, ordering.Alias, ordering.ClrPropertyName);
            aql.Append(ordering.Alias).Append('.').Append(storageName).Append(ordering.Direction == OrderDirection.Ascending ? " ASC" : " DESC");
        }
    }

    private static void EmitLimit(int? skip, int? take, StringBuilder aql, ParameterBag parameters)
    {
        if (skip is null && take is null) return;

        aql.Append(" LIMIT ");
        if (skip is not null)
        {
            string skipParam = parameters.AddValue((long)skip.Value);
            aql.Append('@').Append(skipParam).Append(", ");
        }
        // AQL requires a numeric count after LIMIT; when only Skip was supplied we fall back to a very
        // large value since AQL has no "infinite" sentinel. Long.MaxValue is the documented upper bound.
        string takeParam = parameters.AddValue(take is null ? long.MaxValue : (long)take.Value);
        aql.Append('@').Append(takeParam);
    }

    private static void EmitReturn(string alias, StringBuilder aql) =>
        aql.Append(" RETURN { ").Append(alias).Append(": ").Append(alias).Append(" }");

    private static string ResolveStorageName(IGraphModel model, IReadOnlyList<PatternSegment> segments, string alias, string clrPropertyName)
    {
        PatternSegment segment = segments.FirstOrDefault(s => s.Alias == alias)
            ?? throw new StatementEmissionException($"Predicate references unknown alias '{alias}'.");

        IReadOnlyList<IPropertyMetadata> properties = segment switch
        {
            NodePatternSegment node => model.GetNode(node.NodeType).Properties,
            RelationshipPatternSegment relationship => model.GetRelationship(relationship.EdgeType).Properties,
            _ => throw new StatementEmissionException($"Unknown segment kind '{segment.GetType().Name}'.")
        };

        IPropertyMetadata property = properties.FirstOrDefault(p => p.Property.Name == clrPropertyName)
            ?? throw new StatementEmissionException($"Alias '{alias}' has no property named '{clrPropertyName}'.");

        return property.Name;
    }

    /// <summary>
    /// Tracks the bind-variable dictionary as the emitter walks the AST. Generates fresh, ordinal names for value (<c>p0</c>, <c>p1</c>, ...) and collection (<c>@c0</c>, <c>@c1</c>, ...) bindings.
    /// </summary>
    private sealed class ParameterBag
    {
        public Dictionary<string, object?> Bindings { get; } = new(StringComparer.Ordinal);
        private int valueIndex;
        private int collectionIndex;

        public string AddValue(object? value)
        {
            string name = $"p{valueIndex++}";
            Bindings[name] = value;
            return name;
        }

        public string AddCollection(string collectionName)
        {
            string name = $"c{collectionIndex++}";
            Bindings["@" + name] = collectionName;
            return name;
        }
    }
}
