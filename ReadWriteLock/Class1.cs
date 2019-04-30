/**
 * 覆盖ToString方法，格式化输出当前队列情况
 */
public override string ToString()
{
    StringBuilder sb = new StringBuilder();
    if (head != null)
    {
        if (owner != null)
            sb.Append("Head(持有线程：" + owner.Name + "，重入量：" + reentrants + ")\n");
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