using System;

namespace RoomDemo.Contracts
{
    /// <summary>
    /// 流程源契约（策划案 1.1）：GameManager 实现。
    /// FlowState 直接用父命名空间的 GameManager.GameFlowState，不重复定义。
    ///
    /// 结局判定（5.2）与结算（5.3）只读这里 + Services.Stats，不直接碰 GameManager 字段。
    /// PaintingSeconds 是"实际下笔累计时间"（与单幅计时同源、不受低落倍率污染），
    /// 由 DrawingBoardInteract 累计、GameManager 聚合；DayElapsedSeconds 是当局实际时长
    /// （提前结算后即专注占比的分母）。
    /// </summary>
    public interface IGameFlow
    {
        GameFlowState State { get; }
        int PaintingsCompleted { get; }

        /// <summary>本局画作总数（画板配置决定，结算界面显示 n/total 用）。</summary>
        int TotalPaintings { get; }

        float PaintingSeconds { get; }
        float DayElapsedSeconds { get; }

        /// <summary>提前结束一天（三幅画全完成时调用）；时间耗尽走 DayTimer 侧。</summary>
        void EndDay();

        /// <summary>重置当前游戏流程并开始新一局（由流程控制器或明确的重开入口调用）。</summary>
        void ResetRun();

        event Action<GameFlowState, GameFlowState> StateChanged;
    }
}
