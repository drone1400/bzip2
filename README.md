# bzip2.net
A pure C# implementation of the bzip2 compressor

Originally ported by Jaime Olivares: https://github.com/jaime-olivares/bzip2

Based on the Java implementation by Matthew Francis: https://github.com/MateuszBartosiewicz/bzip2

Modified for .NET6 and also added a multithreadded compressor.

# Notes

The compression algorithm doesn't generate randomized blocks, which is already a deprecated option and may not be decoded by modern bzip2 libraries. Other popular .net compression libraries do generate randomized blocks.

Nuget package is currently not available for this modification nor do I have any plans to publish it there.

_DISCLAIMER (Jaime Olivares)_: Unfortunately, this library has a well-known bug coming from the original implementation, as reported [here](https://github.com/MateuszBartosiewicz/bzip2/issues/1)

_DISCLAIMER NOTE_:I have not yet encountered this well-known bug mentioned above...
