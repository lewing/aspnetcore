// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Routing.Matching;

namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// Metadata used to negotiate which endpoint to select based on the value of the Accept-Encoding header and,
/// for content encoded with a compression dictionary, the Available-Dictionary header.
/// </summary>
/// <param name="value">The <c>Accept-Encoding</c> value this metadata represents.</param>
/// <param name="quality">The server preference to apply to break ties.</param>
public sealed class ContentEncodingMetadata(string value, double quality) : INegotiateMetadata
{
    /// <summary>
    /// Initializes a new instance of <see cref="ContentEncodingMetadata"/> for a content encoding that was produced
    /// with a compression dictionary, such as <c>dcz</c> as defined in RFC 9842.
    /// </summary>
    /// <param name="value">The <c>Accept-Encoding</c> value this metadata represents.</param>
    /// <param name="quality">The server preference to apply to break ties.</param>
    /// <param name="dictionaryHash">
    /// The hash of the compression dictionary the content was encoded with, as a structured field byte sequence
    /// (for example <c>:pZGm1Av0IEBKARczz7exkNYsZb8LzaMrV7J32a2fFG4=:</c>), or <see langword="null"/> if the content
    /// encoding doesn't depend on a dictionary.
    /// The endpoint is only selected when the <c>Available-Dictionary</c> request header contains the same hash.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="dictionaryHash"/> isn't a structured field byte sequence, or <paramref name="value"/> is <c>identity</c>.
    /// </exception>
    /// <example>
    /// <code>
    /// endpointBuilder.Metadata.Add(new ContentEncodingMetadata("dcz", 1.0, ":pZGm1Av0IEBKARczz7exkNYsZb8LzaMrV7J32a2fFG4=:"));
    /// </code>
    /// </example>
    public ContentEncodingMetadata(string value, double quality, string? dictionaryHash)
        : this(value, quality)
    {
        if (dictionaryHash is null)
        {
            return;
        }

        if (string.Equals(value, "identity", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(Resources.ContentEncodingMetadata_IdentityCannotHaveDictionaryHash, nameof(dictionaryHash));
        }

        Span<byte> buffer = stackalloc byte[CompressionDictionaryHash.MaxHashLength];
        if (!CompressionDictionaryHash.TryParse(dictionaryHash, buffer, out var bytesWritten))
        {
            throw new ArgumentException(Resources.ContentEncodingMetadata_InvalidDictionaryHash, nameof(dictionaryHash));
        }

        DictionaryHash = dictionaryHash;
        DictionaryHashBytes = buffer[..bytesWritten].ToArray();
    }

    /// <summary>
    /// Gets the <c>Accept-Encoding</c> value this metadata represents.
    /// </summary>
    public string Value { get; } = value;

    /// <summary>
    /// Gets the server preference to apply to break ties when two or more client options have the same preference.
    /// </summary>
    public double Quality { get; } = quality;

    /// <summary>
    /// Gets the hash of the compression dictionary the content was encoded with, as a structured field byte sequence,
    /// or <see langword="null"/> if the content encoding doesn't depend on a dictionary.
    /// </summary>
    public string? DictionaryHash { get; }

    internal byte[]? DictionaryHashBytes { get; }
}
