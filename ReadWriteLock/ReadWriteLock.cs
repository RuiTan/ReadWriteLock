using System;
using System.Threading;

namespace ReadWriteLock
{
    public class ReadWriteLock
    {
        Node head;
        Node tail;
        public int State { get; set; }

        volatile Thread owner;

        private volatile int reentrants;

        public Thread GetOwner()
        {
            return owner;
        }

        public void SetOwner(Thread thread)
        {
            owner = thread;
        }


        public ReadWriteLock()
        {
        }

        public void WriteLock()
        {
            if (head == null)
            {
                Interlocked.Exchange(ref head, new Node());
                Node node = new Node(Thread.CurrentThread, Node.EXCLUSIVE);
                Interlocked.Exchange(ref node.waitStatus, Node.RUNNING);
                Interlocked.Exchange(ref tail, node);
                Interlocked.Exchange(ref reentrants, 0);
                Interlocked.Exchange(ref head.next, node);
                Interlocked.Exchange(ref node.prev, head);
            }
            else
            {
                Thread current = Thread.CurrentThread;
                if (owner == current)
                {
                    Node node = head.next;
                    Interlocked.Increment(ref reentrants);
                }
                else
                {
                    Node t = tail;
                    Node node = new Node(current, Node.EXCLUSIVE);
                    Interlocked.Exchange(ref t.next, node);
                    Interlocked.Exchange(ref node.prev, t);
                    Interlocked.Exchange(ref tail, node);
                    Interlocked.Exchange(ref node.waitStatus, Node.WAITING);
                    current.Suspend();
                }
            }

        }

        public void WriteUnlock()
        {
            Node node = head.next;
            while (node != null && node.waitStatus == Node.CANCELLED)
            {
                Interlocked.Exchange(ref head.next, node);
                Interlocked.Exchange(ref node.prev, head);
                node = node.next;
            }
            Interlocked.Exchange(ref node.waitStatus, Node.CANCELLED);
            AwakeNext();
        }

        public void AwakeNext()
        {
            Node node = head.next;
            while (node != null && node.waitStatus == Node.CANCELLED)
            {
                node = node.next;
            }
            Interlocked.Exchange(ref head.next, node);
            if (node == null)
            {
                tail = null;
                return;
            }
            else
                Interlocked.Exchange(ref node.prev, head);
            node.thread.Resume();
        }

        public void ReadLock()
        {

        }

        public void ReadUnlock()
        {

        }

        protected bool compareAndSetState(int expect, int update)
        {
            return expect == Interlocked.Exchange(ref expect, update);
        }

        protected bool compareAndSetTail(Node expect, Node update)
        {
            return expect == Interlocked.Exchange(ref expect, update);
        }

        protected bool compareAndSetHead(Node node)
        {
            return head == Interlocked.Exchange(ref head, node);
        }

        protected bool compareAndSetStatus(Node node, int update)
        {
            return node.waitStatus == Interlocked.Exchange(ref node.waitStatus, update);
        }

        protected bool compareAndSetNext(Node node, Node update)
        {
            return node.next == Interlocked.Exchange(ref node.next, update);
        }

        protected bool compareAndAddCount(HoldCounter holdCounter)
        {
            return holdCounter.Count == Interlocked.Increment(ref holdCounter.Count);
        }
    }
}
