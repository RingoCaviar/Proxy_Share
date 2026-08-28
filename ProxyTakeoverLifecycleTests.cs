using System;

internal static class ProxyTakeoverLifecycleTests
{
    private static int failures;

    public static int Main()
    {
        Run("启用后可恢复接管前配置", EnableThenRestore);
        Run("外部修改使恢复快照失效", ExternalChangeInvalidatesSnapshot);
        Run("外部修改后再次启用不会覆盖", EnableAfterExternalChangeDoesNotOverwrite);
        Run("AutoDetect 存在性变化会使快照失效", AutoDetectPresenceChangeInvalidatesSnapshot);
        Run("写入失败会回滚", WriteFailureRollsBack);
        Run("快照保存失败不会修改系统代理", SnapshotSaveFailureDoesNotEscape);
        Run("回滚失败会提示人工检查", RollbackFailureIsReported);
        Run("读取失败会返回中文错误", ReadFailureReturnsError);
        Run("异常退出返回可恢复状态", CrashReturnsRecoveryAvailable);
        Run("退出状态保存失败会返回错误", CleanExitFailureReturnsError);
        Run("快照清理失败不会声称已放弃", SnapshotClearFailureReturnsError);
        Run("复读失败会回滚并返回错误", VerificationReadFailureRollsBack);
        Run("系统刷新通知失败会回滚", NotificationFailureRollsBack);
        Run("复读配置不一致会回滚", VerificationMismatchRollsBack);
        Run("接管状态读取失败会返回错误", TakeoverStateReadFailureReturnsError);
        Run("重新启用失败后仍保持接管状态", FailedReenableRemainsManaged);
        Run("恢复失败后仍保持接管状态", FailedRestoreRemainsManaged);
        Run("未接管时退出记录失败仍保持未接管", UnmanagedCleanExitFailureStaysUnmanaged);
        Console.WriteLine(failures == 0 ? "全部生命周期测试通过。" : failures + " 个测试失败。");
        return failures == 0 ? 0 : 1;
    }

    private static void EnableThenRestore()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        ProxyTakeoverLifecycle lifecycle = new ProxyTakeoverLifecycle(store);
        ProxyTakeoverResult enabled = lifecycle.Enable(new ProxyEndpoint("proxy.local", 7897));
        Equal("proxy.local:7897", enabled.Configuration.ProxyServer);
        ProxyTakeoverResult restored = lifecycle.DisableOrRestore();
        Equal("old:80", restored.Configuration.ProxyServer);
        False(store.HasTakeover);
    }

    private static void ExternalChangeInvalidatesSnapshot()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        ProxyTakeoverLifecycle lifecycle = new ProxyTakeoverLifecycle(store);
        lifecycle.Enable(new ProxyEndpoint("proxy.local", 7897));
        store.Current.SetProxyServer("external:9000");
        ProxyTakeoverResult result = lifecycle.ObserveExternalChange();
        Equal(ProxyTakeoverNoticeKind.Warning, result.NoticeKind);
        False(store.HasTakeover);
        Equal("external:9000", store.Current.ProxyServer);
    }

    private static void WriteFailureRollsBack()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        store.FailNextWrite = true;
        ProxyTakeoverResult result = new ProxyTakeoverLifecycle(store).Enable(new ProxyEndpoint("proxy.local", 7897));
        Equal(ProxyTakeoverNoticeKind.Error, result.NoticeKind);
        Equal("old:80", store.Current.ProxyServer);
        False(store.HasTakeover);
    }

    private static void SnapshotSaveFailureDoesNotEscape()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        store.FailNextSave = true;

        ProxyTakeoverResult result = new ProxyTakeoverLifecycle(store).Enable(new ProxyEndpoint("proxy.local", 7897));

        Equal(ProxyTakeoverNoticeKind.Error, result.NoticeKind);
        Contains("系统代理未修改", result.Message);
        Equal("old:80", store.Current.ProxyServer);
    }

    private static void RollbackFailureIsReported()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        store.WritesToFail = 2;

        ProxyTakeoverResult result = new ProxyTakeoverLifecycle(store).Enable(new ProxyEndpoint("proxy.local", 7897));

        Equal(ProxyTakeoverNoticeKind.Error, result.NoticeKind);
        Contains("回滚失败", result.Message);
        Contains("人工检查", result.Message);
    }

    private static void ReadFailureReturnsError()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        store.FailNextRead = true;

        ProxyTakeoverResult result = new ProxyTakeoverLifecycle(store).Enable(new ProxyEndpoint("proxy.local", 7897));

        Equal(ProxyTakeoverNoticeKind.Error, result.NoticeKind);
        Contains("读取系统代理失败", result.Message);
    }

    private static void EnableAfterExternalChangeDoesNotOverwrite()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        ProxyTakeoverLifecycle lifecycle = new ProxyTakeoverLifecycle(store);
        lifecycle.Enable(new ProxyEndpoint("proxy.local", 7897));
        store.Current.SetProxyServer("external:9000");
        ProxyTakeoverResult result = lifecycle.Enable(new ProxyEndpoint("new.local", 8080));
        Equal(ProxyTakeoverNoticeKind.Warning, result.NoticeKind);
        Equal("external:9000", store.Current.ProxyServer);
        False(store.HasTakeover);
    }

    private static void AutoDetectPresenceChangeInvalidatesSnapshot()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        ProxyTakeoverLifecycle lifecycle = new ProxyTakeoverLifecycle(store);
        lifecycle.Enable(new ProxyEndpoint("proxy.local", 7897));
        store.Current.SetAutoDetect(true, 0);

        ProxyTakeoverResult result = lifecycle.ObserveExternalChange();

        Equal(ProxyTakeoverNoticeKind.Warning, result.NoticeKind);
        False(store.HasTakeover);
    }

    private static void CrashReturnsRecoveryAvailable()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        ProxyTakeoverLifecycle lifecycle = new ProxyTakeoverLifecycle(store);
        lifecycle.Enable(new ProxyEndpoint("proxy.local", 7897));
        ProxyTakeoverResult result = new ProxyTakeoverLifecycle(store).Initialize();
        Equal(ProxyTakeoverState.RecoveryAvailable, result.State);
    }

    private static void CleanExitFailureReturnsError()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        ProxyTakeoverLifecycle lifecycle = new ProxyTakeoverLifecycle(store);
        lifecycle.Enable(new ProxyEndpoint("proxy.local", 7897));
        store.FailNextMarkCleanExit = true;

        ProxyTakeoverResult result = lifecycle.MarkCleanExit();

        Equal(ProxyTakeoverNoticeKind.Error, result.NoticeKind);
        Contains("记录正常退出失败", result.Message);
    }

    private static void SnapshotClearFailureReturnsError()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        ProxyTakeoverLifecycle lifecycle = new ProxyTakeoverLifecycle(store);
        lifecycle.Enable(new ProxyEndpoint("proxy.local", 7897));
        store.Current.SetProxyServer("external:9000");
        store.FailNextClear = true;

        ProxyTakeoverResult result = lifecycle.ObserveExternalChange();

        Equal(ProxyTakeoverNoticeKind.Error, result.NoticeKind);
        Contains("清除恢复快照失败", result.Message);
        Equal("external:9000", store.Current.ProxyServer);
    }

    private static void VerificationReadFailureRollsBack()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        store.FailReadNumber = 2;

        ProxyTakeoverResult result = new ProxyTakeoverLifecycle(store).Enable(new ProxyEndpoint("proxy.local", 7897));

        Equal(ProxyTakeoverNoticeKind.Error, result.NoticeKind);
        Contains("已回滚", result.Message);
        Equal("old:80", store.Current.ProxyServer);
    }

    private static void NotificationFailureRollsBack()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        store.FailNextNotify = true;
        ProxyTakeoverResult result = new ProxyTakeoverLifecycle(store).Enable(new ProxyEndpoint("proxy.local", 7897));
        Equal(ProxyTakeoverNoticeKind.Error, result.NoticeKind);
        Contains("已回滚", result.Message);
        Equal("old:80", store.Current.ProxyServer);
    }

    private static void VerificationMismatchRollsBack()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        store.IgnoreNextWrite = true;
        ProxyTakeoverResult result = new ProxyTakeoverLifecycle(store).Enable(new ProxyEndpoint("proxy.local", 7897));
        Equal(ProxyTakeoverNoticeKind.Error, result.NoticeKind);
        Contains("系统未确认", result.Message);
        Equal("old:80", store.Current.ProxyServer);
    }

    private static void TakeoverStateReadFailureReturnsError()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        store.FailNextLoad = true;
        ProxyTakeoverResult result = new ProxyTakeoverLifecycle(store).Initialize();
        Equal(ProxyTakeoverNoticeKind.Error, result.NoticeKind);
        Contains("代理接管状态失败", result.Message);
    }

    private static void FailedReenableRemainsManaged()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        ProxyTakeoverLifecycle lifecycle = new ProxyTakeoverLifecycle(store);
        lifecycle.Enable(new ProxyEndpoint("proxy.local", 7897));
        store.FailNextWrite = true;
        ProxyTakeoverResult result = lifecycle.Enable(new ProxyEndpoint("new.local", 8080));
        Equal(ProxyTakeoverNoticeKind.Error, result.NoticeKind);
        Equal(ProxyTakeoverState.Managed, result.State);
        True(store.HasTakeover);
    }

    private static void FailedRestoreRemainsManaged()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        ProxyTakeoverLifecycle lifecycle = new ProxyTakeoverLifecycle(store);
        lifecycle.Enable(new ProxyEndpoint("proxy.local", 7897));
        store.FailNextWrite = true;
        ProxyTakeoverResult result = lifecycle.DisableOrRestore();
        Equal(ProxyTakeoverNoticeKind.Error, result.NoticeKind);
        Equal(ProxyTakeoverState.Managed, result.State);
        True(store.HasTakeover);
    }

    private static void UnmanagedCleanExitFailureStaysUnmanaged()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        store.FailNextMarkCleanExit = true;
        ProxyTakeoverResult result = new ProxyTakeoverLifecycle(store).MarkCleanExit();
        Equal(ProxyTakeoverNoticeKind.Error, result.NoticeKind);
        Equal(ProxyTakeoverState.Unmanaged, result.State);
    }

    private static ProxyConfiguration Disabled(string server)
    {
        ProxyConfiguration value = new ProxyConfiguration();
        value.DisableManualProxy();
        value.SetProxyServer(server);
        return value;
    }

    private static void Run(string name, Action test) { try { test(); Console.WriteLine("通过：" + name); } catch (Exception ex) { failures++; Console.WriteLine("失败：" + name + " — " + ex.Message); } }
    private static void Equal(object expected, object actual) { if (!object.Equals(expected, actual)) throw new Exception("期望 [" + expected + "]，实际 [" + actual + "]"); }
    private static void False(bool value) { if (value) throw new Exception("期望 false，实际 true"); }
    private static void True(bool value) { if (!value) throw new Exception("期望 true，实际 false"); }
    private static void Contains(string expected, string actual) { if (actual == null || actual.IndexOf(expected, StringComparison.Ordinal) < 0) throw new Exception("期望消息包含 [" + expected + "]，实际 [" + actual + "]"); }
}

internal sealed class FakeProxyTakeoverStore : IProxyTakeoverStore
{
    public ProxyConfiguration Current;
    public ProxyConfiguration Snapshot;
    public ProxyConfiguration Managed;
    public bool HasTakeover;
    public bool CleanExit = true;
    public bool FailNextWrite;
    public int WritesToFail;
    public bool FailNextRead;
    public int ReadCount;
    public int FailReadNumber;
    public bool FailNextNotify;
    public bool IgnoreNextWrite;
    public bool FailNextLoad;
    public bool FailNextSave;
    public bool FailNextMarkCleanExit;
    public bool FailNextClear;
    public FakeProxyTakeoverStore(ProxyConfiguration current) { Current = current.Clone(); }
    public ProxyConfiguration ReadCurrent() { ReadCount++; if (FailNextRead || ReadCount == FailReadNumber) { FailNextRead = false; throw new InvalidOperationException("模拟读取失败"); } return Current.Clone(); }
    public void WriteCurrent(ProxyConfiguration value) { if (WritesToFail > 0) { WritesToFail--; throw new InvalidOperationException("模拟写入失败"); } if (FailNextWrite) { FailNextWrite = false; throw new InvalidOperationException("模拟写入失败"); } if (IgnoreNextWrite) { IgnoreNextWrite = false; return; } Current = value.Clone(); }
    public void NotifyChanged() { if (FailNextNotify) { FailNextNotify = false; throw new InvalidOperationException("模拟系统刷新通知失败"); } }
    public bool TryLoad(out ProxyConfiguration snapshot, out ProxyConfiguration managed, out bool cleanExit) { if (FailNextLoad) { FailNextLoad = false; throw new InvalidOperationException("模拟接管状态读取失败"); } snapshot = Snapshot == null ? null : Snapshot.Clone(); managed = Managed == null ? null : Managed.Clone(); cleanExit = CleanExit; return HasTakeover; }
    public void Save(ProxyConfiguration snapshot, ProxyConfiguration managed) { if (FailNextSave) { FailNextSave = false; throw new InvalidOperationException("模拟快照保存失败"); } Snapshot = snapshot.Clone(); Managed = managed.Clone(); HasTakeover = true; CleanExit = false; }
    public void Clear() { if (FailNextClear) { FailNextClear = false; throw new InvalidOperationException("模拟快照清理失败"); } HasTakeover = false; CleanExit = true; Snapshot = null; Managed = null; }
    public void MarkCleanExit(bool clean) { if (FailNextMarkCleanExit) { FailNextMarkCleanExit = false; throw new InvalidOperationException("模拟退出状态保存失败"); } if (HasTakeover) CleanExit = clean; }
}
