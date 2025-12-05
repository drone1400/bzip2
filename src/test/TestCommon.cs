using System;
using System.Diagnostics;
using System.IO;
using Xunit;
using Xunit.Abstractions;
namespace Bzip2.test
{
    public static class TestCommon
    {
        
        /// <summary>
        /// Common test routine for Multi Threaded compression + Single Threaded decompression
        /// </summary>
        /// <param name="console"><see cref="ITestOutputHelper"/></param>
        /// <param name="inputStream">Input stream, must be seekable</param>
        /// <param name="threads">Maximum number of threads to use for compression, if 0 will use Environment.ProcessorCount</param>
        /// <param name="outputBufferSize">Size for temporary output memory buffer</param>
        /// <param name="copyBufferSize">Size for temporary copy buffer</param>
        /// <param name="saveFileOnFail">If true, will save inputStream data to a file</param>
        /// <returns>(Time Compression MultiThread, Time Decompression SingleThread)</returns>
        public static (double, double) TestCommon_CMT_DST(ITestOutputHelper console, Stream inputStream, int threads = 0, int outputBufferSize = 8388608, int copyBufferSize = 8388608, bool saveFileOnFail = false)
        {
            void DebugSaveInputStream()
            {
                string randomFile = Path.GetRandomFileName();
                console.WriteLine($"    Saving input data to {randomFile}");
                using FileStream fs = new FileStream(randomFile, FileMode.Create, FileAccess.Write);
                inputStream.Position = 0;
                inputStream.CopyTo(fs);
                fs.Flush();
                fs.Close();
            }
            
            double timeCMT = 0;
            double timeDST = 0;

            if (threads == 0)
            {
                threads = Environment.ProcessorCount;
            }
            
            using MemoryStream output = new MemoryStream(outputBufferSize);
            using MemoryStream outputDecompressed = new MemoryStream(outputBufferSize);
            
            try
            {
                // compress input
                Stopwatch swCMT = new Stopwatch();
                swCMT.Start();
                using BZip2ParallelOutputStream compressor = new BZip2ParallelOutputStream(output, false, 9);
                inputStream.CopyTo(compressor, copyBufferSize);
                compressor.Close();
                swCMT.Stop();
                timeCMT = swCMT.ElapsedMilliseconds;
                console.WriteLine($"    {timeCMT} ms MT compression time... ");

                // reset output position
                output.Position = 0;
                
                // decompress output
                Stopwatch swDST = new Stopwatch();
                swDST.Start();
                using BZip2InputStream decompressor = new BZip2InputStream(output, false);
                decompressor.CopyTo(outputDecompressed, copyBufferSize);
                swDST.Stop();
                timeDST = swDST.ElapsedMilliseconds;
                console.WriteLine($"    {timeDST} ms ST decompression time");
            } catch (Exception ex)
            {
                if (saveFileOnFail)
                {
                    DebugSaveInputStream();
                }
                
                Assert.Fail($"Exception was thrown... {ex}");
            }

            if (inputStream.Length != outputDecompressed.Length)
            {
                Assert.Fail($"Decompressed stream length mismatch, expecting {inputStream.Length}, got {outputDecompressed.Length}");
            }
            
            inputStream.Position = 0;
            outputDecompressed.Position = 0;
            
            for (int i = 0; i < inputStream.Length; i++)
            {
                int expect = inputStream.ReadByte();
                int value = outputDecompressed.ReadByte();
                if ( expect != value)
                {
                    if (saveFileOnFail)
                    {
                        DebugSaveInputStream();
                        Assert.Fail($"bytes differ at position {i}, expected {expect}, got {value}");
                    }
                }
            }

            return (timeCMT, timeDST);
        }
        
        /// <summary>
        /// Common test routine for Single Threaded compression + Single Threaded decompression
        /// </summary>
        /// <param name="console"><see cref="ITestOutputHelper"/></param>
        /// <param name="inputStream">Input stream, must be seekable</param>
        /// <param name="threads">Maximum number of threads to use for compression, if 0 will use Environment.ProcessorCount</param>
        /// <param name="outputBufferSize">Size for temporary output memory buffer</param>
        /// <param name="copyBufferSize">Size for temporary copy buffer</param>
        /// <param name="saveFileOnFail">If true, will save inputStream data to a file</param>
        /// <returns>(Time Compression Single Thread, Time Decompression Single Thread)</returns>
        public static (double, double) TestCommon_CST_DST(ITestOutputHelper console, Stream inputStream, int threads = 0, int outputBufferSize = 8388608, int copyBufferSize = 8388608, bool saveFileOnFail = false)
        {
            void DebugSaveInputStream()
            {
                string randomFile = Path.GetRandomFileName();
                console.WriteLine($"    Saving input data to {randomFile}");
                using FileStream fs = new FileStream(randomFile, FileMode.Create, FileAccess.Write);
                inputStream.Position = 0;
                inputStream.CopyTo(fs);
                fs.Flush();
                fs.Close();
            }
            
            double timeCST = 0;
            double timeDST = 0;

            if (threads == 0)
            {
                threads = Environment.ProcessorCount;
            }
            
            using MemoryStream output = new MemoryStream(outputBufferSize);
            using MemoryStream outputDecompressed = new MemoryStream(outputBufferSize);
            
            try
            {
                // compress input
                Stopwatch swCST = new Stopwatch();
                swCST.Start();
                using BZip2OutputStream compressor = new BZip2OutputStream(output, false, 9);
                inputStream.CopyTo(compressor, copyBufferSize);
                compressor.Close();
                swCST.Stop();
                timeCST = swCST.ElapsedMilliseconds;
                console.WriteLine($"    {timeCST} ms MT compression time... ");
                
                
                // reset output position
                output.Position = 0;
                
                // decompress output
                Stopwatch swDST = new Stopwatch();
                swDST.Start();
                using BZip2InputStream decompressor = new BZip2InputStream(output, false);
                decompressor.CopyTo(outputDecompressed, copyBufferSize);
                swDST.Stop();
                timeDST = swDST.ElapsedMilliseconds;
                console.WriteLine($"    {timeDST} ms ST decompression time");
            } catch (Exception ex)
            {
                if (saveFileOnFail)
                {
                    DebugSaveInputStream();
                }
                
                Assert.Fail($"Exception was thrown... {ex}");
            }

            if (inputStream.Length != outputDecompressed.Length)
            {
                Assert.Fail($"Decompressed stream length mismatch, expecting {inputStream.Length}, got {outputDecompressed.Length}");
            }
            
            inputStream.Position = 0;
            outputDecompressed.Position = 0;
            
            for (int i = 0; i < inputStream.Length; i++)
            {
                int expect = inputStream.ReadByte();
                int value = outputDecompressed.ReadByte();
                if ( expect != value)
                {
                    if (saveFileOnFail)
                    {
                        DebugSaveInputStream();
                        Assert.Fail($"bytes differ at position {i}, expected {expect}, got {value}");
                    }
                }
            }

            return (timeCST, timeDST);
        }

        public enum RandomDataMode
        {
            SingleByteValue,
            RandomBytes,
            RandomBytesRepeat,
        }

        public enum TestMode
        {
            CMT_DST, // multi thread compress, single thread decompress
            CST_DST, // single thread compress, single thread decompress
            CMT_DMT, // multi thread compress, multi thread decompress (TODO)
            CST_DMT, // single thread compress, multi thread decompress (TODO)
        }
        
        public static void RandomLongTest_X(ITestOutputHelper console, int repeat, RandomDataMode dataMode, TestMode testMode, int len = 9000000)
        {
            Random random = new Random();

            int copyBufferSize = 8388608;
            int outBufferSize = 8388608;
            int repeatStreaks = 64;

            double totalCompressionTimeMs = 0;
            double totalDecompressionTimeMs = 0;
            
            for (int r = 0; r < repeat; r++)
            {
                byte[] bigBuffer = new byte[len];
                switch (dataMode)
                {
                    case RandomDataMode.RandomBytes:
                    {
                        random.NextBytes(bigBuffer);
                        break;
                    }
                    case RandomDataMode.RandomBytesRepeat:
                    {
                        random.NextBytes(bigBuffer);

                        int offset = 0;
                        for (int rs = 0; rs < repeatStreaks; rs++)
                        {
                            int newoffset = random.Next(0, (len - 10000) / repeatStreaks);
                            offset += newoffset;
                            int count = random.Next(0, 512);
                            byte val = bigBuffer[offset++];
                            for (int i = 0; i < count; i++)
                            {
                                bigBuffer[offset++] = val;
                            }
                        }
                        break;
                    }
                    case RandomDataMode.SingleByteValue:
                    {
                        byte value = (byte)(random.Next() & 0xFF);
                        for (int i = 0; i < len; i++)
                        {
                            bigBuffer[i] = value;
                        }
                        break;
                    }
                }
                
                MemoryStream ms = new MemoryStream(bigBuffer);
                double timeC = 0, timeD = 0;
                switch (testMode)
                {
                    default: throw new Exception("Unknown test mode");
                    case TestMode.CMT_DMT:
                    {
                        throw new NotImplementedException();
                    }
                    case TestMode.CST_DMT:
                    {
                        throw new NotImplementedException();
                    }
                    case TestMode.CST_DST:
                    {
                        (timeC, timeD) = TestCommon_CST_DST(console, ms, Environment.ProcessorCount, outBufferSize, copyBufferSize, true);
                        break;
                    }
                    case TestMode.CMT_DST:
                    {
                        (timeC, timeD) = TestCommon_CMT_DST(console, ms, Environment.ProcessorCount, outBufferSize, copyBufferSize, true);
                        break;
                    }
                }

                totalCompressionTimeMs += timeC;
                totalDecompressionTimeMs += timeD;
            }
            console.WriteLine($"DONE {testMode}");
            console.WriteLine($"AVERAGE {totalCompressionTimeMs / repeat} ms compression time... ");
            console.WriteLine($"AVERAGE {totalDecompressionTimeMs / repeat} ms decompression time... ");
        }
    }
}
