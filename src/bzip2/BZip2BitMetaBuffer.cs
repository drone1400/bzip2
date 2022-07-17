// Added by drone1400, July 2022
// Location: https://github.com/drone1400/bzip2

using System;
using System.Collections.Generic;
using System.IO;

namespace Bzip2
{
    /// <summary>A collection of bit output data</summary>
    /// <remarks>
    /// Allows the writing of single bit booleans, unary numbers, bit
    /// strings of arbitrary length(up to 24 bits), and bit aligned 32-bit integers.A single byte at a
    /// time is written to a list of structures that serves as a buffer for use in parallelized
    /// execution of block compression
    /// </remarks>
    internal class BZip2BitMetaBuffer : IBZip2BitOutputStream
    {
        private struct Bzip2BitDataPair
        {
            public byte BitN { get;}
            public uint BitV { get; }
            public Bzip2BitDataPair(byte bitN, uint bitV)
            {
                this.BitN = bitN;
                this.BitV = bitV;
            }
        }
        
        private List<Bzip2BitDataPair> _data;

        
        /// <summary>
        /// Compressed block CRC to be stored here when block is finished
        /// </summary>
        public uint BlockCrc { get => this._blockCrc; }
        private uint _blockCrc = 0;
        
        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="internalListBufferCapacity">Initial internal buffer list capacity</param>
        public BZip2BitMetaBuffer(int internalListBufferCapacity)
        {
            this._data = new List<Bzip2BitDataPair>(internalListBufferCapacity);
        }

        /// <summary>
        /// Set compressed block CRC when finished
        /// </summary>
        /// <param name="crc">CRC value</param>
        public void SetCrc(uint crc) {
            this._blockCrc = crc;
        }
        
        /// <summary>
        /// Writes all the buffer data to the real <see cref="BZip2BitOutputStream"/>
        /// </summary>
        /// <param name="stream">The real bit output stream</param>
        /// <exception cref="Exception">if an error occurs writing to the stream</exception>
        public void WriteToRealOutputStream(BZip2BitOutputStream stream) {
            for (int i = 0; i < this._data.Count; i++)
            {
                stream.WriteBits(this._data[i].BitN, this._data[i].BitV);
            }
        }

        #region IBZip2BitOutputStream implementation

        public void WriteBoolean (bool value) 
        {
            this._data.Add(new Bzip2BitDataPair(1, value ? (uint)1 : (uint)0));
        }
        
        public void WriteUnary (int value)  
        {
            while (value >= 8) {
                this._data.Add(new Bzip2BitDataPair(8, 0xFF));
                value -= 8;
            }
            switch (value) {
                case 7: this._data.Add(new Bzip2BitDataPair(7, 0x7F)); break;
                case 6: this._data.Add(new Bzip2BitDataPair(6, 0x3F)); break;
                case 5: this._data.Add(new Bzip2BitDataPair(5, 0x1F)); break;
                case 4: this._data.Add(new Bzip2BitDataPair(4, 0x0F)); break;
                case 3: this._data.Add(new Bzip2BitDataPair(3, 0x07)); break;
                case 2: this._data.Add(new Bzip2BitDataPair(2, 0x03)); break;
                case 1: this._data.Add(new Bzip2BitDataPair(1, 0x01)); break;
            }
            
            this._data.Add(new Bzip2BitDataPair(1, 0x00));
        }
        
        public void WriteBits (int count,  uint value) 
        {
            this._data.Add(new Bzip2BitDataPair((byte)count, value));
        }

        public void WriteInteger (uint value)  
        {
            this.WriteBits (16, (value >> 16) & 0xffff);
            this.WriteBits (16, value & 0xffff);
        }
        
        /// <summary>
        /// For compliance with interface, doesn't do anything
        /// </summary>
        public void Flush() {
            
        }

        #endregion
    }
}
