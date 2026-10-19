namespace RoomDemo
{
    /// <summary>
    /// 游戏流程状态（策划案 1.1）：开场独白 → 自由探索 → 结算。
    ///
    /// 注意：放在命名空间层级，不再是 GameManager 的嵌套枚举——
    /// 契约层（RoomDemo.Contracts.IGameFlow）要从子命名空间引用它，
    /// 嵌套类型在子命名空间里不可见。
    /// </summary>
    public enum GameFlowState { Opening, Exploring, Settlement }
}
