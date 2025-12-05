using System.IO;
namespace Bzip2.Interface
{
    public interface IBZip2BitInputStream
    {
        /// <summary>Reads a single bit from the wrapped input stream</summary>
        /// <return>true if the bit read was 1, otherwise false</return>
        /// <exception cref="IOException">if no more bits are available in the input stream</exception>
        bool ReadBoolean();

        /// <summary>Reads a zero-terminated unary number from the wrapped input stream</summary>
        /// <return>The unary number</return>
        /// <exception cref="IOException">if no more bits are available in the input stream</exception>
        uint ReadUnary();

        /// <summary>Reads up to 32 bits from the wrapped input stream</summary>
        /// <param name="count">The number of bits to read (maximum 32)</param>
        /// <return>The bits requested, right-aligned within the integer</return>
        /// <exception cref="IOException">if no more bits are available in the input stream</exception>
        uint ReadBits(int count);

        /// <summary>Reads 32 bits of input as an integer</summary>
        /// <return>The integer read</return>
        /// <exception cref="IOException">if 32 bits are not available in the input stream</exception>
        uint ReadInteger();
    }
}
