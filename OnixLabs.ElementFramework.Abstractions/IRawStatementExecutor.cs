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
/// Defines an executor for raw provider statements against the underlying store.
/// </summary>
public interface IRawStatementExecutor
{
    /// <summary>
    /// Executes the supplied statement and yields each result row as a property-value dictionary keyed by binding name.
    /// </summary>
    /// <param name="statement">The statement text in the provider's native query language.</param>
    /// <param name="parameters">The parameter bindings referenced by <paramref name="statement"/>, keyed by parameter name without any provider-specific prefix.</param>
    /// <returns>An enumerable of result rows. Construction is eager; enumeration is lazy.</returns>
    /// <exception cref="RawStatementException">Thrown when the statement cannot be executed.</exception>
    IEnumerable<IReadOnlyDictionary<string, object?>> Execute(string statement, IReadOnlyDictionary<string, object?> parameters);

    /// <summary>
    /// Asynchronously executes the supplied statement and yields each result row as a property-value dictionary keyed by binding name.
    /// </summary>
    /// <param name="statement">The statement text in the provider's native query language.</param>
    /// <param name="parameters">The parameter bindings referenced by <paramref name="statement"/>, keyed by parameter name without any provider-specific prefix.</param>
    /// <param name="token">The token that may be used to cancel the enumeration.</param>
    /// <returns>An asynchronous enumerable of result rows.</returns>
    /// <exception cref="RawStatementException">Thrown when the statement cannot be executed or initial execution fails.</exception>
    IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ExecuteAsync(string statement, IReadOnlyDictionary<string, object?> parameters, CancellationToken token = default);
}
