using System;
using Xunit;
using Xunit.Abstractions;
namespace Bzip2.test
{
    public class TestsRandomDataX100 : TestsRandomData
    {
        public TestsRandomDataX100(ITestOutputHelper console) :  base(console)
        {
            this._repeatCount = 100;
        }
    }
}
