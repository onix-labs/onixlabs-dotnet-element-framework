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

using System.Text;
using ArangoDBNetStandard.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OnixLabs.ElementFramework;

/// <summary>
/// ArangoDB serializer wrapper that pins <see cref="DateParseHandling.None"/> on the underlying Newtonsoft.Json reader. With the default <see cref="DateParseHandling.DateTime"/>, ISO-8601 strings are silently parsed into <see cref="DateTime"/> values with the offset converted to local time, so a <see cref="DateTimeOffset"/> property round-trips with the wrong offset (lost by the time we see it in the result row). Reading dates as raw strings lets <see cref="ArangoResultMaterializer"/> parse them with <see cref="System.Globalization.DateTimeStyles.RoundtripKind"/> and preserve the offset.
/// </summary>
internal sealed class ArangoSerialization : IApiClientSerialization
{
    private static readonly JsonSerializerSettings DeserializeSettings = new()
    {
        DateParseHandling = DateParseHandling.None
    };

    /// <inheritdoc/>
    public T DeserializeFromStream<T>(Stream stream)
    {
        using StreamReader reader = new(stream);
        using JsonTextReader jsonReader = new(reader) { DateParseHandling = DateParseHandling.None };
        JsonSerializer serializer = JsonSerializer.Create(DeserializeSettings);
        return serializer.Deserialize<T>(jsonReader)!;
    }

    /// <inheritdoc/>
    public Task<T> DeserializeFromStreamAsync<T>(Stream stream) => Task.FromResult(DeserializeFromStream<T>(stream));

    /// <inheritdoc/>
    public byte[] Serialize<T>(T item, ApiClientSerializationOptions serializationOptions) =>
        Encoding.UTF8.GetBytes(SerializeToString(item, serializationOptions));

    /// <inheritdoc/>
    public Task<byte[]> SerializeAsync<T>(T item, ApiClientSerializationOptions serializationOptions) =>
        Task.FromResult(Serialize(item, serializationOptions));

    /// <inheritdoc/>
    public string SerializeToString<T>(T item, ApiClientSerializationOptions serializationOptions)
    {
        // The default CamelCasePropertyNamesContractResolver camel-cases dictionary keys too — which
        // mangles our bind-vars payload, where keys like "WrittenAt" get rewritten to "writtenAt"
        // and then can't be found on read. ProcessDictionaryKeys = false preserves dict keys verbatim
        // while still camel-casing C# property names (ArangoDBNetStandard's own request bodies need
        // that mapping for AQL options like "BatchSize" → "batchSize").
        DefaultContractResolver resolver = new()
        {
            NamingStrategy = new CamelCaseNamingStrategy
            {
                ProcessDictionaryKeys = false,
                OverrideSpecifiedNames = true
            }
        };
        JsonSerializerSettings settings = new()
        {
            NullValueHandling = serializationOptions.IgnoreNullValues ? NullValueHandling.Ignore : NullValueHandling.Include,
            ContractResolver = serializationOptions.UseCamelCasePropertyNames ? resolver : new DefaultContractResolver(),
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind
        };
        if (serializationOptions.IgnoreMissingMember) settings.MissingMemberHandling = MissingMemberHandling.Ignore;
        return JsonConvert.SerializeObject(item, settings);
    }

    /// <inheritdoc/>
    public Task<string> SerializeToStringAsync<T>(T item, ApiClientSerializationOptions serializationOptions) =>
        Task.FromResult(SerializeToString(item, serializationOptions));
}
