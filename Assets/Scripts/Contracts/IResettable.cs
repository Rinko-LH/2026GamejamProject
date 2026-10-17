namespace RoomDemo.Contracts
{
    /// <summary>
    /// 可重置契约：开新局（R / ResetRun）时统一复位。
    /// GameManager.ResetRun 遍历场景内所有实现者调用——新系统只要实现本接口，
    /// 重置自动化，不会再出现"道具吃掉不复活、画笔拿走不归还"这类残留
    /// （第 2 步回归时这两个 bug 是手工恢复的）。
    /// 注意：可能挂在未激活物体上，查找时要 includeInactive。
    /// </summary>
    public interface IResettable
    {
        void ResetForNewRun();
    }
}
