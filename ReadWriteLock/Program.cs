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
                readWriteLock.ReadLock();
                N++;
                readWriteLock.ReadUnlock();
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
        public static void Add(ManualResetEvent resetEvent)
        {
            for (int i = 0; i < 1000; i++)
            {
                resetEvent.WaitOne();
                N++;
                resetEvent.Set();
            }
        }
        public static void Add()
        {
            for (; ; )
            {
                N++;
            }
        }
        public static void TestWriter(ReadWriteLock readWriteLock)
        {
            readWriteLock.WriteLock();
            Console.WriteLine(Thread.CurrentThread.Name + " : 开始写");
            Thread.Sleep(100);
            Console.WriteLine(Thread.CurrentThread.Name + " : 写结束");
            readWriteLock.WriteUnlock();
        }
        public static void TestReader(ReadWriteLock readWriteLock)
        {
            readWriteLock.ReadLock();
            Console.WriteLine(Thread.CurrentThread.Name + " : 开始读");
            Thread.Sleep(100);
            Console.WriteLine(Thread.CurrentThread.Name + " : 读结束");
            readWriteLock.ReadUnlock();
        }
        public static void Main(string[] args)
        {
            var start = DateTime.Now;
            ReadWriteLock readWriteLock = new ReadWriteLock();
            Mutex mutex = new Mutex();
            ManualResetEvent manual = new ManualResetEvent(true);
            for (int i = 0; i < 10; i++)
            {
                Thread thread = new Thread(() => TestWriter(readWriteLock));
                thread.Name = "WriteThread-" + i;
                thread.Start();
            }
            for (int i = 0; i < 10; i++)
            {
                Thread thread = new Thread(() => TestReader(readWriteLock));
                thread.Name = "ReadThread-" + i;
                thread.Start();
            }
            //Thread thread1 = new Thread(() => Add(readWriteLock));
            //Thread thread2 = new Thread(() => Add(readWriteLock));
            //thread1.Name = "thread1";
            //thread2.Name = "thread2";
            //thread1.Start();
            //thread2.Start();
            //thread1.Join();
            //thread2.Join();
            //Console.WriteLine(N);
            //Console.WriteLine(DateTime.Now - start);
            Console.ReadKey();
        }
    }
}
