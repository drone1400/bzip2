using System;
using Xunit;
using Xunit.Abstractions;
namespace Bzip2.test
{
    public class TestsRandomDataX1000
    {
        private readonly ITestOutputHelper _console;
        private readonly int _repeatCount = 1000;

        public TestsRandomDataX1000(ITestOutputHelper console)
        {
            this._console = console;
        }
        
        [Fact]
        public void RandomSingleByteLongTest_CST_DST() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.SingleByteValue, TestCommon.TestMode.CST_DST, 100000000);
        [Fact]
        public void RandomLongTest_CST_DST() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.RandomBytes, TestCommon.TestMode.CST_DST);
        [Fact]
        public void RandomLongTestWithRepeatedValues_CST_DST() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.RandomBytesRepeat, TestCommon.TestMode.CST_DST);
        [Fact]
        public void RandomSingleByteLongTest_CMT_DST() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.SingleByteValue, TestCommon.TestMode.CMT_DST, 100000000);
        [Fact]
        public void RandomLongTest_CMT_DST() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.RandomBytes, TestCommon.TestMode.CMT_DST);
        [Fact]
        public void RandomLongTestWithRepeatedValues_CMT_DST() => 
            TestCommon.RandomLongTest_X(this._console, this._repeatCount, TestCommon.RandomDataMode.RandomBytesRepeat, TestCommon.TestMode.CMT_DST);
    }
}
