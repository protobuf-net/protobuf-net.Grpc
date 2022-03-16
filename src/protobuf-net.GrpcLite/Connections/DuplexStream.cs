using ProtoBuf.Grpc.Lite.Internal;

namespace ProtoBuf.Grpc.Lite.Connections;

public sealed class DuplexStream : Stream
{
    private readonly Stream _read;
    private readonly Stream _write;

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
    public override bool CanTimeout => _read.CanTimeout || _write.CanTimeout;
    public override int ReadTimeout
    {
        get => _read.ReadTimeout;
        set => _read.ReadTimeout = value;
    }
    public override int WriteTimeout
    {
        get => _write.WriteTimeout;
        set => _write.WriteTimeout = value;
    }
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override string ToString() => $"{nameof(DuplexStream)}:{_read}/{_write}";

    public override ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return Utilities.SafeDisposeAsync(_read, _write);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _read.SafeDispose();
            _write.SafeDispose();
        }
    }
    public override void Close()
    {
        _read.Close();
        _write.Close();
    }

    // read ops
    public override bool CanRead => _read.CanRead;
    public override int Read(byte[] buffer, int offset, int count) => _read.Read(buffer, offset, count);
    public override int ReadByte() => _read.ReadByte();
    public override int Read(Span<byte> buffer) => _read.Read(buffer);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _read.ReadAsync(buffer, offset, count, cancellationToken);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _read.ReadAsync(buffer, cancellationToken);
    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => _read.BeginRead(buffer, offset, count, callback!, state);
    public override int EndRead(IAsyncResult asyncResult) => _read.EndRead(asyncResult);
    public override void CopyTo(Stream destination, int bufferSize) => _read.CopyTo(destination, bufferSize);
    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => _read.CopyToAsync(destination, bufferSize, cancellationToken);

    // write ops
    public override bool CanWrite => _write.CanWrite;
    public override void Flush() => _write.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _write.FlushAsync(cancellationToken);
    public override void Write(byte[] buffer, int offset, int count) => _write.Write(buffer, offset, count);
    public override void Write(ReadOnlySpan<byte> buffer) => _write.Write(buffer);
    public override void WriteByte(byte value) => _write.WriteByte(value);
    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => _write.BeginWrite(buffer, offset, count, callback!, state);
    public override void EndWrite(IAsyncResult asyncResult) => _write.EndWrite(asyncResult);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _write.WriteAsync(buffer, offset, count, cancellationToken);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _write.WriteAsync(buffer, cancellationToken);

}
