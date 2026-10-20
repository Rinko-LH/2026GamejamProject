using RoomDemo.Contracts;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 玩家进入碰撞区时自动播放一段对白（走近即触发，无需按键）。
    /// 台词直接在 Inspector 的 Lines 数组里逐条填写，无需改代码。
    /// 可与 RoomInteractable 子类挂在同一物体上共存，互不影响。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DialogueAutoTrigger : MonoBehaviour
    {
        [Header("对白")]
        [Tooltip("要播放的对白。逐条填写正文，播完一条由玩家按空格/E 推进下一条。")]
        public DialogueLine[] lines;

        [Header("触发")]
        [Tooltip("勾选后只触发一次；不勾则每次重新进入碰撞区都会重播。")]
        public bool once = true;

        [Tooltip("触发对象标签。默认 Player。")]
        public string actorTag = "Player";

        bool _done;

        void Reset()
        {
            var c = GetComponent<Collider2D>();
            if (c != null) c.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_done) return;
            if (other == null || !other.isTrigger) return;
            if (!IsActor(other.transform)) return;

            _done = once;
            if (lines != null && lines.Length > 0)
                Services.Dialogue?.Play(lines);
        }

        bool IsActor(Transform candidate)
        {
            if (string.IsNullOrEmpty(actorTag)) return false;
            for (var current = candidate; current != null; current = current.parent)
                if (current.CompareTag(actorTag)) return true;
            return false;
        }
    }
}
