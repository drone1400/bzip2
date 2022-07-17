// Added by drone1400, July 2022
// Location: https://github.com/drone1400/bzip2

using NUnit.Framework;
using System;
using System.IO;

namespace Bzip2.test;


public class TestsPrallelOutputStream {
    [Test]
    public void RandomLongTestX1() {
        this.RandomLongTestX(1);
    }
    
    [Test]
    public void RandomLongTestX10() {
        this.RandomLongTestX(10);
    }
    [Test]
    public void RandomLongTestX100() {
        this.RandomLongTestX(100);
    }
    
    [Test]
    public void RandomLongTestX1000() {
        this.RandomLongTestX(1000);
    }

    [Test]
    public void RandomLongTestWithRepeatedValuesX1() {
        this.RandomLongTestWithRepeatedValuesX(1);
    }
    
    [Test]
    public void RandomLongTestWithRepeatedValuesX10() {
        this.RandomLongTestWithRepeatedValuesX(10);
    }
    
    [Test]
    public void RandomLongTestWithRepeatedValuesX100() {
        this.RandomLongTestWithRepeatedValuesX(100);
    }
    
    [Test]
    public void RandomLongTestWithRepeatedValuesX1000() {
        this.RandomLongTestWithRepeatedValuesX(1000);
    }

    private void RandomLongTestX(int x) {
        TimeSpan totalCompressionTime = TimeSpan.Zero;
        TimeSpan totalDecompressionTime = TimeSpan.Zero;
        for (int i = 0; i < x; i++) {
            (var comp, var decomp) = this.RandomLongTest();
            totalCompressionTime += comp;
            totalDecompressionTime += decomp;
        }
        Console.Write($"AVERAGE {totalCompressionTime.TotalMilliseconds / x} ms compression time... ");
        Console.Write($"AVERAGE {totalDecompressionTime.TotalMilliseconds / x} ms decompression time... ");
        Assert.Pass();
    }
    
    private void RandomLongTestWithRepeatedValuesX(int x) {
        TimeSpan totalCompressionTime = TimeSpan.Zero;
        TimeSpan totalDecompressionTime = TimeSpan.Zero;
        for (int i = 0; i < x; i++) {
            (var comp, var decomp) = this.RandomLongTestWithRepeatedValues();
            totalCompressionTime += comp;
            totalDecompressionTime += decomp;
        }
        Console.Write($"AVERAGE {totalCompressionTime.TotalMilliseconds / x} ms compression time... ");
        Console.Write($"AVERAGE {totalDecompressionTime.TotalMilliseconds / x} ms decompression time... ");
        Assert.Pass();
    }
    
    private (TimeSpan,TimeSpan) RandomLongTest() {
        int len = 9000000;
        Random random = new Random();
        DateTime end;
        DateTime start;
        
        byte[] bigBufferI = new byte[len];
        byte[] bigBufferO = new byte[len];
        
        // random input bytes
        random.NextBytes(bigBufferI);
        
        using MemoryStream input = new MemoryStream(bigBufferI);
        using MemoryStream output = new MemoryStream();
        using MemoryStream output2 = new MemoryStream(bigBufferO);
        
        TimeSpan compressionTime = TimeSpan.Zero;
        TimeSpan decompressionTime = TimeSpan.Zero;

        try {

            start = DateTime.Now;
            using BZip2ParallelOutputStream compressor = new BZip2ParallelOutputStream(output, 12, false, 9);
            input.CopyTo(compressor, 8640000);
            compressor.Close();
            end = DateTime.Now;
            compressionTime = end - start;
            Console.Write($"{compressionTime.TotalMilliseconds} ms compression time... ");

            start = DateTime.Now;
            output.Position = 0;
            using BZip2InputStream decompressor = new BZip2InputStream(output, false);
            decompressor.CopyTo(output2);
            end = DateTime.Now;
            decompressionTime = end - start;
            Console.WriteLine($"{decompressionTime.TotalMilliseconds} ms decompression time");
        } catch (Exception) {
            string randomFile = Path.GetRandomFileName();
            using FileStream fs = new FileStream(randomFile, FileMode.Create, FileAccess.Write);
            input.Position = 0;
            input.CopyTo(fs);
            fs.Flush();
            fs.Close();
            Assert.Fail();
        }
        
        for (int i = 0; i < bigBufferI.Length; i++) {
            if (bigBufferI[i] != bigBufferO[i]) {
                string randomFile = Path.GetRandomFileName();
                using FileStream fs = new FileStream(randomFile, FileMode.Create, FileAccess.Write);
                input.Position = 0;
                input.CopyTo(fs);
                fs.Flush();
                fs.Close();
                Assert.Fail($"bytes differ at position {i}");
            }
        }

        return (compressionTime, decompressionTime);
    }
    
    public (TimeSpan,TimeSpan) RandomLongTestWithRepeatedValues() {
        int len = 9000000;
        int repeatStreaks = 64;
        Random random = new Random();
        DateTime end;
        DateTime start;
        
        byte[] bigBufferI = new byte[len];
        byte[] bigBufferO = new byte[len];
        
        // random input bytes
        random.NextBytes(bigBufferI);

        int offset = 0;
        for (int rs = 0; rs < repeatStreaks; rs++) {
            int newoffset = random.Next(0, (len - 10000) / repeatStreaks);
            offset += newoffset;
            int count = random.Next(0, 512);
            byte val = bigBufferI[offset++];
            for (int i = 0; i < count; i++) {
                bigBufferI[offset++] = val;
            }
        }
        
        using MemoryStream input = new MemoryStream(bigBufferI);
        using MemoryStream output = new MemoryStream();
        using MemoryStream output2 = new MemoryStream(bigBufferO);

        TimeSpan compressionTime = TimeSpan.Zero;
        TimeSpan decompressionTime = TimeSpan.Zero;

        try {

            start = DateTime.Now;
            using BZip2ParallelOutputStream compressor = new BZip2ParallelOutputStream(output, 12, false, 9);
            input.CopyTo(compressor, 8640000);
            compressor.Close();
            end = DateTime.Now;
            compressionTime = end - start;
            Console.Write($"{compressionTime.TotalMilliseconds} ms compression time... ");

            start = DateTime.Now;
            output.Position = 0;
            using BZip2InputStream decompressor = new BZip2InputStream(output, false);
            decompressor.CopyTo(output2);
            end = DateTime.Now;
            decompressionTime = end - start;
            Console.WriteLine($"{decompressionTime.TotalMilliseconds} ms decompression time");
        } catch (Exception) {
            string randomFile = Path.GetRandomFileName();
            using FileStream fs = new FileStream(randomFile, FileMode.Create, FileAccess.Write);
            input.Position = 0;
            input.CopyTo(fs);
            fs.Flush();
            fs.Close();
            Assert.Fail();
        }

        for (int i = 0; i < bigBufferI.Length; i++) {
            if (bigBufferI[i] != bigBufferO[i]) {
                string randomFile = Path.GetRandomFileName();
                using FileStream fs = new FileStream(randomFile, FileMode.Create, FileAccess.Write);
                input.Position = 0;
                input.CopyTo(fs);
                fs.Flush();
                fs.Close();
                Assert.Fail($"bytes differ at position {i}");
            }
        }
        
        return (compressionTime, decompressionTime);
    }
}
