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

using ArangoDBNetStandard;
using ArangoDBNetStandard.CollectionApi.Models;

namespace OnixLabs.ElementFramework;

/// <summary>
/// One-time schema bootstrap that ensures every collection implied by a <see cref="GraphContext"/>'s model exists in the target ArangoDB database.
/// </summary>
/// <remarks>
/// ArangoDB, unlike Cypher-family stores, does not auto-create collections on first write — INSERT against an unknown collection fails. The provider's emitter targets <see cref="INodeMetadata.Label"/> for document collections and <see cref="IRelationshipMetadata.RelationshipType"/> for edge collections, so this bootstrap walks <see cref="IGraphModel.Nodes"/> and <see cref="IGraphModel.Relationships"/> and creates any missing collections of the matching type.
/// <para>
/// Call <see cref="EnsureCollectionsAsync"/> once at application startup (or once per test class in integration tests). Subsequent calls are idempotent: already-existing collections are left alone, and creating two collections concurrently is fine — ArangoDB serializes them and the second call is a no-op.
/// </para>
/// </remarks>
public static class ArangoSchemaBootstrap
{
    /// <summary>
    /// Ensures every document collection (one per registered node label) and every edge collection (one per registered relationship type) exists in the database bound to <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The <see cref="GraphContext"/> whose model drives the schema.</param>
    /// <param name="endpoint">The ArangoDB endpoint URI.</param>
    /// <param name="databaseName">The database name to bind to.</param>
    /// <param name="username">The username for basic auth.</param>
    /// <param name="password">The password for basic auth.</param>
    /// <param name="token">A cancellation token observed for each underlying HTTP call.</param>
    /// <returns>Returns a task that completes when every required collection has been ensured.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/>, <paramref name="endpoint"/>, or <paramref name="password"/> is <see langword="null"/>.</exception>
    public static async Task EnsureCollectionsAsync(
        GraphContext context,
        Uri endpoint,
        string databaseName,
        string username,
        string password,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);

        ArangoDBClient client = ArangoClientCache.GetOrCreate(endpoint, databaseName, username, password);

        foreach (INodeMetadata node in context.Model.Nodes)
            await EnsureCollectionAsync(client, node.Label, CollectionType.Document, token).ConfigureAwait(false);

        foreach (IRelationshipMetadata relationship in context.Model.Relationships)
            await EnsureCollectionAsync(client, relationship.RelationshipType, CollectionType.Edge, token).ConfigureAwait(false);
    }

    private static async Task EnsureCollectionAsync(ArangoDBClient client, string name, CollectionType type, CancellationToken token)
    {
        try
        {
            await client.Collection.GetCollectionAsync(name).ConfigureAwait(false);
        }
        catch
        {
            // 404 — create it. Any other error will resurface from PostCollectionAsync.
            await client.Collection.PostCollectionAsync(new PostCollectionBody { Name = name, Type = type }).ConfigureAwait(false);
        }

        _ = token;
    }
}
