// Added by drone1400, July 2022
// Location: https://github.com/drone1400/bzip2

using System.IO;

namespace Bzip2 {
    internal class TempBlockBuffer
    {
        public byte[] Buffer { get; }
        public int Count { get; private set; }
        public int BlockId { get; }
        
        public bool IsFinished { get => this.Count == this.Buffer.Length; }

        /// <summary>
        /// Creates an empty temporary block buffer
        /// </summary>
        /// <param name="size">Block buffer size</param>
        /// <param name="blockId">numeric ID value of the block buffer</param>
        public TempBlockBuffer(int size, int blockId) {
            this.Count = 0;
            this.BlockId = blockId;
            this.Buffer = new byte[size];
        }
        
        /// <summary>
        /// Creates a temporary block buffer by reading a nubmer of bytes from a stream
        /// </summary>
        /// <param name="stream">Stream to read the data from</param>
        /// <param name="size">number of bytes to read, also total block size</param>
        /// <param name="blockId">numeric ID value of the block buffer</param>
        public TempBlockBuffer(Stream stream, int size, int blockId)
        {
            this.BlockId = blockId;
            this.Buffer = new byte[size];
            this.Count = stream.Read(this.Buffer, 0, this.Buffer.Length);
        }

        /// <summary>
        /// Reads a number of bytes into the buffer from another byte buffer
        /// </summary>
        /// <param name="buffer">source byte buffer</param>
        /// <param name="offset">source buffer offset</param>
        /// <param name="count">number of bytes to read</param>
        /// <returns>number of bytes actually read (fewer if this buffer becomes full)</returns>
        public int ReadBytes(byte[] buffer, int offset, int count) {
            for (int i = 0; i < count; i++) {
                if (this.IsFinished) {
                    return i;
                }

                this.Buffer[this.Count++] = buffer[offset++];
            }

            return count;
        }
        
        /// <summary>
        /// Reads a single byte into the buffer
        /// </summary>
        /// <param name="data">data byte</param>
        /// <returns>number of bytes actually read (0 if this buffer becomes full, otherwise 1)</returns>
        public int ReadByte(byte data) {
            if (this.IsFinished) {
                return 0;
            }

            this.Buffer[this.Count++]  = data;

            return 1;
        }
    }
}
