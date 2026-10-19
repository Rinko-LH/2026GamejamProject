using RoomDemo.Contracts;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 通用道具拾取：零食 / 水等，用于支线补充状态。
    ///
    /// 契约接入（S0）：
    /// - 状态恢复走 <see cref="IStatSource.Add"/>（支线恢复唯一入口），不再直接摸 channel；
    /// - <see cref="reportId"/> 非 None 时完成即上报进度总线（S4 接水 / S5 吃饭接线时填，
    ///   None = 纯测试道具）；
    /// - <see cref="lines"/> 非空时完成时播台词；
    /// - 实现 <see cref="IResettable"/>：R 重开时复活（修复"吃掉不复活"的既有 bug）。
    /// </summary>
    public class ConsumablePickup : RoomInteractable, IResettable
    {
        public PlayerStats stats;

        public enum Effect { Water, Satiety, Spirit }
        public Effect effect = Effect.Satiety;

        [Tooltip("补充多少点")]
        public float amount = 20f;

        [Tooltip("是否要消耗游戏内时间")]
        public bool costTime = true;
        public float costMinutes = 20f;

        public DayTimer timer;

        [Header("支线接入")]
        [Tooltip("完成时汇报给进度总线的支线 id。None = 不计入进度")]
        public QuestId reportId = QuestId.None;

        [Tooltip("完成时播放的台词，可空")]
        public DialogueLine[] lines = new DialogueLine[0];

        public override void Interact()
        {
            if (stats == null) return;

            // 状态恢复走契约入口；Services 未就绪（启动帧）时回退直连，保证不炸
            var sink = Services.Stats ?? (IStatSource)stats;
            var kind = effect switch
            {
                Effect.Water => StatKind.Water,
                Effect.Satiety => StatKind.Satiety,
                _ => StatKind.Spirit
            };
            sink.Add(kind, amount);

            if (costTime && timer != null) timer.SpendGameMinutes(costMinutes);

            if (reportId != QuestId.None) Services.Progress?.Report(reportId);

            if (lines != null && lines.Length > 0)
                Services.Dialogue?.Play(lines);

            canInteract = false;
            gameObject.SetActive(false);
        }

        /// <summary>R 重开时复活（IResettable）。</summary>
        public void ResetForNewRun()
        {
            gameObject.SetActive(true);
            canInteract = true;
        }
    }
}
