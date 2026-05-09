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
/// Defines a translator from a <see cref="TraversalAst"/> to an executable provider query, with materialization of the result.
/// </summary>
public interface ITraversalTranslator
{
    /// <summary>
    /// Translates and executes the supplied traversal AST, materializing the results as <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">The CLR type of each materialized result.</typeparam>
    /// <param name="model">The frozen graph model used for translation.</param>
    /// <param name="ast">The traversal AST produced by the fluent builder.</param>
    /// <returns>An enumerable of materialized results. Construction is eager; enumeration is lazy.</returns>
    /// <exception cref="TraversalTranslationException">Thrown when the traversal cannot be translated or initial execution fails.</exception>
    IEnumerable<TResult> Translate<TResult>(IGraphModel model, TraversalAst ast);

    /// <summary>
    /// Asynchronously translates and executes the supplied traversal AST, materializing the results as <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">The CLR type of each materialized result.</typeparam>
    /// <param name="model">The frozen graph model used for translation.</param>
    /// <param name="ast">The traversal AST produced by the fluent builder.</param>
    /// <param name="token">The token that may be used to cancel the enumeration.</param>
    /// <returns>An asynchronous enumerable of materialized results.</returns>
    /// <exception cref="TraversalTranslationException">Thrown when the traversal cannot be translated or initial execution fails.</exception>
    IAsyncEnumerable<TResult> TranslateAsync<TResult>(IGraphModel model, TraversalAst ast, CancellationToken token = default);
}
