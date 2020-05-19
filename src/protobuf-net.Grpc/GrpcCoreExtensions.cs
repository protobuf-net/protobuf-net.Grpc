using Grpc.Core;

namespace ProtoBuf.Grpc
{
    /// <summary>
    /// Provides auxiliary helper methods to Grpc.Core features
    /// </summary>
    public static class GrpcCoreExtensions
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
                    if (string.Equals(header.Key, key, System.StringComparison.OrdinalIgnoreCase))
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
        public static string? GetValue(this Metadata? headers, string key)
            => headers.GetEntry(key)?.Value;

        /// <summary>
        /// Gets a header's ValueBytes by key
        /// </summary>
        public static byte[]? GetValueBytes(this Metadata? headers, string key)
            => headers.GetEntry(key)?.ValueBytes;
    }
}
