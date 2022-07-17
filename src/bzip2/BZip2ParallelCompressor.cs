// Added by drone1400, July 2022
// Location: https://github.com/drone1400/bzip2

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Bzip2 {
    
    /// <summary>
    /// A helper class for compressing an input stream into a given output stream using multithreadded BZip2.
    /// </summary>
    /// <remarks>
    /// Use <see cref="StartCompression"/> to start compressing the whole input stream.
    /// Using this class to compress a whole stream is faster than using <see cref="BZip2ParallelOutputStream"/>,
    /// since the later uses double buffering of input data.
    /// Although <see cref="StartCompression"/> can be called with a single thread argument, it is faster to use <see cref="BZip2OutputStream"/> in that case.
    /// Instances of this class are not threadsafe.
    /// </remarks>
    public class BZip2ParallelCompressor {

        #region  Private Fields
        
        // is there a point to limit this?...
        private const int ABSOLUTE_MAX_THREADS = 128;

        // block size fields
        private readonly int _readBlockSize;
        private readonly int _compressBlockSize;
        private readonly int _blockLevel;

        // max number of active threads
        private int _mtAdditionalThreads = 0;
        
        // number of active threads
        private int _mtActiveThreads = 0;
        
        // next inptu block id
        private int _mtNextInputBlockId = 0;
        
        // next output block id
        private int _mtNextOutputBlockId = 0;

        // flag indicating input stream is finished
        private bool _mtIsDoneReading = false;

        // number of done processing blocks that are pending writing
        private int _mtPendingWritingBlocks = 0;

        // yikes! sounds bad right?
        private bool _unsafeFatalException = false;

        // dictionary of processed blocks
        private readonly Dictionary<int, BZip2BitMetaBuffer> _mtProcessedBlocks = new Dictionary<int, BZip2BitMetaBuffer>();
        private readonly Queue<TempBlockBuffer> _mtPendingBlocksQueue = new Queue<TempBlockBuffer>();

        private readonly object _syncRootProcesing = new object();
        private readonly object _syncRootActiveThread = new object();

        // The input stream
        private readonly Stream _inputStream;
        
        // The output stream
        private readonly Stream _outputStream;
        
        // The bit output stream
        private readonly BZip2BitOutputStream _bitStream;
        
        // The merged CRC of all blocks compressed so far
        private uint _streamCrc = 0;
        
        #endregion

        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="input">Input stream to compress</param>
        /// <param name="output">Output stream to write compressed data to</param>
        /// <param name="blockLevel">The BZip2 block size as a multiple of 100,000 bytes (minimum 1, maximum 9)</param>
        public BZip2ParallelCompressor(Stream input, Stream output, int blockLevel = 9)
        {
            this._inputStream = input;
            this._outputStream = output;
            this._bitStream = new BZip2BitOutputStream(output);

            if (blockLevel < 1 ) blockLevel = 1;
            if (blockLevel > 9 ) blockLevel = 9;

            this._blockLevel = blockLevel;
            
            // supposedly a block can only expand 1.25x, so 0.8 of normal block size should always be safe...
            this._readBlockSize = 80000 * this._blockLevel;
            this._compressBlockSize = 100000 * this._blockLevel;
        }
        
        /// <summary>
        /// Start compressing the given input stream into the output stream
        /// </summary>
        /// <remarks>
        /// The calling thread will be used for I/O operations, an additional
        /// number of block processing threads will be created equal to the given parameter.
        /// </remarks>
        /// <param name="blockProcessingThreads">Number of block processing threads to use.</param>
        public void StartCompression(int blockProcessingThreads = 0)
        {
            // evaluate thread count
            if (blockProcessingThreads < 0) blockProcessingThreads = 0;
            if (blockProcessingThreads > ABSOLUTE_MAX_THREADS) blockProcessingThreads = ABSOLUTE_MAX_THREADS;
            this._mtAdditionalThreads = blockProcessingThreads;
            
            this.WriteBz2Header();

            if (this._mtAdditionalThreads == 0)
            {
                this.SingleThreadCompression();
            }
            else
            {
                this.MultiThreadCompression();
            }

            this.WriteBz2FooterAndFlush();
        }

        /// <summary>
        /// Executes compression on current thread...
        /// </summary>
        /// <exception cref="Exception">If anything goes wrong...</exception>
        private void SingleThreadCompression()
        {
            int blockId = 0;

            while (true)
            {
                byte[] buffer = new byte[this._readBlockSize];
                int count = this._inputStream.Read(buffer, 0, buffer.Length);

                // if no more data, exit function
                if (count <= 0) return;

                blockId++;
                
                BZip2BlockCompressor bz2Block = new BZip2BlockCompressor(this._bitStream, this._compressBlockSize);
                int newCount = bz2Block.Write(buffer, 0, count);
                bz2Block.CloseBlock();

                if (newCount != count) {
                    throw new Exception($"Could not write all the bytes for blockId {blockId}... This should never happen!");
                }

                // update CRC
                this._streamCrc = ((this._streamCrc << 1) | (this._streamCrc >> 31)) ^ bz2Block.CRC;
            }
        }

        /// <summary>
        /// Tries to write the next output block
        /// </summary>
        /// <returns>true if successfully wrote an outptu block, false if nothign was done</returns>
        /// <exception cref="Exception">If anything goes wrong...</exception>
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
                        this._mtPendingWritingBlocks --;
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

        /// <summary>
        /// Handles main multithread block input/output operations
        /// </summary>
        /// <exception cref="Exception">If anything goes wrong...</exception>
        private void MultiThreadInputOutput() {
            while (true)
            {
                if (this._unsafeFatalException) {
                    throw new Exception("A BZip2 Processing thread somehow crashed... Can not continue input/output of data...");
                }

                bool canQueueUpMoreBlocks;
                lock (this._syncRootProcesing) {
                    canQueueUpMoreBlocks = this._mtPendingWritingBlocks < this._mtAdditionalThreads * 10 && !this._mtIsDoneReading;
                }
                
                if (canQueueUpMoreBlocks)
                {
                    TempBlockBuffer buff = new TempBlockBuffer(this._inputStream, this._readBlockSize, this._mtNextInputBlockId);

                    if (buff.Count > 0)
                    {
                        lock (this._syncRootProcesing)
                        {
                            this._mtNextInputBlockId++;
                            this._mtPendingBlocksQueue.Enqueue(buff);
                        }
                    }
                    else
                    {
                        lock (this._syncRootProcesing)
                        {
                            this._mtIsDoneReading = true;
                        }
                    }
                }

                // keep writing output as long as possible
                while (this.TryWriteOutputBlockAndIncrementId()) { }

                if (this._mtIsDoneReading && 
                    this._mtNextInputBlockId == this._mtNextOutputBlockId)
                {
                    // all done, can safely exit
                    return;
                }

                if (this._mtIsDoneReading &&
                    this._mtNextInputBlockId > this._mtNextOutputBlockId &&
                    this._mtActiveThreads == 0)
                {
                    // this should be impossible...
                    throw new Exception("BZip2 input / output operations could not be finished... All worker threads are stopped but there are still unprocessed blocks!");
                }
            }
        }

        /// <summary>
        /// Multi thread block compression - gets a <see cref="TempBlockBuffer"/> from the buffer queue, compresses the data,
        /// then adds the resulting <see cref="BZip2BitMetaBuffer"/> to the processed block dictionary based on its BlockID
        /// </summary>
        /// <exception cref="Exception">If anything goes wrong...</exception>
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
                        else if (this._mtIsDoneReading)
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
                            this._mtPendingWritingBlocks ++;
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

        /// <summary>
        /// Multithread compression starter - creates worker threads and starts stream input/output operations on calling thread
        /// </summary>
        private void MultiThreadCompression()
        {
            // start processing threads
            for (int i = 0; i < this._mtAdditionalThreads; i++)
            {
                Thread thread = new Thread(this.MultiThreadWorkerAction)
                {
                    Name = $"PBZip2 - Thread #{i}",
                    IsBackground = true,
                    Priority = ThreadPriority.Normal,
                };

                lock (this._syncRootActiveThread)
                {
                    this._mtActiveThreads++;
                    thread.Start();
                }
            }

            // continue I/O operations in this thread
            this.MultiThreadInputOutput();
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

        
    }
}
