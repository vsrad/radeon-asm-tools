namespace VSRAD.Syntax.Parser.Helper
{
    unsafe internal sealed class BitHelper
    {   // should not be serialized
        private const byte MarkedBitFlag = 1;
        private const byte IntSize = 32;

        // m_length of underlying int array (not logical bit array)
        private readonly int _length;

        // ptr to stack alloc'd array of ints
        private readonly int* _arrayPtr;

        // array of ints
        private readonly int[] _array;

        // whether to operate on stack alloc'd or heap alloc'd array 
        private readonly bool _useStackAlloc;

        /// <summary>
        /// Instantiates a BitHelper with a heap alloc'd array of ints
        /// </summary>
        /// <param name="bitArray">int array to hold bits</param>
        /// <param name="length">length of int array</param>
        internal BitHelper(int* bitArrayPtr, int length)
        {
            _arrayPtr = bitArrayPtr;
            _length = length;
            _useStackAlloc = true;
        }

        /// <summary>
        /// Instantiates a BitHelper with a heap alloc'd array of ints
        /// </summary>
        /// <param name="bitArray">int array to hold bits</param>
        /// <param name="length">length of int array</param>
        internal BitHelper(int[] bitArray, int length)
        {
            _array = bitArray;
            _length = length;
        }

        /// <summary>
        /// Mark bit at specified position
        /// </summary>
        /// <param name="bitPosition"></param>
        internal void MarkBit(int bitPosition)
        {
            int bitArrayIndex = bitPosition / IntSize;
            if (bitArrayIndex < _length && bitArrayIndex >= 0)
            {
                int flag = (MarkedBitFlag << (bitPosition % IntSize));
                if (_useStackAlloc)
                {
                    _arrayPtr[bitArrayIndex] |= flag;
                }
                else
                {
                    _array[bitArrayIndex] |= flag;
                }
            }
        }

        /// <summary>
        /// Is bit at specified position marked?
        /// </summary>
        /// <param name="bitPosition"></param>
        /// <returns></returns>
        internal bool IsMarked(int bitPosition)
        {
            int bitArrayIndex = bitPosition / IntSize;
            if (bitArrayIndex < _length && bitArrayIndex >= 0)
            {
                int flag = (MarkedBitFlag << (bitPosition % IntSize));
                if (_useStackAlloc)
                {
                    return ((_arrayPtr[bitArrayIndex] & flag) != 0);
                }
                else
                {
                    return ((_array[bitArrayIndex] & flag) != 0);
                }
            }
            return false;
        }

        /// <summary>
        /// How many ints must be allocated to represent n bits. Returns (n+31)/32, but 
        /// avoids overflow
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        internal static int ToIntArrayLength(int n)
        {
            return n > 0 ? ((n - 1) / IntSize + 1) : 0;
        }
    }
}
