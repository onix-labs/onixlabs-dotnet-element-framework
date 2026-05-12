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
/// Represents the in-memory provider's <see cref="IGraphTransactionOpener"/> implementation.
/// </summary>
/// <remarks>
/// Each successful <see cref="Open"/> clones the canonical store into a transaction-scoped copy and stores the wrapping
/// <see cref="InMemoryGraphTransaction"/> as the ambient transaction. Opening a second transaction while another is
/// active throws <see cref="GraphTransactionAlreadyActiveException"/>.
/// </remarks>
/// <param name="canonical">The canonical store the opener mints transactions against.</param>
internal sealed class InMemoryGraphTransactionOpener(InMemoryStore canonical) : IGraphTransactionOpener
{
    /// <summary>
    /// The currently-active ambient transaction for this opener, or <see langword="null"/> when none is open.
    /// </summary>
    private InMemoryGraphTransaction? active;

    /// <inheritdoc/>
    public IGraphTransaction? Active => active;

    /// <summary>
    /// Gets the canonical store reads and writes target when no ambient transaction is active.
    /// </summary>
    /// <value>The canonical <see cref="InMemoryStore"/>.</value>
    internal InMemoryStore Canonical => canonical;

    /// <inheritdoc/>
    public IGraphTransaction Open()
    {
        if (active is not null)
            throw new GraphTransactionAlreadyActiveException(
                "An ambient graph transaction is already active for this context. Commit, roll back, or dispose it before opening another.");

        InMemoryGraphTransaction transaction = new(canonical, this);
        active = transaction;
        return transaction;
    }

    /// <inheritdoc/>
    public Task<IGraphTransaction> OpenAsync(CancellationToken token = default) =>
        Task.FromResult(Open());

    /// <summary>
    /// Clears the ambient transaction slot so that <see cref="Open"/> can be invoked again.
    /// </summary>
    internal void ClearActive() => active = null;
}
