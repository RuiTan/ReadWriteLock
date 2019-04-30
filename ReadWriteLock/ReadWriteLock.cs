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
                if (owner != null)
                    Console.WriteLine("Head(持有线程：{0}，重入量：{1})", owner.Name, reentrants);
                else
                    Console.WriteLine("Head(持有线程：{0}，重入量：{1})", "未持有线程", reentrants);
                Node node = head.next;
                while (node != null)
                {
                    Node reader = node;
                    if (node.isShared())
                    {
                        while (reader != null)
                        {
                            Console.Write("->【线程名称：{0}，类型：读，状态：{1}】", reader.thread.Name, Node.GetStatus(reader.waitStatus));
                            reader = reader.nextReader;
                        }
                        Console.WriteLine();

                    }
                    else
                    {
                        Console.WriteLine("->【线程名称：{0}，类型：写，状态：{1}】", node.thread.Name, Node.GetStatus(node.waitStatus));
                    }
                    node = node.next;
                }
            }
        }

        // 获取写锁
        public void WriteLock()
        {
            Node currentNode = null;
            // 加锁是为了同步修改队列信息
            // 如果头节点为空，则初始化头节点，这里使用了惰性初始化
            lock (this)
            {
                if (head == null)
                {
                    head = new Node();
                }
            }
            // 如果尾节点为空，说明当前队列为空，此时线程直接入队列设置为RUNNING状态
            lock (this)
            {
                if (tail == null)
                {
                    EnqWhenTailNull(Node.EXCLUSIVE);
                    return;
                    //Console.WriteLine(Thread.CurrentThread.Name + " : 获取写锁成功");
                }
            }
            lock (this)
            {
                // 如果当前线程就是持有线程，说明锁在重入，重入量加1
                if (owner == Thread.CurrentThread)
                {
                    reentrants += 1;
                    return;
                }
                // 否则，当前线程需要进入等待队列进行等待
                else
                {
                    // 获取尾节点
                    Node t = tail;
                    if (t == null)
                    {
                        EnqWhenTailNull(Node.EXCLUSIVE);
                        return;
                    }
                    else
                        currentNode = Enq(Node.EXCLUSIVE);
                    //Console.WriteLine(Thread.CurrentThread.Name + " : 获取写锁成功");
                }
            }
            // 循环检测当前节点是否可以被唤醒
            while (currentNode.waitStatus != Node.SIGNAL)
            {
            }
            reentrants = 1;
            owner = Thread.CurrentThread;
        }
        private Node Enq(Node mode)
        {
            Node t = tail; 
            // 以当前线程创建独占节点
            Node node = new Node(Thread.CurrentThread, mode);
            // 连接尾节点与当前节点
            t.next = node;
            node.prev = t;
            // 重置尾节点为当前节点
            tail = node;
            // 设置节点等待状态为WAITING
            node.waitStatus = Node.WAITING;
            return node;
        }
        private void EnqWhenTailNull(Node mode)
        {
            // 以当前线程创建独占节点
            Node node = new Node(Thread.CurrentThread, mode);
            // 设置等待类型为RUNNING
            node.waitStatus = Node.RUNNING;
            // 当前节点加入队列，尾节点为当前节点
            tail = node;
            // 设置锁的持有者为当前线程
            owner = Thread.CurrentThread;
            // 重入量初始为1
            reentrants = 1;
            // 连接头节点与此节点
            head.next = node;
            node.prev = head;
            if (mode == Node.SHARED)
                node.readerHead = node;
        }

        // 释放写锁
        public void WriteUnlock()
        {
            // 加锁是为了同步修改队列信息
            lock (this)
            {
                // 当前锁持有者重入量直接-1
                reentrants -= 1;
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
                        // 唤醒后继有效节点
                        AwakeNext();
                    }
                }
            }
        }

        public void AwakeNext()
        {
            Node node = GetHolderNode();
            // 当前节点为空，说明队列中无有效节点，直接返回即可
            if (node == null)
            {
                tail = null;
                owner = null;
                reentrants = 0;
                return;
            }
            // 否则需要唤醒此有效节点
            else
            {
                // 连接到头节点
                node.prev = head;
                reentrants = node.readerCount;
                // 对于读节点来说，需唤醒读链中的所有节点；对于写节点来说，无读链，只会唤醒当前节点
                while (node != null)
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
                if (head == null)
                {
                    head = new Node();
                }
            }
            Thread current = Thread.CurrentThread;
            lock (this)
            {
                if (tail == null)
                {
                    EnqWhenTailNull(Node.SHARED);
                    return;
                }
            }
            Node currentNode = null;
            lock(this)
            {
                // 获取当前持有锁节点
                Node node = head.next;
                if (node == null)
                {
                    EnqWhenTailNull(Node.SHARED);
                    return;
                }
                //// 添加重入量
                //if (node.readerHead != null && owner == node.readerHead.thread)
                //{
                //    reentrants += 1;
                //    return;
                //}
                else
                {
                    bool exclusive = false;
                    Node excluNode = head.next;
                    while (excluNode != null)
                    {
                        if (excluNode.mode == Node.EXCLUSIVE)
                        {
                            exclusive = true;
                            break;
                        }
                        excluNode = excluNode.next;
                    }
                    if (exclusive)
                    {
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
                            currentNode = Enq(Node.SHARED);
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
                                currentNode = AddReader(node, reader);
                                // 是否需要循环等待
                                if (node.waitStatus == Node.RUNNING)
                                {
                                    reader.waitStatus = Node.RUNNING;
                                    return;
                                }
                            }
                            // 超过了阈值
                            else
                            {
                                currentNode = Enq(Node.SHARED);
                            }
                        }
                    }
                    else
                    {
                        if (tail.readerCount < Node.Threshold)
                        {
                            currentNode = AddReader(tail, reader);
                            // 是否需要循环等待
                            if (node.waitStatus == Node.RUNNING)
                            {
                                reader.waitStatus = Node.RUNNING;
                                return;
                            }
                        }
                        else
                        {
                            currentNode = Enq(Node.SHARED);
                        }
                    }
                }
            }
            while(currentNode.waitStatus != Node.SIGNAL) { }
            reentrants = currentNode.readerCount;
            owner = currentNode.readerHead == null ? null : currentNode.readerHead.thread;
        }
        // 添加读者到指定读链头所在的读链中
        public Node AddReader(Node readerHead, Node node)
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
            node.waitStatus = readerHead.waitStatus;
            // 读链头记录的读链长度+1
            readerHead.readerCount++;
            reentrants++;
            return node;
        }
        // 释放读锁
        public void ReadUnlock()
        {
            lock (this)
            {
                // 读重入量-1
                reentrants -= 1;
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
                    // 当前节点置为取消，读链长度-1
                    node.waitStatus = Node.CANCELLED;
                    // 重入量为0，需要判断读链是否已全部读完成
                    if (reentrants == 0)
                    {
                        //node.readerHead.readerCount--;
                        //// 读链长度为0，说明已全部完成，则唤醒后续节点
                        //if (node.readerHead.readerCount == 0)
                        //{
                        //    AwakeNext();
                        //}
                        AwakeNext();
                    }
                }
            }
        }
    }
}
