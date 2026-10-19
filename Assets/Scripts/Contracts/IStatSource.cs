using System;

namespace RoomDemo.Contracts
{
    /// <summary>
    /// 状态源契约（策划案 1.3）：PlayerStats 实现。
    ///
    /// 命名注意：成员用 WaterValue/SatietyValue/SpiritValue 而不是 Water/Satiety/Spirit——
    /// PlayerStats 已有同名字段（StatChannel Water），同名属性编译冲突。
    /// StatKind/DialogueLine/StatChannel 等类型直接复用父命名空间 RoomDemo 的
    /// （Contracts 不重复定义，避免两套枚举撞名）。
    ///
    /// Add 是支线恢复状态的唯一入口；IsLowMood/AllCritical 供低落机制（5.4）与
    /// 结局判定（5.2）查询。
    /// </summary>
    public interface IStatSource
    {
        float WaterValue { get; }
        float SatietyValue { get; }
        float SpiritValue { get; }

        /// <summary>任一低于 20%（低落机制触发线）。</summary>
        bool IsLowMood { get; }

        /// <summary>按该状态自身的阈值判断是否过低，供 UI 单独显示警告。</summary>
        bool IsStatLow(StatKind kind);

        /// <summary>三项全部低于 10%（低落结局线）。</summary>
        bool AllCritical { get; }

        /// <summary>任一值变化时触发，参数是被改动项。</summary>
        event Action<StatChannel> AnyChanged;

        /// <summary>跨过低落阈值时触发（进入或脱离），参数 true=进入低落，由实现方派发。</summary>
        event Action<bool> LowMoodChanged;

        /// <summary>恢复/扣减状态。支线（早餐/收拾/接水）一律走这里，禁止直接改核心域字段。</summary>
        void Add(StatKind kind, float delta);
    }
}
