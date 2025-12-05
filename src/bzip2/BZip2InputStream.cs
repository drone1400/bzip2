// Bzip2 library for .net
// Modified by drone1400
// Location: https://github.com/drone1400/bzip2
// Ported from the Java implementation by Matthew Francis: https://github.com/MateuszBartosiewicz/bzip2
// Modified from the .net implementation by Jaime Olivares: http://github.com/jaime-olivares/bzip2

using System;
using System.IO;

namespace Bzip2
{
    /// <summary>An InputStream wrapper that decompresses BZip2 data</summary>
    /// <remarks>Instances of this class are not threadsafe</remarks>
    public class BZip2InputStream : Stream
    {
        // The stream from which compressed BZip2 data is read and decoded
        private Stream inputStream;

        // An InputStream wrapper that provides bit-level reads
        private BZip2BitInputStream bitInputStream;

        // If true, the caller is assumed to have read away the stream's leading "BZ" identifier bytes
        private readonly bool headerless;

        // (@code true} if the end of the compressed stream has been reached, otherwise false
        private bool streamComplete;

        /// <summary>
        /// The declared block size of the stream (before final run-length decoding). The final block
        /// will usually be smaller, but no block in the stream has to be exactly this large, and an
        /// encoder could in theory choose to mix blocks of any size up to this value. Its function is
        /// therefore as a hint to the decompressor as to how much working space is sufficient to
        /// decompress blocks in a given stream
        /// </summary>
        private uint streamBlockSize;

        // The merged CRC of all blocks decompressed so far
        private uint streamCRC;

        // The decompressor for the current block
        private BZip2BlockDecompressor blockDecompressor;
        
        /// <summary>Public constructor</summary>
        /// <param name="inputStream">The InputStream to wrap</param>
        /// <param name="headerless">If true, the caller is assumed to have read away the stream's 
        /// leading "BZ" identifier bytes</param>
        public BZip2InputStream(Stream inputStream, bool headerless)
        {

            if (inputStream == null)
            {
                throw new ArgumentException("Null input stream");
            }

            this.inputStream = inputStream;
            this.bitInputStream = new BZip2BitInputStream(inputStream);
            this.headerless = headerless;

            // initialize stream immediately
            this.InitializeStream();
            // prepare first block
            this.InitializeNextBlock();
        }

        #region Implementation of abstract members of Stream

        #pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override void Flush()
        {
            throw new NotSupportedException($"{nameof(BZip2InputStream)} does not support 'Flush()' method.");
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException($"{nameof(BZip2InputStream)} does not support 'Seek(long offset, SeekOrigin origin)' method.");
        }
        public override void SetLength(long value)
        {
            throw new NotSupportedException($"{nameof(BZip2InputStream)} does not support 'SetLength(long value)' method.");
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException($"{nameof(BZip2InputStream)} does not support 'Write(byte[] buffer, int offset, int count)' method.");
        }
        public override bool CanRead => this.inputStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => this.inputStream.Length;
        public override long Position
        {
            get => this.inputStream.Position;
            set =>throw new NotSupportedException($"{nameof(BZip2InputStream)} does not support Set operation for property 'Position'.");
        }

        public override int ReadByte() 
        {
            var nextByte = this.blockDecompressor.Read();
            
            // if current block has reached its end, prepare next block and try reading again
            if (nextByte == -1) 
            {
                if (this.InitializeNextBlock()) 
                {
                    nextByte = this.blockDecompressor.Read();
                }
            }

            return nextByte;
        }

        public override int Read(byte[] destination,  int offset,  int length) 
        {
            int bytesRead = this.blockDecompressor.Read(destination, offset, length);

            // if current block has reached its end, prepare next block and try reading again
            if (bytesRead == -1) 
            {
                if (this.InitializeNextBlock()) 
                {
                    bytesRead = this.blockDecompressor.Read(destination, offset, length);
                }
            }

            if (bytesRead == -1) bytesRead = 0;

            return bytesRead;
        }

        // overriding Dispose instead of Close as recommended in https://docs.microsoft.com/en-us/dotnet/api/system.io.stream.close?view=net-6.0
        protected override void Dispose(bool disposing)
        {
            if (this.bitInputStream == null)
                return;

            this.streamComplete = true;
            this.blockDecompressor = null;
            this.bitInputStream = null;

            try
            {
                this.inputStream.Close();
            } finally
            {
                this.inputStream = null;
            }
        }

        #pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        #endregion

        /// <summary>Reads the stream header and checks that the data appears to be a valid BZip2 stream</summary>
        /// <exception cref="IOException">if the stream header is not valid</exception>
        private void InitializeStream() 
        {
            /* If the stream has been explicitly closed, throw an exception */
            if (this.bitInputStream == null)
                throw new IOException("Stream closed");

            // If we're already at the end of the stream, do nothing
            if (this.streamComplete)
                return;

            // Read the stream header
            try
            {
                uint marker1 = this.headerless ? 0 : this.bitInputStream.ReadBits(16);
                uint marker2 = this.bitInputStream.ReadBits (8);
                uint blockSize = (this.bitInputStream.ReadBits(8) - '0');

                if ((!this.headerless && (marker1 != BZip2Constants.STREAM_START_MARKER_1))
                    || (marker2 != BZip2Constants.STREAM_START_MARKER_2)
                    || (blockSize < 1) || (blockSize > 9))
                {
                    throw new IOException("Invalid BZip2 header");
                }

                this.streamBlockSize = blockSize * 100000;
            } catch (IOException)
            {
                // If the stream header was not valid, stop trying to read more data
                this.streamComplete = true;
                throw;
            }
        }

        /// <summary>Prepares a new block for decompression if any remain in the stream</summary>
        /// <remarks>If a previous block has completed, its CRC is checked and merged into the stream CRC.
        /// If the previous block was the final block in the stream, the stream CRC is validated</remarks>
        /// <return>true if a block was successfully initialised, or false if the end of file marker was encountered</return>
        /// <exception cref="IOException">If either the block or stream CRC check failed, if the following data is
        /// not a valid block-header or end-of-file marker, or if the following block could not be decoded</exception>
        private bool InitializeNextBlock()
        {

            // If we're already at the end of the stream, do nothing
            if (this.streamComplete)
                return false;

            // If a block is complete, check the block CRC and integrate it into the stream CRC
            if (this.blockDecompressor != null)
            {
                uint blockCRC = this.blockDecompressor.CheckCrc();
                this.streamCRC = ((this.streamCRC << 1) | (this.streamCRC >> 31)) ^ blockCRC;
            }

            // Read block-header or end-of-stream marker
            uint marker1 = this.bitInputStream.ReadBits(24);
            uint marker2 = this.bitInputStream.ReadBits(24);

            if (marker1 == BZip2Constants.BLOCK_HEADER_MARKER_1 && marker2 == BZip2Constants.BLOCK_HEADER_MARKER_2)
            {
                // Initialise a new block
                try
                {
                    this.blockDecompressor = new BZip2BlockDecompressor(this.bitInputStream, this.streamBlockSize);
                } catch (IOException)
                {
                    // If the block could not be decoded, stop trying to read more data
                    this.streamComplete = true;
                    throw;
                }
                return true;
            }
            if (marker1 == BZip2Constants.STREAM_END_MARKER_1 && marker2 == BZip2Constants.STREAM_END_MARKER_2)
            {
                // Read and verify the end-of-stream CRC
                this.streamComplete = true;
                uint storedCombinedCRC = this.bitInputStream.ReadInteger(); // .ReadBits(32);

                if (storedCombinedCRC != this.streamCRC)
                    throw new IOException("BZip2 stream CRC error");

                return false;
            }

            // If what was read is not a valid block-header or end-of-stream marker, the stream is broken
            this.streamComplete = true;
            throw new IOException("BZip2 stream format error");
        }
    }
}
