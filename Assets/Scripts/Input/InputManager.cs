using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomDemo.Input
{
    /// <summary>
    /// 输入管理（策划案 1.7）：全工程唯一直接读取 Input 的地方。
    /// 玩法脚本一律经此取语义化指令，不自己读 Input。
    ///
    /// - 轮询：Move / PointerScreenPos / PaintHeld（每帧值）；
    /// - 事件：Interact(E) / Confirm(空格·回车) / CancelPressed(Esc·右键) /
    ///   三个调试键（Demo 专用，正式 UI 做好后连订阅一起删）；
    /// - 上下文栈：PushContext 期间（对话播放、UI 面板等）Move 归零、
    ///   边沿动作全部不派发——"播放台词时锁输入"就靠它（第 2 步启用）。
    ///
    /// 注意：Move 是轮询值，消费方在自己的 Update 里读，可能比本帧晚一帧
    /// （Update 执行顺序不定）——位移类可接受；边沿动作走事件，同帧派发无此问题。
    ///
    /// ⚠️ 本文件内 UnityEngine.Input 一律写全名：本命名空间叫 RoomDemo.Input，
    /// 简写 `Input.` 会按名称解析优先命中 RoomDemo.Input 自己（CS0234），
    /// 不要"简化"回简写。
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("按键绑定")]
        public KeyCode interactKey = KeyCode.E;
        public KeyCode confirmKey = KeyCode.Space;
        [Tooltip("确认的备用键")]
        public KeyCode confirmKeyAlt = KeyCode.Return;
        public KeyCode cancelKey = KeyCode.Escape;
        [Tooltip("取消（退出画板特写）的鼠标备用键")]
        public int cancelMouseButton = 1;
        [Tooltip("画画落笔的鼠标键")]
        public int paintMouseButton = 0;

        [Header("调试键（Demo 验收用，正式 UI 做好后连事件一起删）")]
        public KeyCode debugToggleHudKey = KeyCode.F1;
        public KeyCode debugFastForwardKey = KeyCode.T;
        public KeyCode debugRestartKey = KeyCode.R;

        // ---------- 语义化轮询 ----------

        /// <summary>本帧移动向量（WASD/方向键）。被上下文封锁时返回零。</summary>
        public Vector2 Move { get; private set; }

        /// <summary>鼠标屏幕位置（画板特写里转画布坐标用）。</summary>
        public Vector2 PointerScreenPos { get; private set; }

        /// <summary>落笔键是否按住。被上下文封锁时 false。</summary>
        public bool PaintHeld { get; private set; }

        // ---------- 语义化事件（按下沿，同帧派发） ----------

        /// <summary>交互键（E）：拾取、吃喝、走近画板进特写。</summary>
        public event Action Interact;

        /// <summary>确认键（空格/回车）：对话框推进、UI 确认（第 2/3 步启用）。</summary>
        public event Action Confirm;

        /// <summary>取消键（Esc/右键）：退出画板特写。</summary>
        public event Action CancelPressed;

        /// <summary>调试：显示/隐藏 HUD（F1）。</summary>
        public event Action DebugToggleHud;

        /// <summary>调试：快进 60 游戏分钟（T）。</summary>
        public event Action DebugFastForward;

        /// <summary>调试：结算画面重开一局（R）。</summary>
        public event Action DebugRestart;

        // ---------- 上下文栈 ----------

        readonly HashSet<string> _contexts = new HashSet<string>();

        /// <summary>是否有上下文封锁输入（对话/UI 等）。</summary>
        public bool IsBlocked => _contexts.Count > 0;

        /// <summary>压入一个封锁上下文（同名只记一次）。</summary>
        public void PushContext(string name)
        {
            if (!string.IsNullOrEmpty(name)) _contexts.Add(name);
        }

        /// <summary>弹出上下文。</summary>
        public void PopContext(string name)
        {
            if (!string.IsNullOrEmpty(name)) _contexts.Remove(name);
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            // 先快照原始输入，再按上下文决定放行多少
            // （UnityEngine.Input 写全名，原因见类注释：RoomDemo.Input 同名优先）
            float rawX = UnityEngine.Input.GetAxisRaw("Horizontal");
            float rawY = UnityEngine.Input.GetAxisRaw("Vertical");
            PointerScreenPos = UnityEngine.Input.mousePosition;
            bool blocked = IsBlocked;

            Move = blocked ? Vector2.zero : new Vector2(rawX, rawY);
            PaintHeld = blocked ? false : UnityEngine.Input.GetMouseButton(paintMouseButton);

            if (blocked) return; // 封锁期间不派发任何按下沿

            if (UnityEngine.Input.GetKeyDown(interactKey)) Interact?.Invoke();
            if (UnityEngine.Input.GetKeyDown(confirmKey) || UnityEngine.Input.GetKeyDown(confirmKeyAlt)) Confirm?.Invoke();
            if (UnityEngine.Input.GetKeyDown(cancelKey) || UnityEngine.Input.GetMouseButtonDown(cancelMouseButton)) CancelPressed?.Invoke();

            if (UnityEngine.Input.GetKeyDown(debugToggleHudKey)) DebugToggleHud?.Invoke();
            if (UnityEngine.Input.GetKeyDown(debugFastForwardKey)) DebugFastForward?.Invoke();
            if (UnityEngine.Input.GetKeyDown(debugRestartKey)) DebugRestart?.Invoke();
        }
    }
}
