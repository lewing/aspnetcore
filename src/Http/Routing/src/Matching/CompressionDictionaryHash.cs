// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Routing.Matching;

// Helpers for the dictionary hashes used by Compression Dictionary Transport (RFC 9842).
// Hashes are represented as structured field byte sequences (RFC 9651), for example
// "Available-Dictionary: :pZGm1Av0IEBKARczz7exkNYsZb8LzaMrV7J32a2fFG4=:".
internal static class CompressionDictionaryHash
{
    internal const string AvailableDictionaryHeaderName = "Available-Dictionary";

    // SHA-256 (32 bytes) is the only hash defined today; leave room for a longer algorithm.
    internal const int MaxHashLength = 64;

    private const int MaxBase64Length = (MaxHashLength + 2) / 3 * 4;

    public static bool TryParse(ReadOnlySpan<char> value, Span<byte> destination, out int bytesWritten)
    {
        bytesWritten = 0;

        // Structured field parsing discards leading and trailing spaces.
        value = value.Trim(' ');
        if (value.Length < 3 || value[0] != ':')
        {
            return false;
        }

        var end = value[1..].IndexOf(':') + 1;
        if (end <= 1)
        {
            return false;
        }

        // A structured field item can carry parameters (";key=value") after the bare item. They don't affect the hash.
        var parameters = value[(end + 1)..];
        if (!parameters.IsEmpty && parameters[0] != ';')
        {
            return false;
        }

        var base64 = value[1..end];
        if (base64.Length > MaxBase64Length || base64.ContainsAny(" \t\r\n"))
        {
            return false;
        }

        // RFC 9651 recommends that parsers accept byte sequences without '=' padding.
        var remainder = base64.Length % 4;
        if (remainder == 1)
        {
            return false;
        }

        if (remainder == 0)
        {
            return Convert.TryFromBase64Chars(base64, destination, out bytesWritten) && bytesWritten > 0;
        }

        Span<char> padded = stackalloc char[MaxBase64Length];
        base64.CopyTo(padded);
        var paddedLength = base64.Length + (4 - remainder);
        padded[base64.Length..paddedLength].Fill('=');
        return Convert.TryFromBase64Chars(padded[..paddedLength], destination, out bytesWritten) && bytesWritten > 0;
    }

    // Reads the Available-Dictionary request header. Returns an empty span if the header is missing or malformed.
    public static ReadOnlySpan<byte> ReadAvailableDictionary(HttpContext httpContext, Span<byte> buffer)
    {
        var header = httpContext.Request.Headers[AvailableDictionaryHeaderName];

        // The header is a single structured field item.
        if (header.Count != 1 || !TryParse(header[0], buffer, out var bytesWritten))
        {
            return default;
        }

        return buffer[..bytesWritten];
    }
}
