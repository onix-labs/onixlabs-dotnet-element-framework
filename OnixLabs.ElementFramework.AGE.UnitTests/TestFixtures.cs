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

using System.Diagnostics.CodeAnalysis;

namespace OnixLabs.ElementFramework.AGE.UnitTests;

/// <summary>
/// A wide-shape node used exclusively by <see cref="AgeResultMaterializerTests"/> to exercise every conversion
/// path in <c>AgeResultMaterializer.Convert</c> (Guid string ↔ Guid, ISO-8601 string ↔ DateTimeOffset / DateTime,
/// enum string ↔ enum, long ↔ int, nullable Guid). These shapes don't belong in the blog-domain Conformance
/// fixtures because they're not modelling concerns — they're materializer-conversion concerns specific to the AGE provider.
/// </summary>
/// <remarks>
/// Every property setter is invoked via reflection from <c>AgeResultMaterializer.Materialize</c>; the IDE analyzer
/// can't see those callers, so the <see cref="SuppressMessageAttribute"/> below silences the spurious unused-accessor
/// warnings.
/// </remarks>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global", Justification = "Set via reflection by AgeResultMaterializer.")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local", Justification = "Set via reflection by AgeResultMaterializer.")]
internal sealed class TypedNode
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public double Score { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTime CreatedAtLocal { get; set; }
    public SampleStatus Status { get; set; }
    public Guid? OptionalId { get; set; }
}

internal enum SampleStatus
{
    Pending,
    Submitted,
    Shipped
}

internal static class TestModel
{
    public static GraphModel Build()
    {
        GraphModelBuilder builder = new();
        builder
            .ApplyConfiguration(new AuthorConfiguration())
            .ApplyConfiguration(new PostConfiguration())
            .ApplyConfiguration(new CommentConfiguration())
            .ApplyConfiguration(new WroteConfiguration())
            .ApplyConfiguration(new CommentOnConfiguration())
            .ApplyConfiguration(new ReplyToConfiguration());
        builder.Node<TypedNode>().HasKey(t => t.Id);
        return builder.Build();
    }
}
