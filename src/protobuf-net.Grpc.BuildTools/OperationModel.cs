using Microsoft.CodeAnalysis;

namespace ProtoBuf.Grpc.BuildTools;

internal enum MethodKind
{
    Unary,
    ServerStreaming,
    ClientStreaming,
    DuplexStreaming,
}

internal enum ContextKind
{
    None,
    CallContext,
    CancellationToken,
}

internal enum ResultShape
{
    Sync,        // T or void
    Task,        // Task or Task<T>
    ValueTask,   // ValueTask or ValueTask<T>
    AsyncEnumerable,
}

internal enum ArgShape
{
    Data,            // a single data parameter
    Void,            // no data parameter
    AsyncEnumerable, // streaming input
}

internal sealed record OperationModel(
    string OperationName,
    string MethodName,
    MethodKind Kind,
    ContextKind Context,
    ArgShape RequestShape,
    ResultShape ResponseShape,
    string RequestTypeFullName,   // fully-qualified data type (Empty if VoidRequest)
    string ResponseTypeFullName,  // fully-qualified data type (Empty if VoidResponse)
    bool VoidRequest,
    bool VoidResponse,
    string ReturnTypeDisplay,
    System.Collections.Immutable.ImmutableArray<ParameterModel> Parameters,
    Location? Location);

internal sealed record ParameterModel(string Name, string TypeDisplay);
