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
            lock (this)
            {
                Thread current = Thread.CurrentThread;
                if (head == null)
                {
                    head = new Node();
                }
                if (tail == null)
                {
                    Node node = new Node(current, Node.EXCLUSIVE);
                    node.waitStatus = Node.RUNNING;
                    tail = node;
                    owner = current;
                    reentrants = 1;
                    head.next = node;
                    node.prev = head;
                }
                else
                {
                    if (owner == current)
                    {
                        reentrants++;
                    }
                    else
                    {
                        Node t = tail;
                        Node node = new Node(current, Node.EXCLUSIVE);
                        t.next = node;
                        node.prev = t;
                        tail = node;
                        node.waitStatus = Node.WAITING;
                        node.manualResetEvent.Reset();
                    }
                }
            }
        }

        public void WriteUnlock()
        {
            lock (this)
            {
                reentrants--;
                Node node = head.next;
                while (node != null && node.waitStatus == Node.CANCELLED)
                { 
                    node = node.next;
                    head.next = node;
                    if (node != null)
                        node.prev = head;
                }
                if (node != null)
                {
                    if (reentrants == 0)
                    {
                        node.waitStatus = Node.CANCELLED;
                        AwakeNext();
                    }
                }
            }
        }

        public void AwakeNext()
        {
            Node node = head.next;
            while (node != null && node.waitStatus == Node.CANCELLED)
            {
                node = node.next;
            }
            head.next = node;
            if (node == null)
            {
                tail = null;
                return;
            }
            else
            {
                node.prev = head;
                while(node != null)
                {
                    node.manualResetEvent.Set();
                    node = node.nextReader;
                }
            }
        }
        public void ReadLock()
        {
            lock (this)
            {
                Thread current = Thread.CurrentThread;
                if (head == null)
                {
                    head = new Node();
                }
                if (head.next == null)
                {
                    Node node = new Node(current, Node.SHARED);
                    node.waitStatus = Node.RUNNING;
                    tail = node;
                    owner = current;
                    reentrants = 1;
                    head.next = node;
                    node.prev = head;
                    node.readerHead = node;
                }
                else
                {
                    if (owner == current)
                    {
                        reentrants++;
                    }
                    else
                    {
                        Node node = head.next;
                        Node reader = new Node(current, Node.SHARED);
                        if (node.isShared() && node.readerCount <= Node.Threshold)
                        {
                            AddReader(node, reader);
                        }
                        else
                        {
                            for (; ; )
                            {
                                if (node == null)
                                    break;
                                if (node.isShared() && node.readerCount <= Node.Threshold)
                                {
                                    AddReader(node, reader);
                                    reader.manualResetEvent.Reset();
                                    return;
                                }
                            }
                            tail.next = reader;
                            reader.prev = tail;
                            tail = reader;
                            reader.manualResetEvent.Reset();
                        }
                        
                    }
                }
            }
        }
        public void AddReader(Node readerHead, Node node)
        {
            Node reader = readerHead;
            while (reader.nextReader != null)
            {
                reader = reader.nextReader;
            }
            reader.nextReader = node;
            node.readerHead = readerHead;
            readerHead.readerCount++;
        }
        public void ReadUnlock()
        {
            lock (this)
            {
                reentrants--;
                Node node = head.next;
                while (node != null && node.waitStatus == Node.CANCELLED)
                {
                    node = node.next;
                    head.next = node;
                    if (node != null)
                        node.prev = head;
                }
                if (node != null)
                {
                    if (reentrants == 0)
                    {
                        node.waitStatus = Node.CANCELLED;
                        node.readerHead.readerCount--;
                        if (node.readerHead.readerCount == 0)
                        {
                            if (node.readerHead.waitStatus != Node.CANCELLED)
                                throw new Exception("操作冲突，读数量为0时仍然有在读");
                            AwakeNext();
                        }
                    }
                }
            }
        }
    }
}
