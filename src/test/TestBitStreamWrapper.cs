using System.IO;
using Bzip2.InputStream;
using Bzip2.Interface;
using Bzip2.OutputStream;
using Xunit;
namespace Bzip2.test;

public class TestBitStreamWrapper
{
    [Fact]
    public void TestBitStreamReadWriteSeekBit()
    {
        BZip2BitStreamWrapper wrapper = new BZip2BitStreamWrapper(64);

        wrapper.WriteBoolean(true);
        wrapper.WriteBoolean(false);
        wrapper.WriteBoolean(true);

        wrapper.SeekBitPosition(0,0);
        Assert.Equal(true, wrapper.ReadBoolean());
        wrapper.SeekBitPosition(0,2);
        Assert.Equal(true, wrapper.ReadBoolean());
        wrapper.SeekBitPosition(0,1);
        Assert.Equal(false, wrapper.ReadBoolean());
    }

    [Fact]
    public void TestBitStreamReadWriteSeekInteger()
    {
        BZip2BitStreamWrapper wrapper = new BZip2BitStreamWrapper(64);

        uint val1 = 123456;
        uint val2 = 813847;
        uint val3 = 194837;

        wrapper.WriteInteger(val1);
        wrapper.WriteInteger(val2);
        wrapper.WriteInteger(val3);

        wrapper.SeekBitPosition(0,0);
        Assert.Equal(val1, wrapper.ReadInteger());
        wrapper.SeekBitPosition(0,64);
        Assert.Equal(val3, wrapper.ReadInteger());
        wrapper.SeekBitPosition(0,32);
        Assert.Equal(val2, wrapper.ReadInteger());
    }

    [Fact]
    public void TestBitStreamReadWriteBits()
    {
        BZip2BitStreamWrapper wrapper = new BZip2BitStreamWrapper(64);

        wrapper.WriteBits(7, 0xFFFF);
        wrapper.WriteBits(8, 0xA3);
        wrapper.WriteBits(3, 0xF0);
        wrapper.WriteBits(3, 0x0F);


        wrapper.SeekBitPosition(0,0);
        Assert.Equal((uint)0x7F, wrapper.ReadBits(7));
        Assert.Equal((uint)0xA3, wrapper.ReadBits(8));
        Assert.Equal((uint)0x00, wrapper.ReadBits(3));
        Assert.Equal((uint)0x07, wrapper.ReadBits(3));
    }

    [Fact]
    public void TestBitStreamReadWritekUnary()
    {
        BZip2BitStreamWrapper wrapper = new BZip2BitStreamWrapper(64);

        int unary1 = 3;
        int unary2 = 29;
        int unary3 = 7;

        wrapper.WriteUnary(unary1);
        wrapper.WriteUnary(unary2);
        wrapper.WriteUnary(unary3);

        wrapper.SeekBitPosition(0,0);
        Assert.Equal((uint)unary1, wrapper.ReadUnary());
        Assert.Equal((uint)unary2, wrapper.ReadUnary());
        Assert.Equal((uint)unary3, wrapper.ReadUnary());
    }

    [Fact]
    public void TestBitStreamFlush()
    {
        MemoryStream ms = new MemoryStream(64);
        IBZip2BitOutputStream test1 = new BZip2BitOutputStream(ms);
        test1.WriteBits(7, 0xFFFF);
        test1.Flush();
        test1.WriteBits(8, 0xA3);
        test1.Flush();
        test1.WriteBits(3, 0xF0);
        test1.Flush();
        test1.WriteBits(3, 0x0F);
        test1.Flush();

        BZip2BitStreamWrapper wrapper = new BZip2BitStreamWrapper(64);

        wrapper.WriteBits(7, 0xFFFF);
        wrapper.Flush();
        wrapper.WriteBits(8, 0xA3);
        wrapper.Flush();
        wrapper.WriteBits(3, 0xF0);
        wrapper.Flush();
        wrapper.WriteBits(3, 0x0F);
        wrapper.Flush();

        wrapper.SeekBitPosition(0,0);
        Assert.Equal((uint)ms.GetBuffer()[0], wrapper.ReadBits(8));
        Assert.Equal((uint)ms.GetBuffer()[1], wrapper.ReadBits(8));
        Assert.Equal((uint)ms.GetBuffer()[2], wrapper.ReadBits(8));
        Assert.Equal((uint)ms.GetBuffer()[3], wrapper.ReadBits(8));
    }
}
