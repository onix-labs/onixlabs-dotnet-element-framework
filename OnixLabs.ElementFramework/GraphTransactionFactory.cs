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

using System.Diagnostics;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Represents the per-context factory that opens transactions through the provider's <see cref="IGraphTransactionOpener"/> and wraps each one in a <see cref="RollbackAwareGraphTransaction"/>.
/// </summary>
/// <remarks>
/// The opener owns the ambient state; this factory is a thin translator from the provider seam to the internal coordination contract that ensures the owning context's identity map stays in sync with the underlying transaction's outcome.
/// </remarks>
/// <param name="opener">The provider's <see cref="IGraphTransactionOpener"/> supplied via <see cref="GraphContextOptions"/>.</param>
/// <param name="tracker">The owning context's <see cref="IChangeTracker"/> that opened transactions are bound to for rollback-aware reset.</param>
internal sealed class GraphTransactionFactory(IGraphTransactionOpener opener, IChangeTracker tracker) : IGraphTransactionFactory
{
    /// <inheritdoc/>
    public IGraphTransaction? Active => opener.Active;

    /// <inheritdoc/>
    public IGraphTransaction Open()
    {
        using Activity? activity = ElementFrameworkDiagnostics.Source.StartActivity("ElementFramework.BeginTransaction", ActivityKind.Internal);
        try
        {
            IGraphTransaction transaction = opener.Open();
            activity?.SetStatus(ActivityStatusCode.Ok);
            return new RollbackAwareGraphTransaction(transaction, tracker);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.SetTag("exception.type", exception.GetType().FullName);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IGraphTransaction> OpenAsync(CancellationToken token = default)
    {
        using Activity? activity = ElementFrameworkDiagnostics.Source.StartActivity("ElementFramework.BeginTransaction", ActivityKind.Internal);
        try
        {
            IGraphTransaction transaction = await opener.OpenAsync(token).ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return new RollbackAwareGraphTransaction(transaction, tracker);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.SetTag("exception.type", exception.GetType().FullName);
            throw;
        }
    }
}
