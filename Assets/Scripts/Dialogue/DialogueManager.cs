using System;
using RoomDemo.Contracts;
using RoomDemo.Input;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>一行对白。</summary>
    [Serializable]
    public class DialogueLine
    {
        [Tooltip("台词正文")]
        [TextArea(2, 4)]
        public string text = "";

        [Tooltip("本行无按键时多少秒后自动推进（兜底，保证不卡流程）。<=0 表示必须按键推进")]
        public float autoAdvance = 3f;
    }

    /// <summary>
    /// 对话系统（策划案 1.5）：队列播放对白。
    ///
    /// - Play(lines) 播一段；Confirm（空格/回车）推进下一行，Cancel（Esc/右键）跳过整段；
    /// - 每行有 autoAdvance 秒自动推进兜底（D4：不按键也不会卡住流程）；
    /// - 播放期间压入 InputManager 的 "dialogue" 上下文：移动/交互/调试键全锁，
    ///   只放行 Confirm/Cancel（门禁豁免见 InputManager.DialogueContext）；
    /// - 事件：LineChanged（UI 订阅刷新，参数 null = 播放结束）、
    ///   Finished（整段播完或跳过时触发一次）。
    ///
    /// 订阅用惰性模式（Instance 可能比消费方晚就绪），与 GameManager 同一套时序兜底。
    /// </summary>
    public class DialogueManager : MonoBehaviour, IDialogueService
    {
        public static DialogueManager Instance { get; private set; }

        [Header("当前队列")]
        [Tooltip("运行时可读。要播放请调 Play()，不要直接改")]
        public DialogueLine[] lines = new DialogueLine[0];

        /// <summary>是否正在播放对白。</summary>
        public bool IsPlaying => _playing;

        /// <summary>当前行序号（0 起）。未播放时为 -1。</summary>
        public int CurrentIndex => _index;

        /// <summary>当前行。未播放时为 null。</summary>
        public DialogueLine Current =>
            _playing && lines != null && _index >= 0 && _index < lines.Length ? lines[_index] : null;

        /// <summary>切行时触发，参数为新行；null 表示播放结束（UI 据此隐藏对话框）。</summary>
        public event Action<DialogueLine> LineChanged;

        /// <summary>整段队列播完或被跳过时触发一次。</summary>
        public event Action Finished;

        bool _playing;
        int _index = -1;
        float _lineTimer;
        bool _inputSubscribed;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            var im = InputManager.Instance;
            if (im != null)
            {
                im.Confirm -= Advance;
                im.CancelPressed -= SkipAll;
            }
            _inputSubscribed = false;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            EnsureInputSubscribed();
            EnsureContext(); // 兜底：Play 时 InputManager 未就绪会漏锁，这里补

            if (!_playing) return;
            var line = Current;
            if (line == null) { Stop(); return; }

            _lineTimer += Time.deltaTime;
            if (line.autoAdvance > 0f && _lineTimer >= line.autoAdvance)
                Advance();
        }

        void EnsureInputSubscribed()
        {
            if (_inputSubscribed) return;
            var im = InputManager.Instance;
            if (im == null) return;
            im.Confirm -= Advance; im.Confirm += Advance;
            im.CancelPressed -= SkipAll; im.CancelPressed += SkipAll;
            _inputSubscribed = true;
        }

        /// <summary>
        /// 兜底补锁：播放中若上下文没压上（Play 时 InputManager.Instance 还是 null），
        /// 就绪后补压。已经被别的上下文阻塞时不抢，避免误开豁免。
        /// </summary>
        void EnsureContext()
        {
            if (!_playing) return;
            var im = InputManager.Instance;
            if (im != null && !im.IsBlocked) im.PushContext(InputManager.DialogueContext);
        }

        /// <summary>播放一段对白，替换当前队列。空数组视为立即播完。</summary>
        public void Play(DialogueLine[] newLines)
        {
            lines = newLines ?? new DialogueLine[0];
            _index = -1;
            _playing = true;
            InputManager.Instance?.PushContext(InputManager.DialogueContext);
            NextLine();
        }

        /// <summary>推进到下一行；已是最后一行则结束整段。</summary>
        public void Advance()
        {
            if (!_playing) return;
            NextLine();
        }

        /// <summary>跳过整段（E3）。未播放时无操作。</summary>
        public void SkipAll()
        {
            if (!_playing) return;
            Stop();
        }

        void NextLine()
        {
            _index++;
            _lineTimer = 0f;
            if (lines == null || _index >= lines.Length)
            {
                Stop();
                return;
            }
            LineChanged?.Invoke(lines[_index]);
        }

        /// <summary>
        /// 停止播放并释放上下文。可安全重复调用（GameManager.EnterOpening 会先调它
        /// 清掉上一局残留；没在播放时不会触发 Finished）。
        /// </summary>
        /// <summary>
        /// 重派当前行（契约方法 IDialogueService.ReplayCurrent）。
        /// 供迟到的 UI 订阅者补上错过的行（P6：开场首句）——不重置计时、不推进，
        /// 纯粹补派一次 LineChanged。未播放时无操作。
        /// </summary>
        public void ReplayCurrent()
        {
            if (!_playing) return;
            var line = Current;
            if (line != null) LineChanged?.Invoke(line);
        }

        public void Stop()
        {
            bool wasPlaying = _playing;
            _playing = false;
            _index = -1;
            _lineTimer = 0f;
            InputManager.Instance?.PopContext(InputManager.DialogueContext);
            if (!wasPlaying) return;
            LineChanged?.Invoke(null);
            Finished?.Invoke();
        }
    }
}
