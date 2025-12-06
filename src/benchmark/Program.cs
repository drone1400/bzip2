// Added by drone1400, July 2022
// Location: https://github.com/drone1400/bzip2

using BenchmarkDotNet.Running;
using Bzip2.benchmark;
var summary = BenchmarkRunner.Run<DmodBenchmark>();

