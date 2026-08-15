using System;
using Cysharp.Threading.Tasks;
using R3;
using TinySpire.Battle;
using VContainer;
using VContainer.Unity;

namespace TinySpire.Run
{
    /// <summary>由单个 Battle child Scope 持有并释放的 BattleResult 到 Run 唯一 bridge。</summary>
    public sealed class BattleResultRunBridge : IInitializable, IDisposable
    {
        private readonly ReadOnlyReactiveProperty<BattleResult> _results;
        private readonly RunFlowService _flow;
        private readonly RunBattleId _battleId;

        private IDisposable _subscription;
        private bool _forwarded;
        private bool _disposed;

        /// <summary>绑定生产 Queue 的稳定 Result 流与当前 BattleScope 冻结参数。</summary>
        public BattleResultRunBridge(
            BattleCommandQueue queue,
            BattleSetupOptions setup,
            IObjectResolver resolver)
            : this(
                queue?.Result ?? throw new ArgumentNullException(nameof(queue)),
                setup,
                resolver)
        {
        }

        /// <summary>在存在 RunFlow 的父 Scope 中绑定 attempt；legacy/debug Battle 保持空操作。</summary>
        internal BattleResultRunBridge(
            ReadOnlyReactiveProperty<BattleResult> results,
            BattleSetupOptions setup,
            IObjectResolver resolver)
        {
            _results = results ?? throw new ArgumentNullException(nameof(results));
            if (setup == null)
                throw new ArgumentNullException(nameof(setup));
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            if (!resolver.TryResolve(out RunFlowService flow) ||
                !flow.HasActiveBattleInput)
                return;

            _flow = flow;
            _battleId = _flow.BindBattleAttempt(setup);
        }

        /// <summary>绑定显式只读 Result 流，供 EditMode 验证订阅生命周期。</summary>
        internal BattleResultRunBridge(
            ReadOnlyReactiveProperty<BattleResult> results,
            RunFlowService flow,
            BattleSetupOptions setup)
        {
            _results = results ?? throw new ArgumentNullException(nameof(results));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _battleId = _flow.BindBattleAttempt(setup);
        }

        /// <summary>订阅当前 BattleScope 的稳定 Result；初始空值不会触发结算。</summary>
        public void Initialize()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BattleResultRunBridge));
            if (_subscription != null)
                throw new InvalidOperationException("Battle result bridge is already initialized.");
            if (_flow == null)
                return;

            _subscription = _results.Subscribe(HandleResult);
        }

        /// <summary>首次非空结果交给 RunFlow 编排，后续值保持忽略。</summary>
        private void HandleResult(BattleResult result)
        {
            if (result == null || _forwarded || _disposed)
                return;

            _forwarded = true;
            _flow.HandleBattleResultAsync(_battleId, result).Forget();
        }

        /// <summary>随 Battle child Scope 解除旧 Result 订阅，阻止卸载后回调。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}
