using System;
using System.Threading;

namespace ReadWriteLock
{
    public class Node
    {
        public static ManualResetEvent ManualResetEvent = new ManualResetEvent(false);
        // 读节点
        public static readonly Node SHARED = new Node();
        // 写节点
        public static readonly Node EXCLUSIVE = null;
        // 读链长度阈值
        public static int Threshold = 100;

        public static readonly int CANCELLED = 1;
        public static readonly int RUNNING = -1;
        public static readonly int WAITING = -2;

        public int waitStatus;
        // 前驱节点
        public Node prev;
        // 后继节点
        public Node next;
        // 持有线程
        public Thread thread;
        public ManualResetEvent manualResetEvent = ManualResetEvent;

        //以下参数仅对读节点适用
        // 读链头
        public Node readerHead;
        // 后继读节点
        public Node nextReader;
        // 读链长度
        public int readerCount = 0;

        // 节点类型
        public Node mode;
        // 是否是共享节点
        public bool isShared()
        {
            return mode == SHARED;
        }

        // 当前节点前驱
        public Node predecessor()
        {
            Node p = prev;
            if (p == null)
                throw new NullReferenceException();
            else
                return p;
        }

        public Node()
        {
        }

        public Node(Thread thread, Node mode)
        {
            this.mode = mode;
            this.thread = thread;
        }

        public Node(Thread thread, int waitStatus)
        {
            this.waitStatus = waitStatus;
            this.thread = thread;
        }
    }
}
