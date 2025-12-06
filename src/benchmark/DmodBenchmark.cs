// Added by drone1400, July 2022
// Location: https://github.com/drone1400/bzip2

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Bzip2.InputStream;
using Bzip2.OutputStream;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;

namespace Bzip2.benchmark
{

    // NOTE: to use the RPlotExporter you need the R binaries
    // You can get them here: https://cran.r-project.org/

    [SimpleJob(RuntimeMoniker.Net472)]
    [SimpleJob(RuntimeMoniker.Net48, baseline: true)]
    [SimpleJob(RuntimeMoniker.Net80)]
    [SimpleJob(RuntimeMoniker.Net10_0)]
    [RPlotExporter]
    public class DmodBenchmark
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

        [GlobalSetup]
        public void Setup()
        {

        }

        [Benchmark]
        public void CMT()
        {
            using FileStream fsIn = new FileStream(this._testFilePathTar, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream fsOut = new FileStream(this._testFileOutCompressed, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using Stream streamOut = new BZip2ParallelOutputStream(fsOut, true);
            fsIn.CopyTo(streamOut, this.CopyBuffSize);
            streamOut.Close();
            fsIn.Close();
        }

        [Benchmark]
        public void CST()
        {
            using FileStream fsIn = new FileStream(this._testFilePathTar, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream fsOut = new FileStream(this._testFileOutCompressed, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using Stream streamOut = new BZip2OutputStream(fsOut, true);
            fsIn.CopyTo(streamOut, this.CopyBuffSize);
            streamOut.Close();
            fsIn.Close();
        }
        [Benchmark]
        public void CSC()
        {
            using FileStream fsIn = new FileStream(this._testFilePathTar, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream fsOut = new FileStream(this._testFileOutCompressed, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using Stream streamOut = new BZip2Stream(fsOut, CompressionMode.Compress, false);
            fsIn.CopyTo(streamOut, this.CopyBuffSize);
            streamOut.Close();
            fsIn.Close();
        }

        [Benchmark]
        public void DMT()
        {
            using FileStream fsIn = new FileStream(this._testFilePathBz2, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream fsOut = new FileStream(this._testFileOutDecompressed, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using Stream streamIn = new BZip2ParallelInputStream(fsIn, true);
            streamIn.CopyTo(fsOut, this.CopyBuffSize);
            fsOut.Close();
            streamIn.Close();
        }

        [Benchmark]
        public void DST()
        {
            using FileStream fsIn = new FileStream(this._testFilePathBz2, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream fsOut = new FileStream(this._testFileOutDecompressed, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using Stream streamIn = new BZip2InputStream(fsIn, true);
            streamIn.CopyTo(fsOut, this.CopyBuffSize);
            fsOut.Close();
            streamIn.Close();
        }


        [Benchmark]
        public void DSC()
        {
            using FileStream fsIn = new FileStream(this._testFilePathBz2, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream fsOut = new FileStream(this._testFileOutDecompressed, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using Stream streamIn = new BZip2Stream(fsIn, CompressionMode.Decompress, false);
            streamIn.CopyTo(fsOut, this.CopyBuffSize);
            fsOut.Close();
            streamIn.Close();
        }
    }
}
