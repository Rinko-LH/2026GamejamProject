using RoomDemo.Contracts;
using RoomDemo.Input;
using UnityEngine;

namespace RoomDemo.Quest
{
    /// <summary>
    /// 接水特写（S4，策划案 2.2）：走到水源按 E 进入独立特写，按住鼠标接水，
    /// 水满自动完成：+20 水分、扣 15 游戏分钟（=7.5 真实秒）、上报 Water、播台词。
    /// Esc 退出回客厅。
    ///
    /// 公共部分（隐藏客厅 / 相机 / 输入订阅 / 总闸门 / Esc / IResettable）在基类
    /// <see cref="CloseupBase"/>，这里只留接水自己的逻辑。
    /// </summary>
    public class WaterCloseup : RoomDemo.CloseupBase
    {
        [Header("水位视觉")]
        [Tooltip("水柱（用 localScale.y 表现水位，底边对齐）")]
        public Transform waterFill;
        [Tooltip("满水时水柱高度（世界单位）")]
        public float fillMaxHeight = 3.6f;
        [Tooltip("水柱底边所在的局部 y（用于底边对齐缩放）")]
        public float fillBottomY = -2.2f;

        [Header("接水")]
        [Tooltip("按住从空到满需要的秒数")]
        public float fillSeconds = 2.5f;

        [Header("结算")]
        public PlayerStats stats;
        public DayTimer timer;
        [Tooltip("接满恢复多少点（数值案 +20 水分）")]
        public float amount = 20f;
        [Tooltip("接满消耗的游戏分钟（15 游戏分钟 = 7.5 真实秒）")]
        public float costMinutes = 15f;
        public DialogueLine[] lines = new DialogueLine[0];

        float _level;
        bool _done;

        public float Level => _level;
        public bool IsDone => _done;

        protected override void OnEntered()
        {
            _level = 0f;
            _done = false;
            ApplyVisual();
        }

        protected override void OnCloseupUpdate()
        {
            if (_done) return;

            // 按住鼠标接水（与画板共用 PaintHeld）
            var im = InputManager.Instance;
            if (im == null || !im.PaintHeld) return;

            _level += Time.deltaTime / Mathf.Max(0.01f, fillSeconds);
            if (_level >= 1f)
            {
                _level = 1f;
                Complete();
            }
            ApplyVisual();
        }

        /// <summary>接满：状态恢复 / 扣时 / 上报 / 台词，全部走契约层。</summary>
        void Complete()
        {
            _done = true;

            var sink = Services.Stats ?? (IStatSource)stats;
            sink.Add(StatKind.Water, amount);

            if (timer != null) timer.SpendGameMinutes(costMinutes);

            Services.Progress?.Report(QuestId.Water);

            if (lines != null && lines.Length > 0) Services.Dialogue?.Play(lines);
        }

        void ApplyVisual()
        {
            if (waterFill == null) return;
            float h = Mathf.Max(0.001f, _level * fillMaxHeight);
            waterFill.localScale = new Vector3(waterFill.localScale.x, h, waterFill.localScale.z);
            // 底边固定：中心 = 底边 y + 半个高度
            waterFill.localPosition = new Vector3(
                waterFill.localPosition.x,
                fillBottomY + h * 0.5f,
                waterFill.localPosition.z);
        }

        public override void ResetForNewRun()
        {
            base.ResetForNewRun();
            _level = 0f;
            _done = false;
            ApplyVisual();
        }
    }
}
