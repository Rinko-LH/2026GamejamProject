using RoomDemo.Contracts;
using UnityEngine;

namespace RoomDemo.World
{
    /// <summary>
    /// 猫（策划案 2.4，S2）：色块占位，Idle/走动/打乱三态。
    ///
    /// 铁律合规：
    /// - 打乱<b>不直接改</b>任何道具/画板状态（R1 跨域禁写），只经
    ///   <c>Services.Progress.Report(QuestId.CatMischief)</c> 上报；
    ///   收拾系统（S3 TidySpot）监听该计数变化自行重置归位状态，
    ///   World 域与 Quest 域零直接引用。
    /// - 有本局状态（打乱计数）→ 实现 <see cref="IResettable"/>，
    ///   R 重开由 GameManager 统一遍历重置（R4）。
    ///
    /// 防恶心设计（用户拍板 D12）：打回后 10s CD、每局上限 3 次、
    /// 对白播放中屏蔽（对白期输入锁定，K10）。
    ///
    /// 说明：猫挂在 RoomWorld 下，进画板特写时随房间整体
    /// SetActive(false) 一起隐藏，Update 一并冻结——特写里没有猫
    /// 反而更干净，退出特写自动恢复。
    /// </summary>
    public class CatController : MonoBehaviour, IResettable
    {
        enum State { Idle, Wander, Mischief }

        [Header("游走")]
        [Tooltip("以初始位置为窝，游走半径")]
        public float wanderRadius = 1.2f;

        [Tooltip("游走速度（单位/秒）")]
        public float moveSpeed = 1.1f;

        [Header("打乱节奏")]
        [Tooltip("两次打乱的最小间隔（打回后 CD，秒）")]
        public float minInterval = 10f;

        [Tooltip("两次打乱的最大间隔（秒）")]
        public float maxInterval = 20f;

        [Tooltip("每局打乱次数上限（数值案）")]
        public int maxMischiefPerRun = 3;

        [Header("表现")]
        [Tooltip("打乱冲刺时长（秒）")]
        public float mischiefDashTime = 0.5f;

        [Tooltip("打乱冲刺速度倍率")]
        public float mischiefSpeedScale = 2.5f;

        State _state = State.Idle;
        float _stateTimer;
        float _nextMischiefTime;
        int _mischiefCount;
        Vector3 _wanderTarget;

        Vector3 _homePos;
        Quaternion _homeRot;
        Vector3 _homeScale;

        void Awake()
        {
            _homePos = transform.position;
            _homeRot = transform.rotation;
            _homeScale = transform.localScale;
            // 首次开局也要有 CD：不初始化的话 _nextMischiefTime=0，
            // Time.time>=0 一进场就立刻打乱。
            ResetForNewRun();
        }

        void OnEnable()
        {
            // 从隐藏（特写期）恢复时重置待机计时，避免一出现就接着打乱
            if (_stateTimer <= 0f) _stateTimer = Random.Range(1f, 3f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 屏蔽：对白播放中（输入锁定期）猫也安静待着
            var dialogue = Services.Dialogue;
            bool locked = dialogue != null && dialogue.IsPlaying;
            if (locked) return;

            _stateTimer -= dt;
            switch (_state)
            {
                case State.Idle:
                    if (_stateTimer <= 0f) StartWander();
                    break;
                case State.Wander:
                    MoveToward(_wanderTarget, dt);
                    if (_stateTimer <= 0f || HasArrived()) EnterIdle();
                    break;
                case State.Mischief:
                    MoveToward(_wanderTarget, dt * mischiefSpeedScale);
                    PulseScale();
                    if (_stateTimer <= 0f) FinishMischief();
                    break;
            }

            // 到点且没超上限才打；_Idle 门槛防止 Mischief 状态下重复触发
            if (_state == State.Idle &&
                _mischiefCount < maxMischiefPerRun &&
                Time.time >= _nextMischiefTime)
            {
                StartMischief();
            }
        }

        void StartWander()
        {
            _state = State.Wander;
            _stateTimer = Random.Range(2f, 4f);
            var offset = Random.insideUnitCircle * wanderRadius;
            _wanderTarget = _homePos + new Vector3(offset.x, offset.y * 0.5f, 0f);
        }

        void StartMischief()
        {
            _state = State.Mischief;
            _stateTimer = mischiefDashTime;
            // 在窝旁边随便挑个点冲刺过去，看着像扒拉了一下什么东西。
            // 具体弄乱谁由收拾系统收到上报后自行决定（猫不知道有哪些物品）。
            _wanderTarget = _homePos + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.3f, 0.3f), 0f);
        }

        void FinishMischief()
        {
            transform.localScale = _homeScale;
            _state = State.Idle;
            _stateTimer = Random.Range(2f, 5f);
            _mischiefCount++;
            // 只上报，不碰任何对象（R1）
            Services.Progress?.Report(QuestId.CatMischief);
            _nextMischiefTime = Time.time + Random.Range(minInterval, maxInterval);
        }

        void EnterIdle()
        {
            transform.localScale = _homeScale;
            _state = State.Idle;
            _stateTimer = Random.Range(2f, 5f);
        }

        void MoveToward(Vector3 target, float dt)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * dt);
        }

        bool HasArrived()
        {
            return (_wanderTarget - transform.position).sqrMagnitude < 0.0025f;
        }

        /// <summary>打乱冲刺时轻微挤压拉伸，占位期也能看出"在干坏事"。</summary>
        void PulseScale()
        {
            float k = 1f + 0.2f * Mathf.Sin(Time.time * 25f);
            transform.localScale = new Vector3(_homeScale.x * k, _homeScale.y / k, _homeScale.z);
        }

        /// <summary>R 重开：次数清零、回窝、复位计时与表现态。</summary>
        public void ResetForNewRun()
        {
            _mischiefCount = 0;
            _state = State.Idle;
            _stateTimer = Random.Range(1f, 3f);
            _nextMischiefTime = Time.time + Random.Range(minInterval, maxInterval);
            transform.SetPositionAndRotation(_homePos, _homeRot);
            transform.localScale = _homeScale;
        }
    }
}
