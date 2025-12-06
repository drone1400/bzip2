using System;
using System.IO;
using Bzip2.Interface;
using Sewer56.BitStream;
using Sewer56.BitStream.ByteStreams;
using Sewer56.BitStream.Interfaces;
namespace Bzip2.InputStream;

public class BZip2BitStreamWrapper : IBZip2BitInputStream, IBZip2BitOutputStream
{
    private class StreamWrapper : IByteStream
    {
        private Stream _stream;
        public  StreamWrapper(Stream stream)
        {
            if (stream.CanSeek == false)
                throw new ArgumentException("stream must be seekable");
            this._stream = stream;
        }
        public byte Read(int index)
        {
            if (this._stream.Position != index)
            {
                this._stream.Seek(index, SeekOrigin.Begin);
            }
            return (byte)this._stream.ReadByte();
        }

        public void Write(byte value, int index)
        {
            if (this._stream.Position != index)
            {
                this._stream.Seek(index, SeekOrigin.Begin);
            }
            this._stream.WriteByte(value);
        }
    }

    private BitStream<IByteStream> _stream;
    private bool _isOwner = true;
    private Stream? _streamBuffer = null;
    private byte[]? _internalBuffer = null;

    public int BitIndex => this._stream.BitIndex;
    public int BitOffset => this._stream.BitOffset;
    public int ByteOffset => this._stream.ByteOffset;
    public int NextByteIndex  => this._stream.NextByteIndex;

    public BZip2BitStreamWrapper(Stream stream, bool isOwner)
    {
        this._stream = new BitStream<IByteStream>(new StreamWrapper(stream));
        this._stream.Seek((int)stream.Position, 0);
        this._isOwner = isOwner;
    }

    public BZip2BitStreamWrapper(int bufferSize)
    {
        this._internalBuffer = new byte[bufferSize];
        ArrayByteStream buffer = new ArrayByteStream(this._internalBuffer);
        this._stream = new BitStream<IByteStream>(buffer);
    }

    public void SeekBitPosition(int offset, byte bit) => this._stream.Seek(offset, bit);

    public void Dispose()
    {
        if (this._isOwner)
        {
            this._streamBuffer?.Dispose();
        }
    }
    public bool ReadBoolean() => this._stream.Read<byte>(1) == 1;
    public uint ReadUnary()
    {
        for (uint unaryCount = 0; ; unaryCount++)
        {
            try
            {
                byte bit = this._stream.Read<byte>(1);
                if (bit == 0)
                    return unaryCount;
            }
            catch (IOException)
            {
                return unaryCount;
            }
        }
    }
    public uint ReadBits(int count) => this._stream.Read<uint>(count);
    public uint ReadInteger() => this._stream.Read<uint>(32);
    public void WriteBoolean(bool value) => this._stream.Write<int>(value ? 1 : 0, 1);
    public void WriteUnary(int value)
    {
        while (value >= 8)
        {
            this._stream.Write<byte>(0xFF);
            value -= 8;
        }
        while (value-- > 0)
        {
            this._stream.Write<byte>(1, 1);
        }
        this._stream.Write<byte>(0, 1);
    }
    public void WriteBits(int count, uint value) => this._stream.Write<uint>(value, count);
    public void WriteInteger(uint value) => this._stream.Write<uint>(value, 32);
    public void Flush()
    {
        if (this._stream.BitOffset != 0)
        {
            this._stream.Write<uint>(0, 8-this._stream.BitOffset);
        }

        this._streamBuffer?.Flush();
    }
}
