using Grpc.Core;
using System;
using System.Globalization;

namespace ProtoBuf.Grpc
{
    /// <summary>
    /// Provides auxiliary helper methods to Grpc.Core features
    /// </summary>
    public static class MetadataExtensions
    {
        /// <summary>
        /// Gets a header Entry by key
        /// </summary>
        public static Metadata.Entry? GetEntry(this Metadata? headers, string key)
        {
            if (headers != null)
            {
                var count = headers.Count;
                for (int i = 0; i < count; i++)
                {
                    var header = headers[i];
                    if (string.Equals(header.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return header;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a header's Value by key
        /// </summary>
        public static string? GetString(this Metadata? headers, string key)
            => headers.GetEntry(key)?.Value;

        /// <summary>
        /// Gets a header's ValueBytes by key
        /// </summary>
        public static byte[]? GetBytes(this Metadata? headers, string key)
            => headers.GetEntry(key)?.ValueBytes;

        /// <summary>
        /// Adds a header from an integer value using invariant formatting
        /// </summary>
        public static void Add(this Metadata headers, string key, int value)
            => headers.Add(key, value.ToString(NumberFormatInfo.InvariantInfo));

        /// <summary>
        /// Reads a header as an integer value using invariant formatting
        /// </summary>
        public static int? GetInt32(this Metadata? headers, string key)
        {
            var value = headers.GetEntry(key)?.Value;
            if (value is null) return null;
            try
            {
                return int.Parse(value, NumberFormatInfo.InvariantInfo);
            }
            catch(Exception ex)
            {
                ex.Data?.Add(nameof(key), key);
                throw;
            }
        }
    }
}
