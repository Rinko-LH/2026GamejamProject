using RoomDemo;
using UnityEngine;
using UnityEngine.UI;

namespace RoomDemo.UI
{
    /// <summary>
    /// 时钟显示（S1，策划案 1.6）：读 DayTimer，显示游戏内时钟与剩余秒数。
    /// timer 由装配菜单接线；为空时退化到场景查找。
    /// </summary>
    public class ClockDisplay : MonoBehaviour
    {
        public DayTimer timer;
        Text _label;

        void Start()
        {
            Build();
            if (timer == null) timer = FindObjectOfType<DayTimer>();
        }

        void Build()
        {
            if (_label != null) return;
            _label = UIBuild.Text(transform, "Label", 68, TextAnchor.UpperRight, Color.white,
                new Vector2(1f, 1f), new Vector2(-20f, -16f), new Vector2(1040f, 120f));
        }

        void Update()
        {
            Build();
            if (timer == null) return;

            // 结算态时钟让位：结算面板呈现最终局面，顶部再挂个
            // 「剩余 480s/0s」只有干扰（R 重开后显示值也不对）。
            var flow = RoomDemo.Contracts.Services.Flow;
            bool settle = flow != null && flow.State == RoomDemo.GameFlowState.Settlement;
            if (_label.gameObject.activeSelf == settle)
                _label.gameObject.SetActive(!settle);
            if (settle) return;

            _label.text = $"时钟 {timer.ClockText}　剩余 {Mathf.CeilToInt(timer.RemainingSeconds)}s";
        }
    }
}
