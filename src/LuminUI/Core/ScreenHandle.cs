using System.Threading;
using System.Threading.Tasks;
using LuminThread;

namespace LuminUI
{
    // 屏句柄（强类型）。代际安全：状态查询都按 Id 走 LuminUi。
    // Id 单调递增从不复用，即使底层对象被池复用，旧句柄的 Id 也已失效，IsValid 必为 false。
    // View 仅在 IsValid 为 true 期间有效，关闭后不应再访问。
    public readonly struct ScreenHandle<T> where T : LuminView
    {
        public readonly T View;
        internal readonly int Id;
        private readonly ScreenCompletion? _completion;

        internal ScreenHandle(T view, int id, ScreenCompletion completion)
        { View = view; Id = id; _completion = completion; }

        public bool IsValid => LuminUi.IsAlive(Id);
        public LuminViewState State => LuminUi.TryGetState(Id, out var s) ? s : LuminViewState.None;
        public bool IsOpen => State == LuminViewState.Open || State == LuminViewState.Hidden;
        public bool IsVisible => State == LuminViewState.Open;
        public bool IsClosing => State == LuminViewState.Closing;

        public void Show() => LuminUi.ShowById(Id);
        public void Hide() => LuminUi.HideById(Id);
        public void Close() => LuminUi.CloseById(Id);

        public LuminTask<bool> CloseAsync(CancellationToken ct = default) => LuminUi.CloseByIdAsync(Id, ct);

        public LuminTask<bool> WaitForCloseAsync()
            => _completion == null || _completion.IsCompleted
                ? LuminTask.FromResult(_completion != null)
                : WaitClose(_completion);

        // 等待屏关闭并取回 SetResult 设的返回值（类型不符或无结果返回 default）
        public LuminTask<TResult?> WaitForResultAsync<TResult>()
        {
            if (_completion == null) return LuminTask.FromResult<TResult?>(default);
            if (_completion.TryGetResult(out var result))
                return LuminTask.FromResult(result is TResult value ? value : default);
            return WaitResult<TResult>(_completion);
        }

        private static async LuminTask<bool> WaitClose(ScreenCompletion completion)
        {
            await completion.WaitAsync();
            return true;
        }

        private static async LuminTask<TResult?> WaitResult<TResult>(ScreenCompletion completion)
        {
            var r = await completion.WaitAsync();
            return r is TResult val ? val : default;
        }

        public static implicit operator ScreenHandle(ScreenHandle<T> h)
            => h._completion == null ? default : new ScreenHandle(h.View, h.Id, h._completion);
    }

    // 屏句柄（弱类型），同样代际安全。
    public readonly struct ScreenHandle
    {
        public readonly LuminView View;
        internal readonly int Id;
        private readonly ScreenCompletion? _completion;

        internal ScreenHandle(LuminView view, int id, ScreenCompletion completion)
        { View = view; Id = id; _completion = completion; }

        public bool IsValid => LuminUi.IsAlive(Id);
        public LuminViewState State => LuminUi.TryGetState(Id, out var s) ? s : LuminViewState.None;
        public bool IsOpen => State == LuminViewState.Open || State == LuminViewState.Hidden;
        public bool IsVisible => State == LuminViewState.Open;
        public bool IsClosing => State == LuminViewState.Closing;

        public void Show() => LuminUi.ShowById(Id);
        public void Hide() => LuminUi.HideById(Id);
        public void Close() => LuminUi.CloseById(Id);

        public LuminTask<bool> CloseAsync(CancellationToken ct = default) => LuminUi.CloseByIdAsync(Id, ct);

        public LuminTask<bool> WaitForCloseAsync()
            => _completion == null || _completion.IsCompleted
                ? LuminTask.FromResult(_completion != null)
                : WaitClose(_completion);

        public LuminTask<TResult?> WaitForResultAsync<TResult>()
        {
            if (_completion == null) return LuminTask.FromResult<TResult?>(default);
            if (_completion.TryGetResult(out var result))
                return LuminTask.FromResult(result is TResult value ? value : default);
            return WaitResult<TResult>(_completion);
        }

        private static async LuminTask<bool> WaitClose(ScreenCompletion completion)
        {
            await completion.WaitAsync();
            return true;
        }

        private static async LuminTask<TResult?> WaitResult<TResult>(ScreenCompletion completion)
        {
            var r = await completion.WaitAsync();
            return r is TResult val ? val : default;
        }
    }

    // 每次 Open 独享，保证 View 被池复用后旧句柄仍然只观察自己的生命周期。
    internal sealed class ScreenCompletion
    {
        private bool _completed;
        private object? _result;
        private TaskCompletionSource<object?>? _source;

        internal bool IsCompleted => _completed;

        internal bool TryGetResult(out object? result)
        {
            result = _result;
            return _completed;
        }

        internal Task<object?> WaitAsync()
        {
            if (_source != null) return _source.Task;
            _source = new TaskCompletionSource<object?>();
            if (_completed) _source.TrySetResult(_result);
            return _source.Task;
        }

        internal void Complete(object? result)
        {
            if (_completed) return;
            _result = result;
            _completed = true;
            _source?.TrySetResult(result);
        }
    }
}
