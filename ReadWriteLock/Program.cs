using System;
using System.Threading;

namespace ReadWriteLock
{
    class MainClass
    {
        static int N = 0;
        public static void Add(ReadWriteLock readWriteLock)
        {
            for (int i = 0; i < 1000000; i++)
            {
                readWriteLock.WriteLock();
                N++;
                readWriteLock.WriteUnlock();
            }
        }
        public static void Add(Mutex mutex)
        {
            for (int i = 0; i < 1000000; i++)
            {
                mutex.WaitOne();
                N++;
                mutex.ReleaseMutex();
            }
        }
        public static void Add()
        {
            for (; ; )
            {
                N++;
            }
        }
        public static void Main(string[] args)
        {
            ReadWriteLock readWriteLock = new ReadWriteLock();
            Mutex mutex = new Mutex();
            Thread thread1 = new Thread(() => Add(readWriteLock));
            Thread thread2 = new Thread(() => Add(readWriteLock));
            thread1.Name = "thread1";
            thread2.Name = "thread2";
            thread1.Start();
            thread2.Start();
            thread1.Join();
            thread2.Join();
            //Thread thread = new Thread(Add);
            //thread.Start();
            //Thread.Sleep(1000);
            //thread.Suspend();
            //Console.WriteLine(N);
            //Console.WriteLine(N);
            //Console.WriteLine(N);
            //Console.WriteLine(N);
            //thread.Resume();
            //Console.WriteLine(N);
            //Console.WriteLine(N);
            //Console.WriteLine(N);
            Console.WriteLine(N);
            Console.ReadKey();
            //thread.Abort();
        }
    }
}
