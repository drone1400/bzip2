using Xunit.Abstractions;
namespace Bzip2.test
{
    public class TestsRandomDataX10 : TestsRandomData
    {
        public TestsRandomDataX10(ITestOutputHelper console) :  base(console)
        {
            this._repeatCount = 10;
        }
    }
}
