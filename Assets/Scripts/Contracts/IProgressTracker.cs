using System;

namespace RoomDemo.Contracts
{
    /// <summary>
    /// 支线任务标识。注意：不含 Painting——画作完成计数走 IGameFlow.PaintingsCompleted，
    /// 避免双轨计数。新增支线在此枚举追加，ProgressTracker/结算自动支持。
    /// </summary>
    public enum QuestId
    {
        None,         // 未指定（可选字段的默认值，不计数）
        Breakfast,    // 3.4 吃早餐
        TidyDesk,     // 3.1 收拾桌面
        TidyShelf,    // 3.2 整理书架
        Water,        // 3.3 接水
        BrushFound,   // 4.1 找到画笔
        CatMischief,  // 2.4 被猫弄乱
        RandomFind,   // 3.5 偶然发现
    }

    /// <summary>一次支线完成事件。</summary>
    public struct QuestCompletedEvent
    {
        public QuestId Id;
        public float GameTimeSeconds;

        public QuestCompletedEvent(QuestId id, float gameTime)
        {
            Id = id;
            GameTimeSeconds = gameTime;
        }
    }

    /// <summary>
    /// 进度总线契约：各支线完成后 Report，UI 任务提示 / 结算统计统一订阅。
    /// 实现为场景单例（ProgressTracker），由 GameManager.Awake 注册进 Services。
    /// </summary>
    public interface IProgressTracker
    {
        event Action<QuestCompletedEvent> QuestCompleted;
        void Report(QuestId id);
        int Count(QuestId id);
        void ResetAll();
    }
}
