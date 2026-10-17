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
            _label = UIBuild.Text(transform, "Label", 26, TextAnchor.UpperRight, Color.white,
                new Vector2(1f, 1f), new Vector2(-20f, -16f), new Vector2(380f, 130f));
        }

        void Update()
        {
            Build();
            if (timer == null) return;
            _label.text = $"时钟 {timer.ClockText}　剩余 {Mathf.CeilToInt(timer.RemainingSeconds)}s";
        }
    }
}
