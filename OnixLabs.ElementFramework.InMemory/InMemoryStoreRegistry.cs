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

using System.Collections.Concurrent;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents a process-wide registry of named <see cref="InMemoryStore"/> instances.
/// </summary>
/// <remarks>
/// Multiple <see cref="GraphContext"/> instances that resolve the same database name share the same store, so writes
/// made through one context are visible to reads from another. Tests should pick a unique database name per test
/// class (or per test method) to keep state isolated.
/// </remarks>
public static class InMemoryStoreRegistry
{
    /// <summary>
    /// The registry of stores keyed by database name.
    /// </summary>
    private static readonly ConcurrentDictionary<string, InMemoryStore> Stores = new();

    /// <summary>
    /// Returns the store registered under <paramref name="databaseName"/>, creating an empty one on first call.
    /// </summary>
    /// <param name="databaseName">The unique name of the in-memory database.</param>
    /// <returns>Returns the shared <see cref="InMemoryStore"/> instance for the supplied name.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="databaseName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static InMemoryStore GetOrCreate(string databaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        return Stores.GetOrAdd(databaseName, name => new InMemoryStore(name));
    }

    /// <summary>
    /// Wipes the contents of the store registered under <paramref name="databaseName"/>, leaving the registration intact.
    /// Useful as a per-test reset hook.
    /// </summary>
    /// <param name="databaseName">The unique name of the in-memory database.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="databaseName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static void Reset(string databaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        if (Stores.TryGetValue(databaseName, out InMemoryStore? store))
            store.Clear();
    }

    /// <summary>
    /// Removes the store registered under <paramref name="databaseName"/> from the registry.
    /// </summary>
    /// <param name="databaseName">The unique name of the in-memory database.</param>
    /// <returns>Returns <see langword="true"/> when a store was removed; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="databaseName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static bool Drop(string databaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        return Stores.TryRemove(databaseName, out _);
    }
}
