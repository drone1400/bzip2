using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;
namespace Bzip2.test
{
    public class TestsLocalFiles
    {
        private readonly ITestOutputHelper _console;
        private string _testFolderRoot = @"E:\TEMP\BZIP2_TEST\";
        
        public TestsLocalFiles(ITestOutputHelper console)
        {
            this._console = console;
        }
        
        [Fact]
        public void TestLocalFiles_CMT_DST()
        {
            int copyBufferSize = 8388608;
            int outBufferSize = 8388608;
            
            string directory = Path.Combine(this._testFolderRoot, "testfiles");
            foreach (string file in Directory.GetFiles(directory))
            {
                using FileStream fs = File.OpenRead(file);
                this._console.WriteLine(file);
                TestCommon.TestCommon_CMT_DST(_console, fs, Environment.ProcessorCount, outBufferSize, copyBufferSize, false);
                fs.Close();
            }
        }
        
        [Fact]
        public void TestLocalFiles_CST_DST()
        {
            int copyBufferSize = 8388608;
            int outBufferSize = 8388608;
            
            string directory = Path.Combine(this._testFolderRoot, "testfiles");
            foreach (string file in Directory.GetFiles(directory))
            {
                using FileStream fs = File.OpenRead(file);
                this._console.WriteLine(file);
                TestCommon.TestCommon_CST_DST(_console, fs, Environment.ProcessorCount, outBufferSize, copyBufferSize, false);
                fs.Close();
            }
        }
    }
}
