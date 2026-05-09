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
/// Defines a per-context factory that opens new graph transactions and exposes the active ambient transaction.
/// </summary>
internal interface IGraphTransactionFactory
{
    /// <summary>
    /// Gets the currently active ambient transaction, or <see langword="null"/> when none is active.
    /// </summary>
    /// <value>The ambient <see cref="IGraphTransaction"/>, or <see langword="null"/> when none is active.</value>
    IGraphTransaction? Active { get; }

    /// <summary>
    /// Opens a new graph transaction and stores it as the ambient transaction.
    /// </summary>
    /// <returns>Returns the newly opened transaction.</returns>
    /// <exception cref="GraphTransactionAlreadyActiveException">Thrown when another transaction is already active on the bound context.</exception>
    /// <exception cref="GraphTransactionException">Thrown when the transaction cannot be opened.</exception>
    IGraphTransaction Open();

    /// <summary>
    /// Asynchronously opens a new graph transaction and stores it as the ambient transaction.
    /// </summary>
    /// <param name="token">The token that may be used to cancel the operation.</param>
    /// <returns>Returns a task that resolves to the newly opened transaction.</returns>
    /// <exception cref="GraphTransactionAlreadyActiveException">Thrown when another transaction is already active on the bound context.</exception>
    /// <exception cref="GraphTransactionException">Thrown when the transaction cannot be opened.</exception>
    Task<IGraphTransaction> OpenAsync(CancellationToken token = default);
}
