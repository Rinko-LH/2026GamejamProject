using RoomDemo.Contracts;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RoomDemo.UI
{
    /// <summary>
    /// 任务提示（S1，策划案 1.6）：订阅 Services.Progress.QuestCompleted，
    /// 把已完成的支线列在左下角，给玩家"今天还差什么"的进展感。
    /// </summary>
    public class TaskHints : MonoBehaviour
    {
        Text _label;
        readonly List<string> _done = new List<string>();
        bool _subscribed;

        void Start() => Build();

        void Build()
        {
            if (_label != null) return;
            _label = UIBuild.Text(transform, "Label", 18, TextAnchor.LowerLeft,
                new Color(0.8f, 0.95f, 1f), new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(560f, 220f));
            _label.text = "支线进展：\n（暂无）";
        }

        void Update()
        {
            Build();
            if (_subscribed) return;
            var p = Services.Progress;
            if (p == null) return;
            p.QuestCompleted -= OnQuest;
            p.QuestCompleted += OnQuest;
            _subscribed = true;
        }

        void OnDestroy()
        {
            if (Services.Progress != null) Services.Progress.QuestCompleted -= OnQuest;
            _subscribed = false;
        }

        void OnQuest(QuestCompletedEvent e)
        {
            var id = e.Id.ToString();
            // 同一支线只列一次（猫打乱一局可触发多次，面板去重显示）
            if (_done.Contains(id)) return;
            _done.Add(id);
            _label.text = "支线进展：\n· " + string.Join("\n· ", _done);
        }
    }
}
