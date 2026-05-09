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
/// Defines an atomic boundary for graph operations.
/// </summary>
public interface IGraphTransaction : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Commits the transaction, making all flushed writes durable. Once called, the transaction is finished and further calls are invalid.
    /// </summary>
    /// <exception cref="GraphTransactionException">Thrown when the commit fails.</exception>
    void Commit();

    /// <summary>
    /// Asynchronously commits the transaction, making all flushed writes durable. Once called, the transaction is finished and further calls are invalid.
    /// </summary>
    /// <param name="token">The token that may be used to cancel the operation.</param>
    /// <exception cref="GraphTransactionException">Thrown when the commit fails.</exception>
    Task CommitAsync(CancellationToken token = default);

    /// <summary>
    /// Rolls back the transaction, discarding all flushed writes. Once called, the transaction is finished and further calls are invalid.
    /// </summary>
    /// <exception cref="GraphTransactionException">Thrown when the rollback fails.</exception>
    void Rollback();

    /// <summary>
    /// Asynchronously rolls back the transaction, discarding all flushed writes. Once called, the transaction is finished and further calls are invalid.
    /// </summary>
    /// <param name="token">The token that may be used to cancel the operation.</param>
    /// <exception cref="GraphTransactionException">Thrown when the rollback fails.</exception>
    Task RollbackAsync(CancellationToken token = default);
}
