// Added by drone1400, July 2022
// Location: https://github.com/drone1400/bzip2

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Bzip2 {
    
    /// <summary>An OutputStream wrapper that compresses BZip2 data using multiple threads</summary>
    /// <remarks>Instances of this class are not threadsafe</remarks>
    public class BZip2ParallelOutputStream : Stream{
        #region  Private Fields
        
        // is there a point to limit this?...
        private const int ABSOLUTE_MAX_THREADS = 128;

        // block size fields
        private readonly int _readBlockSize;
        private readonly int _compressBlockSize;
        private readonly int _blockLevel;

        // max number of active threads
        private int _mtCompressorThreads = 0;
        
        // number of active threads
        private int _mtActiveThreads = 0;
        private int _mtNextThreadId = 0;

        private int _mtNextInputBlockId = 0;
        private int _mtNextOutputBlockId = 0;

        // flag indicating input stream is finished
        private bool _mtStreamIsFinished = false;

        // yikes! sounds bad right?
        private bool _unsafeFatalException = false;

        // dictionary of processed blocks
        private readonly Dictionary<int, BZip2BitMetaBuffer> _mtProcessedBlocks = new Dictionary<int, BZip2BitMetaBuffer>();
        private readonly Queue<TempBlockBuffer> _mtPendingBlocksQueue = new Queue<TempBlockBuffer>();

        private readonly object _syncRootProcesing = new object();
        private readonly object _syncRootActiveThread = new object();

        // The output stream
        private readonly Stream _outputStream;
        
        // The bit output stream
        private readonly BZip2BitOutputStream _bitStream;
        
        // The merged CRC of all blocks compressed so far
        private uint _streamCrc = 0;
        
        //
        private TempBlockBuffer _currentBlockBuffer = null;
        
        // True if the underlying stream will be closed with the current Stream
        private bool _isOwner;
        #endregion

        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="output">The output stream in which to write the compressed data</param>
        /// <param name="compressorThreads">Maximum number of block processing threads to use</param>
        /// <param name="isOwner">True if the underlying stream will be closed with the current Stream</param>
        /// <param name="blockLevel">The BZip2 block size as a multiple of 100,000 bytes (minimum 1, maximum 9)</param>
        /// <remarks>When writing to the stream, data will be buffered into temporary byte arrays with
        /// size a multiple of 80,000 bytes depending on the specified block multiplier.</remarks>
        public BZip2ParallelOutputStream(Stream output, int compressorThreads, bool isOwner = true, int blockLevel = 9)
        {
            this._outputStream = output;
            this._bitStream = new BZip2BitOutputStream(output);

            if (blockLevel < 1 ) blockLevel = 1;
            if (blockLevel > 9 ) blockLevel = 9;
            this._blockLevel = blockLevel;
            
            // evaluate thread count
            if (compressorThreads < 1) compressorThreads = 1;
            if (compressorThreads > ABSOLUTE_MAX_THREADS) compressorThreads = ABSOLUTE_MAX_THREADS;
            this._mtCompressorThreads = compressorThreads;

            // supposedly a block can only expand 1.25x, so 0.8 of normal block size should always be safe...
            this._readBlockSize = 80000 * this._blockLevel;
            this._compressBlockSize = 100000 * this._blockLevel;
            
            // initialize initial temp buffer
            this._currentBlockBuffer = new TempBlockBuffer(this._readBlockSize, this._mtNextInputBlockId++);

            this._isOwner = isOwner;
            
            // write the bz2 header...
            this.WriteBz2Header();
        }

        private bool TryCreateNewProcessingThread() {
            lock (this._syncRootActiveThread) {
                if (this._mtActiveThreads < this._mtCompressorThreads) {
                    Thread thread = new Thread(this.MultiThreadWorkerAction)
                    {
                        Name = $"PBZip2 - Thread #{this._mtNextThreadId++}",
                        IsBackground = true,
                        Priority = ThreadPriority.Normal,
                    };
                    this._mtActiveThreads++;
                    thread.Start();

                    return true;
                }
            }

            return false;
        }

        private bool TryWriteOutputBlockAndIncrementId()
        {
            try {
                int blockId = this._mtNextOutputBlockId;
                
                BZip2BitMetaBuffer currentOutput = null;

                lock (this._syncRootProcesing)
                {
                    // check if the next output block can be extracted
                    if (this._mtProcessedBlocks.ContainsKey(blockId))
                    {
                        currentOutput = this._mtProcessedBlocks[blockId];
                        this._mtProcessedBlocks.Remove(blockId);
                    }
                }

                // nothing to write... exit with unchanged blockId
                if (currentOutput == null) return false;

                // update file CRC
                this._streamCrc = ((this._streamCrc << 1) | (this._streamCrc >> 31)) ^ currentOutput.BlockCrc;

                currentOutput.WriteToRealOutputStream(this._bitStream);
                
                this._mtNextOutputBlockId = blockId + 1;
                return true;
            }
            catch (Exception ex)
            {
                // set this without any locks...
                this._unsafeFatalException = true;

                // rethrow exception, hopefully something catches it?...
                throw new Exception("BZip2 error writing output data! See inner exception for details!",ex);
            }
        }
        
        private void MultiThreadWorkerAction()
        {
            try
            {
                while (true)
                {
                    // abort if one of the threads failed since can not continue compression...
                    if (this._unsafeFatalException) return;

                    // temp input buffer
                    TempBlockBuffer buff = null;

                    lock (this._syncRootProcesing)
                    {
                        if (this._mtPendingBlocksQueue.Count > 0)
                        {
                            buff = this._mtPendingBlocksQueue.Dequeue();
                        }
                        else if (this._mtStreamIsFinished)
                        {
                            // thread can not do anything else, time to stop
                            return;
                        }
                    }

                    if (buff != null) {
                        // initialize temporary buffer for output data...
                        BZip2BitMetaBuffer meta = new BZip2BitMetaBuffer(this._readBlockSize);
                        
                        // process the current block
                        BZip2BlockCompressor compressor = new BZip2BlockCompressor(meta, this._compressBlockSize);
                        int newCount = compressor.Write(buff.Buffer, 0, buff.Count);
                        compressor.CloseBlock();
                        
                        // set CRC in temporary buffer
                        meta.SetCrc(compressor.CRC);
                        
                        if (newCount != buff.Count) {
                            // this should never happen unless _readBlockSize was too big?
                            throw new Exception($"Could not write all the bytes for blockId {buff.BlockId}... This should never happen!");
                        }

                        // add my block to the dictionary
                        lock (this._syncRootProcesing)
                        {
                            this._mtProcessedBlocks.Add(buff.BlockId, meta);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
            }
            catch (Exception ex)
            {
                // set this without any locks...
                this._unsafeFatalException = true;
                
                throw new Exception("BZip2 Processing thread somehow crashed... See inner exception for details!",ex);
            }
            finally
            {
                lock (this._syncRootActiveThread)
                {
                    this._mtActiveThreads--;
                }
            }
        }

        private void WriteBz2Header()
        {
            // write BZIP file header
            this._bitStream.WriteBits(8, 0x42); // B
            this._bitStream.WriteBits(8, 0x5A); // Z
            this._bitStream.WriteBits(8, 0x68); // h
            this._bitStream.WriteBits(8, (uint)(0x30 + this._blockLevel)); // block level digit
        }

        private void WriteBz2FooterAndFlush()
        {
            // end magic
            this._bitStream.WriteBits(8, 0x17);
            this._bitStream.WriteBits(8, 0x72);
            this._bitStream.WriteBits(8, 0x45);
            this._bitStream.WriteBits(8, 0x38);
            this._bitStream.WriteBits(8, 0x50);
            this._bitStream.WriteBits(8, 0x90);

            // write combined CRC
            this._bitStream.WriteBits(16, (this._streamCrc >> 16) & 0xFFFF);
            this._bitStream.WriteBits(16, this._streamCrc & 0xFFFF);

            // flush all remaining bits
            this._bitStream.Flush();
            this._outputStream.Flush();
        }
        
        private bool ProcessTemporaryBuffer() {
            if (this._currentBlockBuffer.Count > 0)
            {
                // make sure queue is not flooded with buffers, wait if that's the case...
                while (true) {
                    bool canQueueUpMoreBlocks;
                    
                    lock (this._syncRootProcesing) {
                        canQueueUpMoreBlocks = this._mtPendingBlocksQueue.Count < this._mtCompressorThreads * 10;
                    }
                    
                    if (canQueueUpMoreBlocks) break;

                    // try writing output block and keep doing so while successful
                    while (this.TryWriteOutputBlockAndIncrementId()) { }
                }

                // enqueue current buffer and prepare new buffer
                lock (this._syncRootProcesing)
                {
                    this._mtPendingBlocksQueue.Enqueue(this._currentBlockBuffer);
                    this._currentBlockBuffer = new TempBlockBuffer(this._readBlockSize, this._mtNextInputBlockId++);
                }
                
                // since we enqueued a buffer, make sure there's an active processing thread to deal with it
                this.TryCreateNewProcessingThread();

                return true;
            }

            return false;
        }
        
        
        private void FinishBitstream() {
            lock (this._syncRootProcesing) {
                if (this._mtStreamIsFinished) return;
                this._mtStreamIsFinished = true;
            }

            // check if there is still data left to write
            if (this._currentBlockBuffer.Count > 0) {
                this.ProcessTemporaryBuffer();
            }

            // decrement next input block since no longer getting another block
            this._mtNextInputBlockId--;

            while (true) {
                lock (this._syncRootProcesing) {
                    if (this._mtNextInputBlockId == this._mtNextOutputBlockId &&
                        this._mtActiveThreads == 0) {
                        // all done, can safely exit
                        break;
                    }
                }

                // check if there are more queued blocks than active threads, create threads while that is the case
                while (this._mtPendingBlocksQueue.Count > this._mtActiveThreads) {
                    this.TryCreateNewProcessingThread();
                }

                // try writing output block and keep doing so while successful
                while (this.TryWriteOutputBlockAndIncrementId()) { }
            }

            // sanity check, this should be impossible and should be caught by some test right?...
            lock (this._syncRootProcesing) {
                if (this._mtActiveThreads != 0 ||
                    this._mtPendingBlocksQueue.Count > 0 ||
                    this._mtProcessedBlocks.Count > 0) {
                    throw new Exception("BZip2 dispose operation sanity check failed!...");
                }
            }

            // finally, write the footer!
            this.WriteBz2FooterAndFlush();
        }
        
        #region Implementation of abstract members of Stream
        #pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        
        // overriding Dispose instead of Close as recommended in https://docs.microsoft.com/en-us/dotnet/api/system.io.stream.close?view=net-6.0
        protected override void Dispose(bool disposing) {
            this.FinishBitstream();
            base.Dispose(disposing);
            if (this._isOwner) {
                this._outputStream.Close();
            }
        }
        
        public override void Flush() {
            throw new NotSupportedException("BZip2ParallelOutputStream does not support 'Flush()' method! Just use 'Close()' instead.");
        }
        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException("BZip2ParallelOutputStream does not support 'Seek(long offset, SeekOrigin origin)' method.");
        }
        public override void SetLength(long value) {
            throw new NotSupportedException("BZip2ParallelOutputStream does not support 'SetLength(long value)' method.");
        }
        public override int Read(byte[] buffer, int offset, int count) {
            throw new NotSupportedException("BZip2ParallelOutputStream does not support 'Read(byte[] buffer, int offset, int count)' method.");
        }

        public override void WriteByte(byte value) {
            if (this._currentBlockBuffer.ReadByte(value) == 0) {
                this.ProcessTemporaryBuffer();
                this._currentBlockBuffer.ReadByte(value);
                
                // try writing output block and keep doing so while successful
                while (this.TryWriteOutputBlockAndIncrementId()) { }
            }
        }

        public override void Write(byte[] data, int offset, int length) {
            while (length > 0) {
                int count = this._currentBlockBuffer.ReadBytes(data, offset, length);
                offset += count;
                length -= count;

                if (this._currentBlockBuffer.IsFinished) {
                    this.ProcessTemporaryBuffer();
                }
                
                // try writing output block and keep doing so while successful
                while (this.TryWriteOutputBlockAndIncrementId()) { }
            }
        }

        public override bool CanRead {
            get => false;
        }
        public override bool CanSeek {
            get => false;
        }
        public override bool CanWrite {
            get => this._outputStream.CanWrite;
        }
        public override long Length {
            get => this._outputStream.Length;
        }

        public override long Position {
            get => this._outputStream.Position;
            set => throw new NotSupportedException("BZip2ParallelOutputStream does not support Set operation for property 'Position'.");
        }
                
        
        #pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        #endregion
    }
}
