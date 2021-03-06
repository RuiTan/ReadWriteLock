﻿using System;
using System.Threading;

namespace ReadWriteLock
{
    public class Node
    {
        // 读节点
        public static readonly Node SHARED = new Node();
        // 写节点
        public static readonly Node EXCLUSIVE = null;
        // 读链长度阈值
        public static int Threshold = 3;

        // 节点状态
        public static readonly int CANCELLED = 1;
        public static readonly int RUNNING = -1;
        public static readonly int WAITING = -2;
        public static readonly int SIGNAL = -3;

        public static string GetStatus(int status)
        {
            switch (status)
            {
                case 1:
                    return "CANCELLED";
                case -1:
                    return "RUNNING";
                case -2:
                    return "WAITING";
                case -3:
                    return "SIGNAL";
            }
            return "DEFAULT";
        }
        public int waitStatus;
        // 前驱节点
        public Node prev;
        // 后继节点
        public Node next;
        // 持有线程
        public Thread thread;

        //以下参数仅对读节点适用
        // 读链头
        public Node readerHead;
        // 后继读节点
        public Node nextReader;
        // 读链长度
        public int readerCount = 1;

        // 节点类型
        public Node mode;
        // 是否是共享节点
        public bool isShared()
        {
            return mode == SHARED;
        }
        public Node()
        {
        }

        // 创建共享或独占节点
        public Node(Thread thread, Node mode)
        {
            this.mode = mode;
            this.thread = thread;
        }
    }
}
