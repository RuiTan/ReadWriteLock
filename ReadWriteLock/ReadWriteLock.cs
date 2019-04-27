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
                    Monitor.Enter(this);
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
                        while (node.prev != head && node.waitStatus != Node.SIGNAL)
                        {
                        }
                        Monitor.Enter(this);
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
                        Monitor.Exit(this);
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
                    node.waitStatus = Node.SIGNAL;
                    node = node.nextReader;
                    node.prev = head;
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
                                    while (reader.prev != head && reader.waitStatus != Node.SIGNAL) { }
                                    return;
                                }
                                node = node.next;
                            }
                            tail.next = reader;
                            reader.prev = tail;
                            tail = reader;
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
            node.prev = reader.prev;
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
