using ProtoBuf.Grpc.Lite.Internal;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite.Connections;

/// <summary>
/// Allows two streams to be combined, routing reads and writes accordingly.
/// </summary>
public sealed class DuplexStream : Stream
{
    private readonly Stream _read;
    private readonly Stream _write;

    /// <summary>
    /// Create a duplex stream from two separate read and write streams.
    /// </summary>
    /// <remarks>If the two streams provided are the same, the original stream is returned.</remarks>
    public static Stream Create(Stream read, Stream write)
    {
        if (read is null) throw new ArgumentNullException(nameof(read));
        if (write is null) throw new ArgumentNullException(nameof(write));
        var result = ReferenceEquals(read, write) ? read : new DuplexStream(read, write);
        return result.CheckDuplex();
    }

    private DuplexStream(Stream read, Stream write)
    {
        _read = read;
        _write = write;
    }

    // utility ops
    /// <inheritdoc/>
    public override bool CanTimeout => _read.CanTimeout || _write.CanTimeout;
    /// <inheritdoc/>
    public override int ReadTimeout
    {
        get => _read.ReadTimeout;
        set => _read.ReadTimeout = value;
    }
    /// <inheritdoc/>
    public override int WriteTimeout
    {
        get => _write.WriteTimeout;
        set => _write.WriteTimeout = value;
    }
    /// <inheritdoc/>
    public override bool CanSeek => false;
    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();
    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override string ToString() => $"{nameof(DuplexStream)}:{_read}/{_write}";

#if !NET472
    /// <inheritdoc/>
    public override ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return Utilities.SafeDisposeAsync(_read, _write);
    }
#endif

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _read.SafeDispose();
            _write.SafeDispose();
        }
    }
    /// <inheritdoc/>
    public override void Close()
    {
        _read.Close();
        _write.Close();
    }

    // read ops
    /// <inheritdoc/>
    public override bool CanRead => _read.CanRead;
    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => _read.Read(buffer, offset, count);
    /// <inheritdoc/>
    public override int ReadByte() => _read.ReadByte();


    
    /// <inheritdoc/>
    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => _read.BeginRead(buffer, offset, count, callback!, state);
    /// <inheritdoc/>
    public override int EndRead(IAsyncResult asyncResult) => _read.EndRead(asyncResult);

    /// <inheritdoc/>
    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => _read.CopyToAsync(destination, bufferSize, cancellationToken);

#if !NET472
    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _read.ReadAsync(buffer, offset, count, cancellationToken);
    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _read.ReadAsync(buffer, cancellationToken);
    /// <inheritdoc/>
    public override void CopyTo(Stream destination, int bufferSize) => _read.CopyTo(destination, bufferSize);
    /// <inheritdoc/>
    public override int Read(Span<byte> buffer) => _read.Read(buffer);
#endif

    // write ops
    /// <inheritdoc/>
    public override bool CanWrite => _write.CanWrite;
    /// <inheritdoc/>
    public override void Flush() => _write.Flush();
    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken) => _write.FlushAsync(cancellationToken);
    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => _write.Write(buffer, offset, count);

    /// <inheritdoc/>
    public override void WriteByte(byte value) => _write.WriteByte(value);
    /// <inheritdoc/>
    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => _write.BeginWrite(buffer, offset, count, callback!, state);
    /// <inheritdoc/>
    public override void EndWrite(IAsyncResult asyncResult) => _write.EndWrite(asyncResult);
    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _write.WriteAsync(buffer, offset, count, cancellationToken);
#if !NET472
    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _write.WriteAsync(buffer, cancellationToken);
    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer) => _write.Write(buffer);
#endif

}
