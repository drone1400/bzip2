using NUnit.Framework;
using System;
using System.IO;

namespace Bzip2.test; 

public class CustomFileTests {
    [Test]
    public void TestExampleFileX3() {
        string pathIn = @"E:\TEMP\s0yrzp0v\example";
        string pathOut1 = @"E:\TEMP\s0yrzp0v\Example1A.bzip2";
        string pathOut2 = @"E:\TEMP\s0yrzp0v\Example1B.bzip2";

        int threads = Environment.ProcessorCount;

        DateTime start;
        DateTime finish;
            
        start = DateTime.Now;
        using (FileStream fsi = new FileStream(pathIn, FileMode.Open, FileAccess.Read))
        using (FileStream fso = new FileStream(pathOut1, FileMode.Create, FileAccess.Write))
        {
            BZip2OutputStream compressor = new BZip2OutputStream(fso, false, 9);
            fsi.CopyTo(compressor);
            compressor.Close();
        }
        finish = DateTime.Now;
        Console.WriteLine("  BZip2OutputStream " + (finish-start).TotalMilliseconds);
        
        start = DateTime.Now;
        using (FileStream fsi = new FileStream(pathIn, FileMode.Open, FileAccess.Read))
        using (FileStream fso = new FileStream(pathOut2, FileMode.Create, FileAccess.Write))
        using (BZip2ParallelOutputStream bzip2 = new BZip2ParallelOutputStream(fso, threads, true, 9))
        {
            fsi.CopyTo(bzip2);
            bzip2.Close();
        }
        finish = DateTime.Now;
        Console.WriteLine($" BZip2ParallelOutputStream - {threads} threads " + (finish-start).TotalMilliseconds);
       
        
        using (FileStream fstest1 = new FileStream(pathOut1, FileMode.Open, FileAccess.Read))
        using (FileStream fstest2 = new FileStream(pathOut2, FileMode.Open, FileAccess.Read)) {
            if (fstest1.Length != fstest2.Length) {
                Assert.Fail("Output streams length mismatch...");
            }

            for (long i = 0; i < fstest1.Length; i++) {
                int b1 = fstest1.ReadByte();
                if (b1 != fstest2.ReadByte()) {
                    Assert.Fail($"Output stream difference between Stream 1 and 2 at byte index {i}");
                }
            }
        }
        
        Assert.Pass("Files were compressed and both results match!...");
    }
    
    
    [Test]
    public void BunchOfTestFiles() {
        foreach (string file in Directory.GetFiles("testfiles\\")) {
            FileTest(file);
        }
    }
    
     private (TimeSpan,TimeSpan) FileTest(string inputPath) {
        DateTime end;
        DateTime start;
        
        using FileStream input = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
        using MemoryStream output = new MemoryStream();
        byte[] bigBufferO = new byte[input.Length];
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

        input.Position = 0;
        
        for (int i = 0; i < input.Length; i++) {
            if ((byte)input.ReadByte() != bigBufferO[i]) {
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
