using System;
using System.Threading;

namespace ReadWriteLock
{
    class MainClass
    {
        class Number
        {
            private int num = 0;
            public int GetNum() { return num; }
            public void AddNum() { num++; }
        }
        static Number number = new Number();
        static int N = 0;
        public static void Add(ReadWriteLock readWriteLock)
        {
            for (int i = 0; i < 100000000; i++)
            {
                readWriteLock.WriteLock();
                number.AddNum();
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
            for (int i = 0; i < 100000000; i++)
            {
                number.AddNum();
            }
        }
        public static void TestReentrantWriter(ReadWriteLock readWriteLock)
        {
            readWriteLock.WriteLock();
            readWriteLock.WriteLock();
            readWriteLock.WriteLock();
            readWriteLock.WriteLock();
            Thread.Sleep(1000);
            readWriteLock.WriteUnlock();
            readWriteLock.WriteUnlock();
            readWriteLock.WriteUnlock();
            readWriteLock.WriteUnlock();
        }
        public static void TestWriter(ReadWriteLock readWriteLock)
        {
            readWriteLock.WriteLock();
            Thread.Sleep(1000);
            Console.WriteLine(Thread.CurrentThread.Name + "执行完毕");
            readWriteLock.WriteUnlock();
        }
        public static void TestReader(ReadWriteLock readWriteLock)
        {
            readWriteLock.ReadLock();
            Thread.Sleep(1000);
            Console.WriteLine(Thread.CurrentThread.Name + "执行完毕");
            readWriteLock.ReadUnlock();
        }
        public static void Main(string[] args)
        {
            //ReadWriteLock readWriteLock = new ReadWriteLock();
            //for (int i = 1; i <= 3; i++)
            //    CreateThread(false, i, readWriteLock);
            //for (int i = 1; i <= 7; i++)
            //    CreateThread(true, i, readWriteLock);
            //for (int i = 0; i < 100; i++)
            //{
            //    Thread.Sleep(500);
            //    readWriteLock.PrintQueue();
            //}
            TestAdd();
            Console.ReadKey();
        }
        static void TestAdd()
        {
            var start = DateTime.Now;
            Thread thread1 = new Thread(Add);
            Thread thread2 = new Thread(Add);
            thread1.Name = "thread1";
            thread2.Name = "thread2";
            thread1.Start();
            thread2.Start();
            thread1.Join();
            thread2.Join();
            Console.WriteLine(number.GetNum());
            Console.WriteLine(DateTime.Now - start);
        }
        static void TestLock()
        {
            var start = DateTime.Now;
            ReadWriteLock readWriteLock = new ReadWriteLock();
            Thread thread1 = new Thread(() => Add(readWriteLock));
            Thread thread2 = new Thread(() => Add(readWriteLock));
            thread1.Name = "thread1";
            thread2.Name = "thread2";
            thread1.Start();
            thread2.Start();
            thread1.Join();
            thread2.Join();
            Console.WriteLine(number.GetNum());
            Console.WriteLine(DateTime.Now - start);
        }

        static void Print()
        {
            for (; ; )
            {
                Thread.Sleep(1000);
                Console.WriteLine(Thread.CurrentThread + " : write line");
            }
        }
        static void CreateThread(bool share, int i, ReadWriteLock readWriteLock)
        {
            Thread thread;
            if (share) 
            {
                thread = new Thread(() => TestReader(readWriteLock));
                thread.Name = "Reader-" + i;            
            }
            else
            {
                thread = i == 5 ? new Thread(() => TestReentrantWriter(readWriteLock)) : new Thread(() => TestWriter(readWriteLock));
                thread.Name = "Writer-" + i;
            }
            thread.Start();
        }
    }
}
