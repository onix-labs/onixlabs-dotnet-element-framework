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

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the set of extension methods for binding a graph context to an in-memory store.
/// </summary>
public static class GraphContextOptionsBuilderExtensions
{
    /// <summary>
    /// Binds the configured graph context to a process-wide named in-memory store.
    /// </summary>
    /// <remarks>
    /// Contexts sharing the same <paramref name="databaseName"/> share the same store. Use unique names per test class
    /// (or per test method) to keep state isolated, and call <see cref="InMemoryStoreRegistry.Reset(string)"/> between
    /// tests to wipe state. The in-memory provider is intended for unit tests and small demos; it is not optimised for
    /// concurrent access and only supports <see cref="TraversalKind.Match"/> traversals.
    /// </remarks>
    /// <param name="builder">The <see cref="GraphContextOptionsBuilder"/> being configured.</param>
    /// <param name="databaseName">The unique name of the in-memory database to bind to.</param>
    /// <returns>Returns the same <see cref="GraphContextOptionsBuilder"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="databaseName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static GraphContextOptionsBuilder UseInMemory(this GraphContextOptionsBuilder builder, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        InMemoryStore store = InMemoryStoreRegistry.GetOrCreate(databaseName);
        InMemoryGraphTransactionOpener opener = new(store);
        InMemoryStatementEmitter emitter = new();
        InMemoryRawStatementExecutor executor = new(opener);
        InMemoryResultMaterializer materializer = new();
        InMemoryTraversalTranslator translator = new(opener);

        return builder
            .UseStatementEmitter(emitter)
            .UseResultMaterializer(materializer)
            .UseRawStatementExecutor(executor)
            .UseGraphTransactionOpener(opener)
            .UseTraversalTranslator(translator);
    }
}
