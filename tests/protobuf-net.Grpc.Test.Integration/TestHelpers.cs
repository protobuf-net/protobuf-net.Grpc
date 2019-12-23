namespace ProtobufNet.Grpc.Test.Integration
{
    public static class TestHelpers
    {
        public static string NormalizeNewLines(this string text) => text.Trim().Replace("\r", "");
    }
}