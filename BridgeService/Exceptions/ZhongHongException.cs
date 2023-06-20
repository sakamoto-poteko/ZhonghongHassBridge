namespace ZhongHong.BridgeService.Exceptions;

public class ZhongHongException : Exception
{
    public ZhongHongException()
    {
    }

    public ZhongHongException(string message) : base(message)
    {
    }

    public ZhongHongException(string message, Exception inner) : base(message, inner)
    {
    }
}
