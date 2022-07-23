// Bzip2 library for .net
// Modified by drone1400
// Location: https://github.com/drone1400/bzip2
// Ported from the Java implementation by Matthew Francis: https://github.com/MateuszBartosiewicz/bzip2
// Modified from the .net implementation by Jaime Olivares: http://github.com/jaime-olivares/bzip2

using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Bzip2.test {

    /// <summary>
    /// Unit test for the BZip2 compression library
    /// </summary>
    public class Tests {

        private const int BufferSizeLarge = 10000000; // Almost 10 Mb
        private const int BufferSizeSmall = 100000;   // Around 100 Kb
        private static byte[] Buffer = new byte[BufferSizeLarge];

        private readonly ITestOutputHelper _console;
        private readonly Random _random;

        /// <summary>
        /// Fills the test buffer with random values
        /// </summary>
        public Tests(ITestOutputHelper console)
        {
            this._random = new Random();
            this._console = console;

            this._random.NextBytes(Buffer);
        }

        /// <summary>
        /// Performs a CRC check and compare against well-known results
        /// The buffer has different values
        /// </summary>
        [Fact]
        public void CrcAlgorithmDifferentValues() {
            byte[] buffer = {
                0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0xFA
            };

            var crc = new CRC32();
            for (int i = 0; i < buffer.Length; i++)
                crc.UpdateCrc(buffer[i]);

            Assert.Equal(crc.CRC, 0x8AEE127A);
        }

        /// <summary>
        /// Performs a CRC check and compare against well-known results
        /// The buffer has different values
        /// </summary>
        [Fact]
        public void CrcAlgorithmSameValues() {
            var crc = new CRC32();
            crc.UpdateCrc(0x55, 10);
            Assert.Equal(crc.CRC, 0xA1E07747);
        }

        /// <summary>
        /// Compresses the full buffer and checks for a reasonable compressed size
        /// </summary>
        [Fact]
        public void CompressSmokeLarge() {
            var input = new MemoryStream(Buffer);
            var output = new MemoryStream();

            var compressor = new BZip2OutputStream(output, false);
            input.CopyTo(compressor);
            compressor.Close();

            // Estimated size between inputSize*0.5 and inputSize*1.1
            Assert.True(output.Length > BufferSizeLarge * 0.5);
            Assert.True(output.Length < BufferSizeLarge * 1.1);
        }

        /// <summary>
        /// Compresses a portion of the buffer and checks for a reasonable compressed size
        /// </summary>
        [Fact]
        public void CompressSmokeSmall() {
            var input = new MemoryStream(Buffer, 0, BufferSizeSmall);
            var output = new MemoryStream();

            var compressor = new BZip2OutputStream(output, false);
            input.CopyTo(compressor);
            compressor.Close();

            // Estimated size between inputSize*0.5 and inputSize*1.1
            Assert.True(output.Length > BufferSizeSmall * 0.5);
            Assert.True(output.Length < BufferSizeSmall * 1.1);
        }

        /// <summary>
        /// Compresses and decompresses a long random buffer
        /// </summary>
        [Fact]
        public void CompressAndDecompress() {
            var input = new MemoryStream(Buffer);
            var output = new MemoryStream();

            var compressor = new BZip2OutputStream(output, false);
            input.CopyTo(compressor);
            compressor.Close();

            Assert.True(output.Length > 4);

            output.Position = 0;
            var output2 = new MemoryStream();
            var decompressor = new BZip2InputStream(output, false);
            decompressor.CopyTo(output2);

            Assert.Equal(Buffer.Length, output2.Length);
            output2.Position = 0;
            for (int i = 0; i < Buffer.Length; i++) {
                if (Buffer[i] != (byte)output2.ReadByte()) {
                    Assert.True(false, $"bytes differ at position {i}");
                }
            }
        }
    }
}