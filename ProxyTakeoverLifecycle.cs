using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.Win32;

internal enum ProxyTakeoverNoticeKind { None, Success, Warning, Error }
internal enum ProxyTakeoverState { Unmanaged, Managed, RecoveryAvailable }

internal sealed class ProxyTakeoverResult
{
    public readonly ProxyConfiguration Configuration;
    public readonly ProxyTakeoverNoticeKind NoticeKind;
    public readonly string Message;
    public readonly ProxyTakeoverState State;
    public ProxyTakeoverResult(ProxyConfiguration configuration, ProxyTakeoverNoticeKind noticeKind, string message, ProxyTakeoverState state)
    {
        Configuration = configuration;
        NoticeKind = noticeKind;
        Message = message;
        State = state;
    }
}

internal interface IProxyTakeoverStore
{
    ProxyConfiguration ReadCurrent();
    void WriteCurrent(ProxyConfiguration configuration);
    void NotifyChanged();
    bool TryLoad(out ProxyConfiguration snapshot, out ProxyConfiguration managed, out bool cleanExit);
    void Save(ProxyConfiguration snapshot, ProxyConfiguration managed);
    void Clear();
    void MarkCleanExit(bool clean);
}

internal sealed class ProxyTakeoverLifecycle
{
    private const string PrivateBypass = "<local>;localhost;127.*;10.*;192.168.*;169.254.*;[::1];172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*";
    private readonly IProxyTakeoverStore store;
    public ProxyTakeoverLifecycle() : this(new WindowsProxyTakeoverStore()) { }
    internal ProxyTakeoverLifecycle(IProxyTakeoverStore store) { this.store = store; }

    public ProxyTakeoverResult Initialize()
    {
        ProxyConfiguration current;
        ProxyTakeoverResult readFailure;
        if (!TryReadCurrent(out current, out readFailure)) return readFailure;
        try
        {
            ProxyConfiguration snapshot;
            ProxyConfiguration managed;
            bool cleanExit;
            if (store.TryLoad(out snapshot, out managed, out cleanExit))
            {
                if (!current.Equals(managed))
                {
                    try
                    {
                        store.Clear();
                        return Result(current, ProxyTakeoverNoticeKind.Warning,
                            "检测到代理已被其他程序修改，已停止接管。");
                    }
                    catch (Exception clearError)
                    {
                        return Result(current, ProxyTakeoverNoticeKind.Error,
                            "检测到代理已被其他程序修改，但清除恢复快照失败：" + clearError.Message,
                            ProxyTakeoverState.Managed);
                    }
                }
                store.MarkCleanExit(false);
                if (!cleanExit)
                    return Result(current, ProxyTakeoverNoticeKind.Warning,
                        "检测到上次运行未正常结束；可安全恢复接管前配置。",
                        ProxyTakeoverState.RecoveryAvailable);
                return Result(current, ProxyTakeoverNoticeKind.None, null, ProxyTakeoverState.Managed);
            }
            return Result(current, ProxyTakeoverNoticeKind.None, null);
        }
        catch (Exception ex)
        {
            return Result(current, ProxyTakeoverNoticeKind.Error,
                "初始化代理接管状态失败：" + ex.Message);
        }
    }

    public ProxyTakeoverResult Enable(ProxyEndpoint endpoint)
    {
        ProxyConfiguration original;
        ProxyTakeoverResult readFailure;
        if (!TryReadCurrent(out original, out readFailure)) return readFailure;
        ProxyConfiguration snapshot;
        ProxyConfiguration previousManaged;
        bool cleanExit;
        bool alreadyManaged;
        try
        {
            alreadyManaged = store.TryLoad(out snapshot, out previousManaged, out cleanExit);
        }
        catch (Exception ex)
        {
            return Result(original, ProxyTakeoverNoticeKind.Error,
                "读取代理接管状态失败：" + ex.Message);
        }
        if (!alreadyManaged) snapshot = original.Clone();
        if (alreadyManaged && !original.Equals(previousManaged))
        {
            try
            {
                store.Clear();
                return Result(original, ProxyTakeoverNoticeKind.Warning,
                    "代理已被其他程序修改，本软件未覆盖该配置。");
            }
            catch (Exception clearError)
            {
                return Result(original, ProxyTakeoverNoticeKind.Error,
                    "代理已被其他程序修改；本软件未覆盖该配置，但清除恢复快照失败：" + clearError.Message,
                    ProxyTakeoverState.Managed);
            }
        }
        ProxyConfiguration desired = original.Clone();
        IPAddress parsed;
        string address = IPAddress.TryParse(endpoint.Address, out parsed) &&
            parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? "[" + endpoint.Address + "]"
            : endpoint.Address;
        desired.UseManualProxy(address + ":" + endpoint.Port,
            MergeBypass(original.GetProxyOverrideOrNull()));
        try
        {
            store.Save(snapshot, desired);
        }
        catch (Exception ex)
        {
            string repairError;
            if (TryRepairTakeoverState(alreadyManaged, snapshot, previousManaged, out repairError))
                return Result(original, ProxyTakeoverNoticeKind.Error,
                    "保存代理接管状态失败，系统代理未修改：" + ex.Message,
                    alreadyManaged ? ProxyTakeoverState.Managed : ProxyTakeoverState.Unmanaged);
            return Result(original, ProxyTakeoverNoticeKind.Error,
                "保存代理接管状态失败，且恢复接管状态失败，需要人工检查。保存错误：" + ex.Message +
                "；恢复错误：" + repairError, ProxyTakeoverState.Managed);
        }
        string error;
        ProxyConfiguration actual;
        if (TryApply(desired, original, out actual, out error))
            return Result(actual, ProxyTakeoverNoticeKind.None, null, ProxyTakeoverState.Managed);
        string stateRepairError;
        bool takeoverStateRepaired = TryRepairTakeoverState(
            alreadyManaged, snapshot, original, out stateRepairError);
        if (!takeoverStateRepaired)
            error += "；同时恢复接管状态失败，需要人工检查：" + stateRepairError;
        ProxyTakeoverState failureState = alreadyManaged || !takeoverStateRepaired
            ? ProxyTakeoverState.Managed
            : ProxyTakeoverState.Unmanaged;
        return Result(actual, ProxyTakeoverNoticeKind.Error, error, failureState);
    }

    public ProxyTakeoverResult DisableOrRestore()
    {
        ProxyConfiguration current;
        ProxyTakeoverResult readFailure;
        if (!TryReadCurrent(out current, out readFailure)) return readFailure;
        ProxyConfiguration snapshot;
        ProxyConfiguration managed;
        bool cleanExit;
        bool isManaged;
        try
        {
            isManaged = store.TryLoad(out snapshot, out managed, out cleanExit);
        }
        catch (Exception ex)
        {
            return Result(current, ProxyTakeoverNoticeKind.Error,
                "读取代理接管状态失败：" + ex.Message);
        }
        string error;
        if (isManaged)
        {
            if (!current.Equals(managed))
            {
                try
                {
                    store.Clear();
                    return Result(current, ProxyTakeoverNoticeKind.Warning,
                        "代理已被其他程序修改，本软件未覆盖该配置。");
                }
                catch (Exception clearError)
                {
                    return Result(current, ProxyTakeoverNoticeKind.Error,
                        "代理已被其他程序修改；本软件未覆盖该配置，但清除恢复快照失败：" + clearError.Message,
                        ProxyTakeoverState.Managed);
                }
            }
            ProxyConfiguration restored;
            if (TryApply(snapshot, current, out restored, out error))
            {
                try
                {
                    store.Clear();
                    return Result(restored, ProxyTakeoverNoticeKind.Success,
                        "已恢复接管前的系统代理配置。");
                }
                catch (Exception clearError)
                {
                    return Result(restored, ProxyTakeoverNoticeKind.Error,
                        "已恢复系统代理，但清除恢复快照失败：" + clearError.Message,
                        ProxyTakeoverState.Managed);
                }
            }
            return Result(restored, ProxyTakeoverNoticeKind.Error, error, ProxyTakeoverState.Managed);
        }
        ProxyConfiguration desired = current.Clone();
        desired.DisableManualProxy();
        try
        {
            store.Save(current, desired);
        }
        catch (Exception ex)
        {
            string repairError;
            if (TryRepairTakeoverState(false, current, desired, out repairError))
                return Result(current, ProxyTakeoverNoticeKind.Error,
                    "保存代理接管状态失败，系统代理未修改：" + ex.Message);
            return Result(current, ProxyTakeoverNoticeKind.Error,
                "保存代理接管状态失败，且清除不完整快照失败，需要人工检查。保存错误：" + ex.Message +
                "；清理错误：" + repairError, ProxyTakeoverState.Managed);
        }
        ProxyConfiguration disabled;
        if (TryApply(desired, current, out disabled, out error)) return Result(disabled, ProxyTakeoverNoticeKind.Success, "已关闭当前手动代理；原配置已保存，可在后续恢复。", ProxyTakeoverState.Managed);
        ProxyTakeoverState disabledFailureState = ProxyTakeoverState.Unmanaged;
        try
        {
            store.Clear();
        }
        catch (Exception clearError)
        {
            error += "；同时清除恢复快照失败，需要人工检查：" + clearError.Message;
            disabledFailureState = ProxyTakeoverState.Managed;
        }
        return Result(disabled, ProxyTakeoverNoticeKind.Error, error, disabledFailureState);
    }

    public ProxyTakeoverResult ObserveExternalChange()
    {
        ProxyConfiguration current;
        ProxyTakeoverResult readFailure;
        if (!TryReadCurrent(out current, out readFailure)) return readFailure;
        try
        {
            ProxyConfiguration snapshot;
            ProxyConfiguration managed;
            bool cleanExit;
            bool isManaged = store.TryLoad(out snapshot, out managed, out cleanExit);
            if (isManaged && !current.Equals(managed))
            {
                try
                {
                    store.Clear();
                    return Result(current, ProxyTakeoverNoticeKind.Warning,
                        "检测到外部代理修改，已放弃旧的恢复快照。");
                }
                catch (Exception clearError)
                {
                    return Result(current, ProxyTakeoverNoticeKind.Error,
                        "检测到外部代理修改，但清除恢复快照失败：" + clearError.Message,
                        ProxyTakeoverState.Managed);
                }
            }
            return Result(current, ProxyTakeoverNoticeKind.None, null,
                isManaged ? ProxyTakeoverState.Managed : ProxyTakeoverState.Unmanaged);
        }
        catch (Exception ex)
        {
            return Result(current, ProxyTakeoverNoticeKind.Error,
                "读取代理接管状态失败：" + ex.Message);
        }
    }
    public ProxyTakeoverResult MarkCleanExit()
    {
        ProxyConfiguration current;
        ProxyTakeoverResult readFailure;
        if (!TryReadCurrent(out current, out readFailure)) return readFailure;
        bool isManaged = false;
        try
        {
            ProxyConfiguration snapshot;
            ProxyConfiguration managed;
            bool cleanExit;
            isManaged = store.TryLoad(out snapshot, out managed, out cleanExit);
            store.MarkCleanExit(true);
            return Result(current, ProxyTakeoverNoticeKind.None, null,
                isManaged ? ProxyTakeoverState.Managed : ProxyTakeoverState.Unmanaged);
        }
        catch (Exception ex)
        {
            return Result(current, ProxyTakeoverNoticeKind.Error,
                "记录正常退出失败：" + ex.Message,
                isManaged ? ProxyTakeoverState.Managed : ProxyTakeoverState.Unmanaged);
        }
    }
    private bool TryApply(ProxyConfiguration desired, ProxyConfiguration original,
        out ProxyConfiguration actual, out string error)
    {
        actual = null;
        try
        {
            store.WriteCurrent(desired);
            store.NotifyChanged();
            actual = store.ReadCurrent();
            if (!actual.Equals(desired))
                throw new InvalidOperationException("系统未确认新的代理配置：" + desired.DescribeDifferences(actual));
            error = null;
            return true;
        }
        catch (Exception applyError)
        {
            try
            {
                store.WriteCurrent(original);
                store.NotifyChanged();
                actual = store.ReadCurrent();
                if (!actual.Equals(original))
                    throw new InvalidOperationException("复读结果与接管前配置不一致");
                error = "应用失败，已回滚：" + applyError.Message;
            }
            catch (Exception rollbackError)
            {
                actual = null;
                error = "应用失败，且回滚失败，需要人工检查。应用错误：" + applyError.Message +
                    "；回滚错误：" + rollbackError.Message;
            }
            return false;
        }
    }
    private static ProxyTakeoverResult Result(ProxyConfiguration configuration,
        ProxyTakeoverNoticeKind noticeKind, string message)
    {
        return Result(configuration, noticeKind, message, ProxyTakeoverState.Unmanaged);
    }

    private static ProxyTakeoverResult Result(ProxyConfiguration configuration,
        ProxyTakeoverNoticeKind noticeKind, string message, ProxyTakeoverState state)
    {
        return new ProxyTakeoverResult(configuration, noticeKind, message, state);
    }

    private static string MergeBypass(string existing)
    {
        List<string> values = new List<string>();
        Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (string item in ((existing ?? "") + ";" + PrivateBypass).Split(';'))
        {
            string value = item.Trim();
            if (value.Length > 0 && !seen.ContainsKey(value))
            {
                seen.Add(value, true);
                values.Add(value);
            }
        }
        return string.Join(";", values.ToArray());
    }

    private bool TryReadCurrent(out ProxyConfiguration configuration, out ProxyTakeoverResult failure)
    {
        try
        {
            configuration = store.ReadCurrent();
            failure = null;
            return true;
        }
        catch (Exception ex)
        {
            configuration = null;
            failure = Result(null, ProxyTakeoverNoticeKind.Error, "读取系统代理失败：" + ex.Message);
            return false;
        }
    }

    private bool TryRepairTakeoverState(bool wasManaged, ProxyConfiguration snapshot,
        ProxyConfiguration managed, out string error)
    {
        try
        {
            if (wasManaged) store.Save(snapshot, managed);
            else store.Clear();
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

internal sealed class WindowsProxyTakeoverStore : IProxyTakeoverStore
{
    private const string Path = @"Software\ProxyShare";
    public ProxyConfiguration ReadCurrent() { return ProxyConfiguration.Read(); }
    public void WriteCurrent(ProxyConfiguration configuration) { configuration.Write(); }
    public void NotifyChanged()
    {
        bool settingsChanged = InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0);
        bool settingsRefreshed = InternetSetOption(IntPtr.Zero, 37, IntPtr.Zero, 0);
        if (!settingsChanged || !settingsRefreshed)
            throw new InvalidOperationException("Windows 未确认代理设置刷新通知");
    }
    public bool TryLoad(out ProxyConfiguration snapshot, out ProxyConfiguration managed, out bool cleanExit)
    {
        snapshot = null;
        managed = null;
        cleanExit = true;
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(Path))
        {
            if (key == null || Convert.ToInt32(key.GetValue("HasTakeover", 0)) != 1) return false;
            snapshot = ProxyConfiguration.Load(key, "Snapshot");
            managed = ProxyConfiguration.Load(key, "Managed");
            cleanExit = Convert.ToInt32(key.GetValue("CleanExit", 1)) == 1;
            return true;
        }
    }
    public void Save(ProxyConfiguration snapshot, ProxyConfiguration managed)
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Path))
        {
            snapshot.Save(key, "Snapshot");
            managed.Save(key, "Managed");
            key.SetValue("CleanExit", 0, RegistryValueKind.DWord);
            key.SetValue("HasTakeover", 1, RegistryValueKind.DWord);
        }
    }

    public void Clear()
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Path))
        {
            key.SetValue("HasTakeover", 0, RegistryValueKind.DWord);
            key.SetValue("CleanExit", 1, RegistryValueKind.DWord);
        }
    }

    public void MarkCleanExit(bool clean)
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(Path, true))
        {
            if (key != null && Convert.ToInt32(key.GetValue("HasTakeover", 0)) == 1)
                key.SetValue("CleanExit", clean ? 1 : 0, RegistryValueKind.DWord);
        }
    }

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int option, IntPtr buffer, int length);
}
