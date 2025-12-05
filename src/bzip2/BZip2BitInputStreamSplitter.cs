// Added by drone1400, December 2025
// Location: https://github.com/drone1400/bzip2

using System;
using System.IO;
namespace Bzip2
{

    /// <summary>
    /// Helper class for splitting a BZip2 stream into multiple Memory Streams made up of individual BZip2 blocks.
    /// The spitting is done by looking for the magic block header 6 byte sequence.
    /// The output MemoryStreams bits are left aligned.
    /// </summary>
    internal class BZip2BitInputStreamSplitter
    {

        // The stream from which bits are read
        private readonly BZip2BitInputStream _bitInputStream;

        // internal temporary buffer
        private MemoryStream _buffer = null;

        private bool _isStreamComplete = false;
        private int _blockLevel = 0;
        private int _blockSizeBytes = 0;
        private uint _finalCrc = 0;
        private ulong _crtVal = 0x00;
        private int _bitCount = 0;

        public bool IsStreamComplete => this._isStreamComplete;
        public int BlockLevel => this._blockLevel;
        public int BlockSizeBytes => this._blockSizeBytes;
        public uint FinalCrc => this._finalCrc;

        public BZip2BitInputStreamSplitter(Stream inputStream, InputStreamHeaderCheckType inputStreamHeaderCheck = InputStreamHeaderCheckType.FullHeader, int manualBlockLevel = 9)
        {

            if (inputStream == null)
            {
                throw new ArgumentException("Null input stream");
            }

            this._bitInputStream = new  BZip2BitInputStream(inputStream);


            this.Initialize(inputStreamHeaderCheck, manualBlockLevel);
        }

        private void Initialize(InputStreamHeaderCheckType inputStreamHeaderCheck, int blockLevel)
        {
            // If the stream has been explicitly closed, throw an exception
            if (this._bitInputStream == null)
                throw new IOException("Stream closed");

            // If we're already at the end of the stream, do nothing
            if (this._isStreamComplete)
                return;

            /* Read the stream header */
            try
            {
                switch (inputStreamHeaderCheck)
                {
                    case InputStreamHeaderCheckType.FullHeader:
                    {
                        uint marker1 = this._bitInputStream.ReadBits(16);
                        uint marker2 = this._bitInputStream.ReadBits(8);
                        blockLevel = ((int)this._bitInputStream.ReadBits(8) - '0');
                        if (marker1 != BZip2Constants.STREAM_START_MARKER_1 ||
                            marker2 !=  BZip2Constants.STREAM_START_MARKER_2 ||
                            blockLevel < 1 ||  blockLevel > 9)
                        {
                            throw new IOException("Invalid BZip2 header");
                        }
                        break;
                    }
                    case InputStreamHeaderCheckType.NoBz:
                    {
                        uint marker2 = this._bitInputStream.ReadBits(8);
                        blockLevel = ((int)this._bitInputStream.ReadBits(8) - '0');
                        if (marker2 !=  BZip2Constants.STREAM_START_MARKER_2 ||
                            blockLevel < 1 ||  blockLevel > 9)
                        {
                            throw new IOException("Invalid BZip2 header");
                        }
                        break;
                    }
                    case InputStreamHeaderCheckType.NoBzh:
                    {
                        blockLevel = ((int)this._bitInputStream.ReadBits(8) - '0');
                        if (blockLevel < 1 ||  blockLevel > 9)
                        {
                            throw new IOException("Invalid BZip2 header");
                        }
                        break;
                    }
                }

                this._blockLevel = blockLevel;
                this._blockSizeBytes = blockLevel * 100000;

                this._crtVal = this._bitInputStream.ReadBits(24);
                this._crtVal <<= 24;
                this._crtVal |= this._bitInputStream.ReadBits(24);

                // expecting the first block header immediately after
                if (this._crtVal != BZip2Constants.BLOCK_HEADER_MARKER)
                {
                    throw new IOException("BZip2 stream format error");
                }

                this._buffer = new MemoryStream(this._blockSizeBytes + 100000);
            } catch (IOException)
            {
                // If the stream header was not valid, stop trying to read more data
                this._isStreamComplete = true;
                throw;
            }
        }

        /// <summary>
        /// Finds the next BZip2 block and returns an in memory copy of it
        /// </summary>
        /// <returns>MemoryStream</returns>
        public MemoryStream CopyNextBlock()
        {
            void FlushByte()
            {
                this._buffer.WriteByte((byte)(this._crtVal >> 48));
                this._crtVal &= 0x0000FFFF_FFFFFFFF;
                this._bitCount = 0;
            }

            void FlushPartialByte()
            {
                byte partialByte = (byte)(this._crtVal >> 48);
                partialByte <<= (8 - this._bitCount);
                this._buffer.WriteByte(partialByte);

                this._crtVal &= 0x0000FFFF_FFFFFFFF;
                this._bitCount = 0;
            }

            while (true)
            {
                this._crtVal <<= 1;
                this._crtVal |= this._bitInputStream.ReadBits(1);
                ulong testVal = this._crtVal & 0x0000FFFF_FFFFFFFF;
                this._bitCount++;
                if (this._bitCount == 8)
                {
                    FlushByte();
                }

                if (testVal == BZip2Constants.STREAM_END_MARKER)
                {
                    // we found the magic bit sequence for end of stream, that means we're done!
                    if (this._bitCount > 0)
                    {
                        FlushPartialByte();
                    }

                    // read the 32 bit CRC at the end of the file 
                    this._finalCrc = this._bitInputStream.ReadInteger();

                    MemoryStream retBuffer = this._buffer;
                    this._buffer = null;
                    this._isStreamComplete = true;
                    return retBuffer;
                }

                if (testVal == BZip2Constants.BLOCK_HEADER_MARKER)
                {
                    // we found the magic bit sequence!
                    if (this._bitCount > 0)
                    {
                        FlushPartialByte();
                    }
                    MemoryStream retBuffer = this._buffer;
                    this._buffer = new MemoryStream(this._blockSizeBytes);
                    return retBuffer;
                }
            }
        }
    }
}
