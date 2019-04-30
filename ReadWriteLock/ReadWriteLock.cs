using System;
using System.Text;
using System.Threading;

namespace ReadWriteLock
{
    public class ReadWriteLock
    {
        // 头节点
        private volatile Node head;
        // 尾节点
        private volatile Node tail;
        // 当前持有锁的线程
        private volatile Thread owner;

        /**
         * 重入量：
         *      对于写锁（独占锁）来说，当一个写线程获取锁时，reentrants为1，后续每当锁重入一次，reentrants增加1；释放锁时，
         *  每释放一次reentrants减少1，直到reentrants为0时该线程释放当前锁，唤醒后续线程；
         *      对于读锁（共享锁）来说，由于读链中可能会有不超过Node.Threshold个数的读节点，且每个读节点都可能会产生重入，
         *  这里会将reentrants初始化为读链长度，在Node.RUNNING时读链每增加一个读节点会增加一个reentrants，读链中的每个节点
         *  多一次重入也会导致reentrants增加1。
         */
        private volatile int reentrants;

        public ReadWriteLock()
        {
        }

        /**
         * 获取写锁
         */
        public void WriteLock()
        {
            // 当前节点
            Node currentNode = null;
            // 这里使用了惰性初始化，如果头节点为空，则初始化头节点
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
                }
            }
            lock (this)
            {
                // 如果当前线程就是持有线程，说明锁在重入，reentrants加1
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
                    // 由于可能出现其他线程的介入，需要再次检测为节点是否为空
                    if (t == null)
                    {
                        EnqWhenTailNull(Node.EXCLUSIVE);
                        return;
                    }
                    // 否则直接入队列等待
                    else
                        currentNode = Enq(Node.EXCLUSIVE);
                }
            }
            // 检测当前节点是否可以被唤醒
            while (currentNode.waitStatus != Node.SIGNAL){}
            // 此时线程已被唤醒，设置重入量及状态等
            reentrants = 1;
            currentNode.waitStatus = Node.RUNNING;
            owner = Thread.CurrentThread;
        }
        /**
         * 释放写锁
         */
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
        /**
         * 获取读锁
         */
        public void ReadLock()
        {
            Node reader = null;
            // 这里基本的初始化方式和读锁相似，不赘述
            lock (this)
            {
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
            lock (this)
            {
                // 读可重入
                if (ReaderCanReentranted())
                {
                    reentrants += 1;
                    return;
                }
                // 获取当前持有锁节点
                Node node = head.next;
                if (node == null)
                {
                    EnqWhenTailNull(Node.SHARED);
                    return;
                }
                else
                {
                    // 队列中有写者的情况
                    if (HasWriter())
                    {
                        // 创建当前持有当前线程的读节点
                        reader = new Node(current, Node.SHARED);
                        // 获取第一个读节点（可能为null）
                        while (node != null && (!node.isShared() || node.waitStatus == Node.CANCELLED))
                        {
                            if (node.isShared())
                            {
                                if (!CheckReadChainCancelled(node))
                                    break;
                                else
                                    node = node.next;
                            }
                            else
                                node = node.next;
                        }
                        // 未找到读节点，说明队列中只有写节点，此时直接添加到队列尾
                        if (node == null)
                        {
                            currentNode = Enq(Node.SHARED);
                        }
                        // 否则说明找到了有效的读节点，此时此读节点为队列中第一个读节点，需要判断此读节点链长是否达到了阈值，
                        //  若未超过阈值，直接添加到读链中，并判断当前读链头是否正在读，则可以直接读，否则需要循环检测；
                        //  若超过了阈值：
                        //      若队尾为写节点，则添加到队列尾，并循环等待；
                        //      若队尾为读节点，判断是否到达阈值：
                        //          若是则添加到队列尾，并循环等待；
                        //          否则添加到读链中。
                        else
                        {
                            // 未超过阈值
                            if (node.readerCount < Node.Threshold)
                            {
                                // 添加到读链中
                                currentNode = AddReader(node, reader);
                                // 链头正在读，此节点也直接读，且reentrants+1
                                if (node.waitStatus == Node.RUNNING)
                                {
                                    reader.waitStatus = Node.RUNNING;
                                    reentrants += 1;
                                    return;
                                }
                            }
                            // 超过了阈值
                            else
                            {
                                // 判断队尾节点类型
                                Node t = tail;
                                // 队尾为“写节点”或“达到阈值的读节点”
                                if (!t.isShared() || (t.isShared() && t.readerCount >= Node.Threshold))
                                    currentNode = Enq(Node.SHARED);
                                // 队尾为“未达到阈值的读节点”
                                else
                                {
                                    // 添加到队尾节点所在的读链中
                                    currentNode = AddReader(t, reader);
                                    // 链头正在读，此节点也直接读，且reentrants+1
                                    if (t.waitStatus == Node.RUNNING)
                                    {
                                        reader.waitStatus = Node.RUNNING;
                                        reentrants += 1;
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    // 队列中无写者，说明队列中要么为空，要么全为读者，直接加到队尾或者队尾所在的读链
                    else
                    {
                        // 队列为空，直接入队
                        if (tail == null)
                        {
                            EnqWhenTailNull(Node.SHARED);
                            return;
                        }
                        // 队尾节点读链长度未达到阈值
                        if (tail.readerCount < Node.Threshold)
                        {
                            reader = new Node(current, Node.SHARED);
                            // 添加到链尾
                            currentNode = AddReader(tail, reader);
                            // 是否需要循环等待
                            if (tail.waitStatus == Node.RUNNING)
                            {
                                reader.waitStatus = Node.RUNNING;
                                reentrants += 1;
                                return;
                            }
                        }
                        // 队尾节点读链达到了阈值，直接加入队尾，并等待唤醒
                        else
                        {
                            currentNode = Enq(Node.SHARED);
                        }
                    }
                }
            }
            while (currentNode.waitStatus != Node.SIGNAL) { }
            currentNode.waitStatus = Node.RUNNING;
            reentrants = currentNode.readerHead.readerCount;
            owner = currentNode.readerHead == null ? null : currentNode.readerHead.thread;
        }
        /**
         * 释放读锁
         */
        public void ReadUnlock()
        {
            lock (this)
            {
                // 读重入量-1
                reentrants -= 1;
                // 获取到锁持有节点
                Node node = head.next;
                while (node != null)
                {
                    if (CheckReadChainCancelled(node))
                    {
                        node = node.next;
                        head.next = node;
                        if (node != null)
                            node.prev = head;
                    }
                    else
                        break;
                }
                // 获取到有效读节点了
                if (node != null)
                {
                    // 通过一个读节点（通常是链头）获取其读链中持有当前运行线程的节点
                    node = GetCurrentNodeByReader(node);
                    // 当前节点置为取消
                    node.waitStatus = Node.CANCELLED;
                    // 重入量为0，需要判断读链是否已全部读完成，若是则需要唤醒后续线程
                    if (reentrants == 0)
                        AwakeNext();
                }
            }
        }
        /**
         * 唤醒后一个节点
         */
        private void AwakeNext()
        {
            // 获取持有当前锁的节点，此时锁刚好被上个节点释放，获取的节点应处于Node.WAITING状态
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
                // 对于写节点，readerCount为1，即为reentrants初始值1
                // 对于读节点，readerCount即为reentrants的初始值
                reentrants = node.readerCount;
                // 对于读节点来说，需唤醒读链中的所有节点；对于写节点来说，无读链，只会唤醒当前节点
                while (node != null)
                {
                    // 等待类型设置为SIGNAL，会被捕捉从而唤醒相关线程
                    node.waitStatus = Node.SIGNAL;
                    // 若为读节点，可获取下一个读者；否则获取了null
                    node = node.nextReader;
                    // 下一个读者不为空，则也将其前驱节点设置为头节点
                    if (node != null)
                        node.prev = head;
                }
            }
        }
        /** 
         *   删除无效节点（可能由于主动中断或者其他因素导致的线程失效），获取第一个有效节点或空节点，
         * 或者称作获取锁持有节点
         */
        private Node GetHolderNode()
        {
            Node node = head.next;
            // 此判断可能出现歧义，因存在读链头为取消状态时，读链中仍然有读节点读未完成，但是此函数只在释放写锁(WriteUnlock)
            //和唤醒后续节点(AwakeNext)中使用到，当AwakeNext被触发时，读链头的状态便可以代表整个读链的状态了。
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
        /**
         * 返回当前队列中是否存在写者，用在读者入队列时的决策
         */
        private bool HasWriter()
        {
            bool exclusive = false;
            Node excluNode = head.next;
            while (excluNode != null)
            {
                if (!excluNode.isShared())
                {
                    exclusive = true;
                    break;
                }
                excluNode = excluNode.next;
            }
            return exclusive;
        }
        /**
         * 读写节点入队列操作，入队列后需等待
         */
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
            // 若为读节点，需要设置读链头
            if (node.isShared())
                node.readerHead = node;
            return node;
        }
        /**
         * 读写节点入队列操作，此时队列尾为空，入队列之后继续运行，无需等待
         */
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
            // 若为读节点，需要设置读链头
            if (node.isShared())
                node.readerHead = node;
        }
        /**
         * 通过一个读节点（通常是链头）获取其读链中持有当前运行线程的节点
         */
        private Node GetCurrentNodeByReader(Node node)
        {
            Node nextReader = node.readerHead;
            // 以线程名称为依据，这里可能出现多个线程同名的情况，待改进
            while (nextReader != null)
            {
                if (nextReader.thread.Name.Equals(Thread.CurrentThread.Name))
                    return nextReader;
                else
                    nextReader = nextReader.nextReader;
            }
            return null;
        }
        /**
         * 通过一个读节点判断其所在读链是否已全部读完成
         */
        private bool CheckReadChainCancelled(Node node)
        {
            Node nextReader = node.readerHead;
            while (nextReader != null)
            {
                if (nextReader.waitStatus != Node.CANCELLED)
                    return false;
                else
                    nextReader = nextReader.nextReader;
            }
            return true;
        }
        /**
         * 添加读者到指定读链头所在的读链中
         */
        private Node AddReader(Node readerHead, Node node)
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
            return node;
        }
        /**
         * 判断读者是否可以重入，判断依据为当前线程是否被正在执行读的读链中的某一个节点持有
         */
        private bool ReaderCanReentranted()
        {
            Node node = head.next;
            if (node == null || !node.isShared())
                return false;
            else
            {
                Node reader = node;
                while (reader != null)
                {
                    if (reader.thread == Thread.CurrentThread)
                    {
                        return true;
                    }
                    reader = reader.nextReader;
                }
            }
            return false;
        }
        /**
         * 覆盖ToString方法，格式化输出当前队列情况
         */
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (head != null)
            {
                if (owner != null)
                    sb.Append("Head(持有线程："+ owner.Name + "，重入量："+ reentrants + ")\n");
                else
                    sb.Append("Head(持有线程：未持有线程，重入量：" + reentrants + ")\n");
                Node node = head.next;
                while (node != null)
                {
                    Node reader = node;
                    if (node.isShared())
                    {
                        while (reader != null)
                        {
                            sb.AppendFormat("->【线程名称：{0}，类型：读，状态：{1}】", reader.thread.Name, Node.GetStatus(reader.waitStatus));
                            reader = reader.nextReader;
                        }
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendFormat("->【线程名称：{0}，类型：写，状态：{1}】", node.thread.Name, Node.GetStatus(node.waitStatus));
                        sb.AppendLine();
                    }
                    node = node.next;
                }
            }
            return sb.ToString();
        }
        /**
         * 打印当前队列情况
         */
        public void PrintQueue()
        {
            Console.WriteLine(ToString());
        }
        /**
         * 辅助打印方法，调试用
         */
        public void PrintGetOrReleaseLock(bool locked, bool share)
        {
            if (share)
            {
                if (locked)
                    Console.WriteLine(Thread.CurrentThread.Name + ":获取读锁");
                else
                    Console.WriteLine(Thread.CurrentThread.Name + ":释放读锁");
            }
            else
            {
                if (locked)
                    Console.WriteLine(Thread.CurrentThread.Name + ":获取写锁");
                else
                    Console.WriteLine(Thread.CurrentThread.Name + ":释放写锁");
            }
        }
    }
}
