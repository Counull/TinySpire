using System;

namespace TinySpire.Core
{
    /// <summary>
    /// 可跨多个 Tick 持续运行的同步状态。
    /// </summary>
    public interface IState<TEvent>
    {
        void Enter();

        StateTransition<TEvent> Handle(TEvent @event);

        StateTransition<TEvent> Tick(TimeSpan deltaTime);

        void Exit();
    }

    /// <summary>
    /// 状态只需要返回两种结果：保持当前状态，或进入一个新状态。
    /// </summary>
    public readonly struct StateTransition<TEvent>
    {
        private readonly IState<TEvent> _nextState;

        public bool HasNextState => _nextState != null;

        public IState<TEvent> NextState => _nextState;

        private StateTransition(IState<TEvent> nextState)
        {
            _nextState = nextState;
        }

        public static StateTransition<TEvent> Stay => default;

        public static StateTransition<TEvent> To(IState<TEvent> nextState)
        {
            if (nextState == null)
                throw new ArgumentNullException(nameof(nextState));

            return new StateTransition<TEvent>(nextState);
        }
    }

    /// <summary>
    /// 最小同步状态机核心。
    ///
    /// 更新循环和事件队列由调用方负责；本类只持有当前状态并执行状态转换。
    /// </summary>
    public sealed class StateMachine<TEvent>
    {
        private IState<TEvent> _currentState;
        private bool _isStopped;
        private bool _isProcessing;

        public bool IsRunning => !_isStopped;

        public IState<TEvent> CurrentState => _currentState;

        public StateMachine(IState<TEvent> initialState)
        {
            _currentState = initialState ?? throw new ArgumentNullException(nameof(initialState));
            _currentState.Enter();
        }

        public void Tick(TimeSpan deltaTime)
        {
            if (deltaTime < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));

            EnsureRunning();
            BeginProcessing();

            try
            {
                bool firstTick = true;
                while (IsRunning)
                {
                    TimeSpan elapsed = firstTick ? deltaTime : TimeSpan.Zero;
                    firstTick = false;

                    StateTransition<TEvent> transition = _currentState.Tick(elapsed);
                    if (!transition.HasNextState)
                        return;

                    ChangeState(transition.NextState);
                }
            }
            finally
            {
                EndProcessing();
            }
        }

        public void Dispatch(TEvent @event)
        {
            EnsureRunning();
            BeginProcessing();

            try
            {
                StateTransition<TEvent> transition = _currentState.Handle(@event);
                if (transition.HasNextState)
                    ChangeState(transition.NextState);
            }
            finally
            {
                EndProcessing();
            }
        }

        public void Stop()
        {
            if (_isStopped)
                return;

            BeginProcessing();

            try
            {
                _isStopped = true;
                IState<TEvent> state = _currentState;
                _currentState = null;
                state.Exit();
            }
            finally
            {
                EndProcessing();
            }
        }

        private void ChangeState(IState<TEvent> nextState)
        {
            IState<TEvent> previousState = _currentState;
            _currentState = nextState;

            previousState.Exit();
            nextState.Enter();
        }

        private void EnsureRunning()
        {
            if (_isStopped)
                throw new InvalidOperationException("The state machine has been stopped.");
        }

        private void BeginProcessing()
        {
            if (_isProcessing)
                throw new InvalidOperationException("State-machine operations cannot be re-entered.");

            _isProcessing = true;
        }

        private void EndProcessing()
        {
            _isProcessing = false;
        }
    }
}
