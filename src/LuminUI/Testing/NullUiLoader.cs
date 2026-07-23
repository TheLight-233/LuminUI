using System.Threading;
using LuminThread;
using LuminUI.Interface;

namespace LuminUI.Testing
{
    // 无引擎测试加载器：返回占位根节点，不做任何资源 IO。
    public sealed class NullUiLoader : IUiLoader
    {
        public LuminTask<object> LoadAsync(string screenName, UILayer layer, CancellationToken ct = default)
            => LuminTask.FromResult<object>(new NullNode());

        public LuminTask PreloadAsync(string screenName, UILayer layer, CancellationToken ct = default)
            => LuminTask.FromResult(true);

        public void Unload(object root) { }
    }
}
