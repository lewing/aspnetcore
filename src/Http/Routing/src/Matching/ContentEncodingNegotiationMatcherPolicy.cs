// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Routing.Matching;

internal sealed class ContentEncodingNegotiationMatcherPolicy : NegotiationMatcherPolicy<ContentEncodingMetadata>
{
    internal static string HeaderName => "Accept-Encoding";

    private protected override bool HasMetadata(Endpoint endpoint) => endpoint.Metadata.GetMetadata<ContentEncodingMetadata>() != null;

    private protected override string? GetMetadataValue(Endpoint endpoint) => endpoint.Metadata.GetMetadata<ContentEncodingMetadata>()?.Value;

    private protected override StringValues GetNegotiationHeader(HttpContext httpContext) => httpContext.Request.Headers[HeaderName];

    private protected override bool IsDefaultMetadataValue(ReadOnlySpan<char> candidate) =>
        MemoryExtensions.Equals("identity".AsSpan(), candidate, StringComparison.OrdinalIgnoreCase) ||
        MemoryExtensions.Equals("*".AsSpan(), candidate, StringComparison.OrdinalIgnoreCase);

    private protected override double? GetMetadataQuality(Endpoint endpoint)
    {
        var metadata = endpoint.Metadata.GetMetadata<ContentEncodingMetadata>();
        return metadata?.Quality;
    }

    private protected override byte[]? GetMetadataDictionaryHash(Endpoint endpoint) => endpoint.Metadata.GetMetadata<ContentEncodingMetadata>()?.DictionaryHashBytes;

    private protected override ReadOnlySpan<byte> GetAvailableDictionary(HttpContext httpContext, Span<byte> buffer) =>
        CompressionDictionaryHash.ReadAvailableDictionary(httpContext, buffer);

    private protected override NegotiationPolicyJumpTable CreateTable(int exitDestination, (string negotiationValue, byte[]? dictionaryHash, double quality, int destination)[] destinations, int noNegotiationHeaderDestination) => new ContentEncodingPolicyJumpTable(exitDestination, noNegotiationHeaderDestination, new ContentEncodingDestinationsLookUp(destinations));

    internal sealed class ContentEncodingPolicyJumpTable(int anyContentEncodingDestination, int noContentEncodingDestination, ContentEncodingDestinationsLookUp destinations) : NegotiationPolicyJumpTable("Accept-Encoding", anyContentEncodingDestination, noContentEncodingDestination)
    {
        private readonly ContentEncodingDestinationsLookUp _destinations = destinations;

        protected override ReadOnlySpan<byte> GetAvailableDictionary(HttpContext httpContext, Span<byte> buffer) =>
            _destinations.HasDictionaryDestinations ? CompressionDictionaryHash.ReadAvailableDictionary(httpContext, buffer) : default;

        protected override int GetDestination(string? value, ReadOnlySpan<byte> availableDictionary) => _destinations.GetDestination(value, availableDictionary);

        protected override double GetQuality(string? value, ReadOnlySpan<byte> availableDictionary) => _destinations.GetValueQuality(value, availableDictionary);
    }

    internal sealed class ContentEncodingDestinationsLookUp
    {
        private readonly int _brotliDestination = -1;
        private readonly double _brotliQuality;
        private readonly int _gzipDestination = -1;
        private readonly double _gzipQuality;
        private readonly int _identityDestination = -1;
        private readonly double _identityQuality;
        private readonly Dictionary<string, (int destination, double quality)>? _extraDestinations;
        private readonly Dictionary<string, List<(byte[] dictionaryHash, int destination, double quality)>>? _dictionaryDestinations;

        public ContentEncodingDestinationsLookUp((string contentEncoding, byte[]? dictionaryHash, double quality, int destination)[] destinations)
        {
            for (var i = 0; i < destinations.Length; i++)
            {
                var (contentEncoding, dictionaryHash, quality, destination) = destinations[i];
                if (dictionaryHash is not null)
                {
                    _dictionaryDestinations ??= new Dictionary<string, List<(byte[] dictionaryHash, int destination, double quality)>>(StringComparer.OrdinalIgnoreCase);
                    if (!_dictionaryDestinations.TryGetValue(contentEncoding, out var dictionaryDestinations))
                    {
                        dictionaryDestinations = [];
                        _dictionaryDestinations.Add(contentEncoding, dictionaryDestinations);
                    }

                    dictionaryDestinations.Add((dictionaryHash, destination, quality));
                    continue;
                }

                switch (contentEncoding.ToLowerInvariant())
                {
                    case "br":
                        _brotliDestination = destination;
                        _brotliQuality = quality;
                        break;
                    case "gzip":
                        _gzipDestination = destination;
                        _gzipQuality = quality;
                        break;
                    case "identity":
                        _identityDestination = destination;
                        _identityQuality = quality;
                        break;
                    default:
                        _extraDestinations ??= new Dictionary<string, (int destination, double quality)>(StringComparer.OrdinalIgnoreCase);
                        _extraDestinations.Add(contentEncoding, (destination, quality));
                        break;
                }
            }
        }

        public bool HasDictionaryDestinations => _dictionaryDestinations != null;

        public int GetDestination(string? negotiationValue, ReadOnlySpan<byte> availableDictionary)
        {
            // A destination that depends on a dictionary is only available when the request advertises that dictionary.
            // Otherwise, the encoding is treated as unavailable so negotiation falls back to the next acceptable encoding.
            if (TryGetDictionaryDestination(negotiationValue, availableDictionary, out var dictionaryDestination))
            {
                return dictionaryDestination.destination;
            }

            // Specialcase the lookup based on the length of the negotiation value
            // to reduce the number of required comparisons needed to find a match.
            // The match will be validated after this selection.
            var (matchedEncoding, destination) = negotiationValue?.Length switch
            {
                2 => ("br", _brotliDestination),
                4 => ("gzip", _gzipDestination),
                8 => ("identity", _identityDestination),
                _ => (null, -1)
            };

            if (matchedEncoding != null && string.Equals(negotiationValue, matchedEncoding, StringComparison.OrdinalIgnoreCase))
            {
                return destination;
            }

            if (_extraDestinations != null && negotiationValue != null && _extraDestinations.TryGetValue(negotiationValue, out var extraDestination))
            {
                return extraDestination.destination;
            }

            return -1;
        }

        public double GetValueQuality(string? negotiationValue, ReadOnlySpan<byte> availableDictionary)
        {
            if (TryGetDictionaryDestination(negotiationValue, availableDictionary, out var dictionaryDestination))
            {
                return dictionaryDestination.quality;
            }

            var (matchedEncoding, quality) = negotiationValue?.Length switch
            {
                2 => ("br", _brotliQuality),
                4 => ("gzip", _gzipQuality),
                8 => ("identity", _identityQuality),
                _ => (null, -1)
            };

            if (matchedEncoding != null && string.Equals(negotiationValue, matchedEncoding, StringComparison.OrdinalIgnoreCase))
            {
                return quality;
            }

            if (_extraDestinations != null && negotiationValue != null && _extraDestinations.TryGetValue(negotiationValue, out var extraDestination))
            {
                return extraDestination.quality;
            }

            return -1;
        }

        private bool TryGetDictionaryDestination(string? negotiationValue, ReadOnlySpan<byte> availableDictionary, out (int destination, double quality) result)
        {
            if (_dictionaryDestinations != null &&
                negotiationValue != null &&
                !availableDictionary.IsEmpty &&
                _dictionaryDestinations.TryGetValue(negotiationValue, out var dictionaryDestinations))
            {
                foreach (var (dictionaryHash, destination, quality) in dictionaryDestinations)
                {
                    if (availableDictionary.SequenceEqual(dictionaryHash))
                    {
                        result = (destination, quality);
                        return true;
                    }
                }
            }

            result = default;
            return false;
        }
    }
}
