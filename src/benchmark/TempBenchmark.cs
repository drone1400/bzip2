// Added by drone1400, July 2022
// Location: https://github.com/drone1400/bzip2

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Bzip2.InputStream;
using Bzip2.Interface;
using Bzip2.OutputStream;
using Sewer56.BitStream;
using Sewer56.BitStream.ByteStreams;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;

namespace Bzip2.benchmark
{

    // NOTE: to use the RPlotExporter you need the R binaries
    // You can get them here: https://cran.r-project.org/

    [SimpleJob(RuntimeMoniker.Net10_0)]
    [MaxIterationCount(4)]
    [MinIterationCount(1)]
    [InvocationCount(2)]
    public class TempBenchmark
    {
        // get the file here: https://www.dinknetwork.com/file/necromancer/
        //private string _testFilePathTar = @"E:\TEMP\BZIP2_TEST\benchmark\necromancer-demo_v1_02.tar";
        //private string _testFilePathBz2 = @"E:\TEMP\BZIP2_TEST\benchmark\necromancer-demo_v1_02.dmod";

        // get the file here: https://www.dinknetwork.com/file/friends_beyond_3_legend_of_tenjin/
        private string _testFilePathTar = @"E:\TEMP\BZIP2_TEST\benchmark\friends_beyond_3_legend_of_tenjin-v2_01.tar";
        private string _testFilePathBz2 = @"E:\TEMP\BZIP2_TEST\benchmark\friends_beyond_3_legend_of_tenjin-v2_01.dmod";

        private string _testFileOutDecompressed = "necromancer_decompressed.tar";
        private string _testFileOutCompressed = "necromancer_compressed.bz2";

        //[Params(1048576, 41943040)]
        public int CopyBuffSize = 1048576;


        private byte[] data = new byte[1048576];

        [GlobalSetup]
        public void Setup()
        {
            Random r = new Random();
            r.NextBytes(data);
        }

        [Benchmark]
        public void BitstreamReadWrite()
        {
            byte[] outBuffer = new byte[1048576];
            var bitStreamR = new BitStream<ArrayByteStream>(new ArrayByteStream(data), 0);
            var bitStreamW = new  BitStream<ArrayByteStream>(new ArrayByteStream(outBuffer), 0);

            for (int i = 0; i < 8 * this.data.Length; i++)
            {
                bool bitVal = bitStreamR.Read<bool>();
                bitStreamW.Write(bitVal);
            }
        }

        [Benchmark]
        public void CustomImpReadWrite()
        {
            byte[] outBuffer = new byte[1048576];
            IBZip2BitInputStream bitStreamR = new BZip2BitInputStream(new MemoryStream(data));
            IBZip2BitOutputStream bitStreamW = new BZip2BitOutputStream(new MemoryStream(data));

            for (int i = 0; i < 8 * this.data.Length; i++)
            {
                bool bitVal = bitStreamR.ReadBoolean();
                bitStreamW.WriteBoolean(bitVal);
            }
        }

        // [Benchmark]
        // public void DMT()
        // {
        //     using FileStream fsIn = new FileStream(this._testFilePathBz2, FileMode.Open, FileAccess.Read, FileShare.Read);
        //     using FileStream fsOut = new FileStream(this._testFileOutDecompressed, FileMode.Create, FileAccess.Write, FileShare.None);
        //     using Stream streamIn = new BZip2ParallelInputStream(fsIn, true);
        //     streamIn.CopyTo(fsOut, this.CopyBuffSize);
        //     fsOut.Close();
        //     streamIn.Close();
        // }


    }
}
