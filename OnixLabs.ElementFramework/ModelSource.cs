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
/// Provides a process-wide cache of frozen <see cref="GraphModel"/> instances keyed by <see cref="GraphContext"/> CLR type.
/// </summary>
internal static class ModelSource
{
    /// <summary>
    /// The process-wide cache of frozen <see cref="GraphModel"/> instances keyed by <see cref="GraphContext"/> CLR type.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Lazy<GraphModel>> Models = [];

    /// <summary>
    /// Returns the frozen <see cref="GraphModel"/> for the supplied context, building and caching it on first call.
    /// </summary>
    /// <param name="context">The context whose model is being requested.</param>
    /// <returns>Returns the frozen <see cref="GraphModel"/> for the supplied context's CLR type.</returns>
    /// <exception cref="ModelConfigurationException">Thrown when the model fails validation on first build.</exception>
    public static GraphModel ModelFor(GraphContext context) => Models
        .GetOrAdd(context.GetType(), static (_, ctx) => new Lazy<GraphModel>(() => Build(ctx)), context).Value;

    /// <summary>
    /// Builds the frozen <see cref="GraphModel"/> for the supplied context by invoking <see cref="GraphContext.OnModelCreating"/> on a fresh builder.
    /// </summary>
    /// <param name="context">The context whose model is being built.</param>
    /// <returns>Returns the frozen <see cref="GraphModel"/> built from the context's configuration.</returns>
    private static GraphModel Build(GraphContext context)
    {
        GraphModelBuilder builder = new();
        context.OnModelCreating(builder);
        return builder.Build();
    }
}
