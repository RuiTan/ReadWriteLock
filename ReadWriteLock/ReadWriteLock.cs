using System;
using System.Threading;

namespace ReadWriteLock
{
    public class ReadWriteLock
    {
        // 头节点
        Node head;
        // 尾节点
        Node tail;
        // 当前持有线程
        volatile Thread owner;

        // 线程重入数量
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

        public void PrintQueue()
        {
            if (head != null)
            {
                Console.WriteLine("Head(持有线程：{0}，重入量：{1})", owner.Name, reentrants);
                Node node = head.next;
                while (node != null)
                {
                    Node reader = node;
                    if (node.isShared())
                    {
                        while (reader != null)
                            Console.Write("->【线程名称：{0}，类型：读】", node.thread.Name);
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine("->【线程名称：{0}，类型：写】", node.thread.Name);
                    }
                }
            }
        }

        // 获取写锁
        public void WriteLock()
        {
            Node currentNode = null;
            // 加锁是为了同步修改队列信息
            lock (this)
            {
                // 当前线程
                Thread current = Thread.CurrentThread;
                // 如果头节点为空，则初始化头节点，这里使用了惰性初始化
                if (head == null)
                {
                    head = new Node();
                }
                // 如果尾节点为空，说明当前队列为空，此时线程直接入队列设置为RUNNING状态
                if (tail == null)
                {
                    // 以当前线程创建独占节点
                    Node node = new Node(current, Node.EXCLUSIVE);
                    // 设置等待类型为RUNNING
                    node.waitStatus = Node.RUNNING;
                    // 当前节点加入队列，尾节点为当前节点
                    tail = node;
                    // 设置锁的持有者为当前线程
                    owner = current;
                    // 重入量初始为1
                    reentrants = 1;
                    // 连接头节点与此节点
                    head.next = node;
                    node.prev = head;
                    // 标识当前线程获取了锁，后续线程只能等待
                    Monitor.Enter(this);
                    return;
                    //Console.WriteLine(Thread.CurrentThread.Name + " : 获取写锁成功");
                }
                else
                {
                    // 如果当前线程就是持有线程，说明锁在重入，重入量加1
                    if (owner == current)
                    {
                        reentrants++;
                        return;
                    }
                    // 否则，当前线程需要进入等待队列进行等待
                    else
                    {
                        // 获取尾节点
                        Node t = tail;
                        // 以当前线程创建独占节点
                        Node node = new Node(current, Node.EXCLUSIVE);
                        // 连接尾节点与当前节点
                        t.next = node;
                        node.prev = t;
                        // 重置尾节点为当前节点
                        tail = node;
                        // 设置节点等待状态为WAITING
                        node.waitStatus = Node.WAITING;
                        currentNode = node;
                        //Console.WriteLine(Thread.CurrentThread.Name + " : 获取写锁成功");
                    }
                }
            }
            // 循环检测当前节点是否可以被唤醒
            while (currentNode.prev != head && currentNode.waitStatus != Node.SIGNAL)
            {
            }
            // 当前节点可以被唤醒，此时让锁进入
            Monitor.Enter(this);
        }

        // 释放写锁
        public void WriteUnlock()
        {
            bool exited = false;
            // 加锁是为了同步修改队列信息
            lock (this)
            {
                // 当前锁持有者重入量直接-1
                reentrants--;
                // 获取队列头（除了head之外的头）
                Node node = GetHolderNode();
                // 如果后续节点为空，则释放完成，队列已空；若后续节点不为空，则可能需要唤醒后续节点
                if (node != null)
                {
                    // 若当前锁的重入量为0，说明锁已经完全释放，则需要唤醒后继有效节点（否则可能只是释放了一个锁内部的锁）
                    if (reentrants == 0)
                    {
                        // 当前节点置为无效
                        node.waitStatus = Node.CANCELLED;
                        exited = true;
                    }
                }
            }
            if (exited)
            {
                // 释放当前锁 
                Monitor.Exit(this);
                //Console.WriteLine(Thread.CurrentThread.Name + " : 释放写锁成功");
                // 唤醒后继有效节点
                AwakeNext();
            }
        }

        public void AwakeNext()
        {
            Node node = GetHolderNode();
            // 当前节点为空，说明队列中无有效节点，直接返回即可
            if (node == null)
            {
                tail = null;
                return;
            }
            // 否则需要唤醒此有效节点
            else
            {
                // 连接到头节点
                node.prev = head;
                // 对于读节点来说，需唤醒读链中的所有节点；对于写节点来说，无读链，只会唤醒当前节点
                while(node != null)
                {
                    // 等待类型设置为SIGNAL
                    node.waitStatus = Node.SIGNAL;
                    // 若为读节点，可获取下一个读者；否则获取了null
                    node = node.nextReader;
                    // 下一个读者不为空，则也将其前驱节点设置为头节点
                    if (node != null)
                        node.prev = head;
                }
            }
        }
        // 删除无效节点（可能由于主动中断或者其他因素导致的线程失效），获取第一个有效节点或空节点
        public Node GetHolderNode()
        {
            Node node = head.next;
            while (node != null && node.waitStatus == Node.CANCELLED)
            {
                node = node.next;
            }
            // 连接头节点和持有节点，无效节点全部交由垃圾收集器回收
            head.next = node;
            if (node != null)
                node.prev = head;
            return node;
        }

        public void ReadLock()
        {
            Node reader = null;
            lock (this)
            {
                // 这里的基本的初始化方式和读锁相似，不赘述
                Thread current = Thread.CurrentThread;
                if (head == null)
                {
                    head = new Node();
                }
                if (tail == null)
                {
                    Node node = new Node(current, Node.SHARED);
                    node.waitStatus = Node.RUNNING;
                    tail = node;
                    owner = current;
                    reentrants = 1;
                    head.next = node;
                    node.prev = head;
                    // 当前节点设置为读链的链头
                    node.readerHead = node;
                    return;
                }
                else
                {
                    // 添加重入量
                    if (owner == current)
                    {
                        reentrants++;
                        return;
                    }
                    else
                    {
                        // 获取当前持有锁节点
                        Node node = head.next;
                        // 创建当前持有当前线程的读节点
                        reader = new Node(current, Node.SHARED);
                        // 获取第一个读节点
                        while (node != null && (!node.isShared() || node.waitStatus == Node.CANCELLED))
                        {
                            node = node.next;
                        }
                        // 未找到读节点，说明队列中只有写节点，此时直接添加到队列尾
                        if (node == null)
                        {
                            AddReaderNode(reader);
                            return;
                        }
                        // 否则说明找到了有效的读节点，此时此读节点为队列中第一个读节点，只需要判断此读节点链长是否达到了阈值，
                        //若未超过阈值，直接添加到读链中，并判断当前读链头是否正在读，则可以直接读，否则需要循环检测；
                        //若超过了阈值，则添加到队列尾，并需要循环等待。
                        else
                        {
                            // 未超过阈值
                            if (node.readerCount < Node.Threshold)
                            {
                                // 添加到读链中
                                AddReader(node, reader);
                                // 是否需要循环等待
                                if (node.waitStatus == Node.RUNNING)
                                    return;
                            }
                            // 超过了阈值
                            else
                            {
                                AddReaderNode(reader);
                                return;
                            }
                        }
                    }
                }
            }
            while (reader.prev != head && reader.waitStatus != Node.SIGNAL) { }
            return;
        }
        // 添加读者到指定读链头所在的读链中
        public void AddReader(Node readerHead, Node node)
        {
            // 获取到读链尾
            Node reader = readerHead;
            while (reader.nextReader != null)
            {
                reader = reader.nextReader;
            }
            // 读链头的前驱节点也是读链中任何节点的前驱节点
            node.prev = reader.prev;
            // 连接到读链尾
            reader.nextReader = node;
            node.readerHead = readerHead;
            // 读链头记录的读链长度+1
            readerHead.readerCount++;
        }
        // 添加读者到队列尾中
        public void AddReaderNode(Node reader)
        {
            // 添加到队列尾，设置为等待状态
            tail.next = reader;
            reader.prev = tail;
            tail = reader;
            reader.waitStatus = Node.WAITING;
            // 循环检测当前节点是否可以唤醒
            while (reader.prev != head && reader.waitStatus == Node.SIGNAL) { }
            // 若当前节点可以被唤醒，则跳出循环即可，读无需加锁
        }
        // 释放读锁
        public void ReadUnlock()
        {
            lock (this)
            {
                // 读重入量-1
                reentrants--;
                // 获取到锁持有节点
                Node node = head.next;
                while (node != null && node.waitStatus == Node.CANCELLED)
                {
                    node = node.next;
                    head.next = node;
                    if (node != null)
                        node.prev = head;
                }
                // 获取到有效读节点了
                if (node != null)
                {
                    // 重入量为0，需要判断读链是否已全部读完成
                    if (reentrants == 0)
                    {
                        // 当前节点置为取消，读链长度-1
                        node.waitStatus = Node.CANCELLED;
                        node.readerHead.readerCount--;
                        // 读链长度为0，说明已全部完成，则唤醒后续节点
                        if (node.readerHead.readerCount == 0)
                        {
                            AwakeNext();
                        }
                    }
                }
            }
        }
    }
}
