using System;

namespace LuminUI.Localization
{
    /// <summary>
    /// 本地化数据源接口。由项目实现（接 CSV/Excel/SO 等表格资源）。核心层不关心数据来源。
    /// </summary>
    public interface ILocalization
    {
        /// <summary>当前语言标识（如 "zh-CN"、"en"）。</summary>
        string CurrentLanguage { get; }
        /// <summary>按 key 取文本，缺失时建议回退为 key 本身。</summary>
        string Get(string key);
        /// <summary>按 key 取文本并用 args 格式化（string.Format 语义）。</summary>
        string Format(string key, object[] args);
    }

    /// <summary>
    /// 本地化总入口。视图里用 L("key") / LFormat("key", a, b) 取文本；
    /// 切换语言流程：在 ILocalization 实现里切好语言 → 调 LuminUi.BroadcastLanguageChanged()
    /// → 所有打开面板及其 Widget/列表 cell 的 OnLanguageChanged 被级联调用以刷新文本。
    /// </summary>
    public static class LocalizationManager
    {
        private static ILocalization? _impl;

        public static bool   HasProvider => _impl != null;
        public static string Language    => _impl?.CurrentLanguage ?? "";

        public static void SetProvider(ILocalization impl)
            => _impl = impl ?? throw new ArgumentNullException(nameof(impl));

        public static string Get(string key)
            => _impl != null ? _impl.Get(key) : key;

        public static string Format(string key, params object[] args)
            => _impl != null ? _impl.Format(key, args) : key;

        /// <summary>Domain Reload 复位。</summary>
        public static void Reset() => _impl = null;
    }
}
