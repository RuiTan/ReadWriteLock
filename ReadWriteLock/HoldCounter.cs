using System;
using System.Threading;

namespace ReadWriteLock
{
    public class HoldCounter
    {
        public HoldCounter()
        {
        }

        public HoldCounter(int count, int threadCode)
        {
            Count = count;
            ThreadCode = threadCode;
        }

        public int Count;
        public int ThreadCode;
    }
}
