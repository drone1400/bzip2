using System;
using Xunit;
using Xunit.Abstractions;
namespace Bzip2.test
{
    public class TestsRandomDataX1000 : TestsRandomData
    {
        public TestsRandomDataX1000(ITestOutputHelper console) :  base(console)
        {
            this._repeatCount = 1000;
        }
    }
}
