using System;

internal static class ProxyTakeoverLifecycleTests
{
    private static int failures;

    [STAThread]
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
        Run("系统代理端点可与软件配置比较", ProxyEndpointComparison);
        Run("关闭时切换主机仅更新端点", SelectWhileDisabled);
        Run("开启时切换主机并恢复最初配置", SelectWhileEnabled);
        Run("选择主机失败保留原接管状态", FailedSelectionKeepsSnapshot);
        Run("关闭时选择主机刷新失败会回滚", FailedDisabledSelectionRollsBack);
        Run("主机列表编辑和删除后持久化", HostCatalogRoundTrip);
        Run("删除所选主机后选择剩余主机", DeletedSelectionFallsBack);
        Run("损坏的主机列表不会被部分读取", InvalidHostCatalogRejected);
        Run("非法主机端点被拒绝", InvalidHostRejected);
        Run("识别其他代理进程名称", OtherProxyProcessesRecognized);
        Run("代理延迟包括 Google 请求和响应等待时间", GoogleProbeMeasuresResponseDelay);
        Run("代理异常响应不能被当成 Google 延迟", GoogleProbeRejectsUnexpectedResponse);
        Run("Google 延迟测试遵守请求超时", GoogleProbeTimesOut);
        Run("切换主机立即同步开启和关闭状态", HostSwitchUpdatesToggleImmediately);
        Run("切换后发生外部修改立即刷新开关", HostSwitchRereadsLatestSystemState);
        Run("切换失败后开关反映实际系统代理", FailedHostSwitchUpdatesToggle);
        Run("切换后系统复读失败不显示开启", HostSwitchReadFailureDoesNotShowOn);
        Run("升级保留自定义主机且内置数据不可覆盖", HostFilesAreBuildSpecific);
        Run("迁移不恢复用户已删除的主机", MigrationPreservesDeletedHosts);
        Run("损坏的主机文件回退内置数据", CorruptHostFileFallsBack);
        Run("恢复快照和退出标记通过文件完整保存", TakeoverFileRoundTrip);
        Run("文件保存失败不会破坏已有数据", FileSaveFailureKeepsExistingData);
        Run("旧恢复快照转换保留完整配置", LegacyRecoveryConversion);
        Run("退出软件及正常重启均保持代理设置", ExitAndRestartKeepProxy);
        Run("开关支持键盘和可访问状态", SwitchKeyboardAndAccessibility);
        Run("编辑窗口空状态提供添加入口", EditorEmptyStateHasAddAction);
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

    private static void ExitAndRestartKeepProxy()
    {
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(Disabled("old:80"));
        ProxyTakeoverLifecycle lifecycle = new ProxyTakeoverLifecycle(store);
        lifecycle.Enable(new ProxyEndpoint("192.168.1.4", 7892));
        ProxyConfiguration applied = store.Current.Clone();
        ProxyTakeoverResult exited = lifecycle.MarkCleanExit();
        Equal(ProxyTakeoverNoticeKind.None, exited.NoticeKind);
        Equal(applied, store.Current);
        Equal(true, store.CleanExit);
        ProxyTakeoverResult restarted = new ProxyTakeoverLifecycle(store).Initialize();
        Equal(ProxyTakeoverState.Managed, restarted.State);
        Equal(ProxyTakeoverNoticeKind.None, restarted.NoticeKind);
        Equal(applied, store.Current);
        Equal(true, store.Current.ProxyEnabled);
    }

    private static void SwitchKeyboardAndAccessibility()
    {
        using (ToggleSwitch toggle = new ToggleSwitch())
        {
            int changes = 0;
            toggle.ToggleChanged += delegate { changes++; };
            System.Reflection.MethodInfo down = typeof(ToggleSwitch).GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            System.Reflection.MethodInfo up = typeof(ToggleSwitch).GetMethod("OnKeyUp", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            down.Invoke(toggle, new object[] { new System.Windows.Forms.KeyEventArgs(System.Windows.Forms.Keys.Space) });
            down.Invoke(toggle, new object[] { new System.Windows.Forms.KeyEventArgs(System.Windows.Forms.Keys.Space) });
            up.Invoke(toggle, new object[] { new System.Windows.Forms.KeyEventArgs(System.Windows.Forms.Keys.Space) });
            Equal(true, toggle.IsOn);
            Equal(1, changes);
            Equal(true, (toggle.AccessibilityObject.State & System.Windows.Forms.AccessibleStates.Checked) != 0);
            down.Invoke(toggle, new object[] { new System.Windows.Forms.KeyEventArgs(System.Windows.Forms.Keys.Enter) });
            up.Invoke(toggle, new object[] { new System.Windows.Forms.KeyEventArgs(System.Windows.Forms.Keys.Enter) });
            False(toggle.IsOn);
            Equal(2, changes);
            toggle.AccessibilityObject.DoDefaultAction();
            Equal(true, toggle.IsOn);
            toggle.Enabled = false;
            toggle.AccessibilityObject.DoDefaultAction();
            Equal(3, changes);
        }
    }

    private static void EditorEmptyStateHasAddAction()
    {
        using (ProxyHostEditor editor = new ProxyHostEditor(ProxyHostCatalog.Defaults(), ThemePalette.Create(true)))
        {
            System.Windows.Forms.DataGridView grid = FindControl<System.Windows.Forms.DataGridView>(editor);
            Equal(0, grid.Rows.Count);
            Equal(true, HasLabel(grid, "尚未添加自定义主机\r\n添加常用代理后，可在主界面快速切换。"));
            Equal(true, FindControl<System.Windows.Forms.Button>(grid) != null);
        }
    }

    private static void LegacyRecoveryConversion()
    {
        System.Collections.Generic.Dictionary<string, object> fields = new System.Collections.Generic.Dictionary<string, object>();
        fields["SnapshotHasProxyEnable"] = 1;
        fields["SnapshotProxyEnable"] = 0;
        fields["SnapshotHasProxyServer"] = 1;
        fields["SnapshotProxyServer"] = "old:80";
        fields["SnapshotHasAutoDetect"] = 1;
        fields["SnapshotAutoDetect"] = 1;
        ProxyConfiguration expected = Disabled("old:80");
        expected.SetAutoDetect(true, 1);
        ProxyConfiguration loaded = WindowsProxyTakeoverStore.ReadLegacyConfiguration(delegate(string name, object fallback)
        {
            object value;
            return fields.TryGetValue(name, out value) ? value : fallback;
        }, "Snapshot");
        Equal(expected, loaded);
    }

    private static void HostFilesAreBuildSpecific()
    {
        WithTemporaryDirectory(delegate(string directory)
        {
            string firstBuild = System.IO.Path.Combine(directory, "hosts-build-one.txt");
            string nextBuild = System.IO.Path.Combine(directory, "hosts-build-two.txt");
            string warning;
            ProxyHostCatalog catalog = ProxyHostCatalog.Load(firstBuild, out warning);
            Equal(null, warning);
            catalog.SelectedId = "yifan";
            catalog.SaveSelection();
            False(System.IO.File.Exists(firstBuild));
            catalog.Hosts.Add(new ProxyHost("custom-test", "用户自定义", new ProxyEndpoint("proxy.local", 8080)));
            catalog.SelectedId = "custom-test";
            catalog.Save(firstBuild);
            catalog.Save(firstBuild); // Exercise atomic replacement as well as first creation.
            ProxyHostCatalog loaded = ProxyHostCatalog.Load(firstBuild, out warning);
            Equal("用户自定义", loaded.Selected.Name);
            Equal("proxy.local:8080", loaded.Selected.Endpoint.GetServerAddress());
            ProxyHostCatalog upgraded = ProxyHostCatalog.Load(nextBuild, out warning);
            Equal("用户自定义", upgraded.Selected.Name);
            Equal("瘦羊羊", upgraded.Hosts[0].Name);
            Equal(3, upgraded.Hosts.Count);
            Equal("用户自定义", upgraded.Hosts[2].Name);
            Equal(true, System.IO.File.Exists(firstBuild));
            Equal(true, System.IO.File.Exists(nextBuild));
            string[] saved = System.IO.File.ReadAllLines(nextBuild);
            Equal(2, saved.Length);
            False(saved[1].StartsWith("yifan|"));
            string malicious = "yifan|" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("覆盖内置")) + "|evil.local|80";
            ProxyHostCatalog protectedCatalog = ProxyHostCatalog.Parse(new string[] { "yifan", malicious });
            Equal("瘦羊羊", protectedCatalog.Selected.Name);
            Equal("192.168.1.72:7897", protectedCatalog.Selected.Endpoint.GetServerAddress());
        });
    }

    private static void CorruptHostFileFallsBack()
    {
        WithTemporaryDirectory(delegate(string directory)
        {
            string path = System.IO.Path.Combine(directory, "hosts.txt");
            System.IO.File.WriteAllText(path, "invalid");
            string warning;
            ProxyHostCatalog catalog = ProxyHostCatalog.Load(path, out warning);
            Equal("David·博", catalog.Selected.Name);
            Equal(true, warning != null);
            Equal("invalid", System.IO.File.ReadAllText(path));
        });
    }

    private static void MigrationPreservesDeletedHosts()
    {
        WithTemporaryDirectory(delegate(string directory)
        {
            string older = System.IO.Path.Combine(directory, "hosts-old.txt");
            string newer = System.IO.Path.Combine(directory, "hosts-new.txt");
            ProxyHostCatalog catalog = ProxyHostCatalog.Defaults();
            catalog.Hosts.Add(new ProxyHost("deleted", "已删除主机", new ProxyEndpoint("proxy.local", 80)));
            catalog.Save(older);
            ProxyHostCatalog.Defaults().Save(newer);
            System.IO.File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddDays(-1));
            string warning;
            ProxyHostCatalog loaded = ProxyHostCatalog.Load(System.IO.Path.Combine(directory, "custom-hosts.txt"), out warning);
            Equal(2, loaded.Hosts.Count);
            Equal("David·博", loaded.Selected.Name);
        });
    }

    private static void TakeoverFileRoundTrip()
    {
        WithTemporaryDirectory(delegate(string directory)
        {
            string path = System.IO.Path.Combine(directory, "takeover.bin");
            WindowsProxyTakeoverStore store = new WindowsProxyTakeoverStore(path);
            ProxyConfiguration snapshot = new ProxyConfiguration();
            snapshot.SetAutoDetect(true, 0);
            ProxyConfiguration managed = snapshot.Clone();
            managed.UseManualProxy("proxy.local:7890", "<local>");
            store.Save(snapshot, managed);
            ProxyConfiguration loadedSnapshot;
            ProxyConfiguration loadedManaged;
            bool clean;
            Equal(true, new WindowsProxyTakeoverStore(path).TryLoad(out loadedSnapshot, out loadedManaged, out clean));
            Equal(snapshot, loadedSnapshot);
            Equal(managed, loadedManaged);
            False(clean);
            store.MarkCleanExit(true);
            Equal(true, store.TryLoad(out loadedSnapshot, out loadedManaged, out clean));
            Equal(true, clean);
            Equal(snapshot, loadedSnapshot);
            store.MarkCleanExit(false);
            store.TryLoad(out loadedSnapshot, out loadedManaged, out clean);
            False(clean);
            store.Clear();
            False(store.TryLoad(out loadedSnapshot, out loadedManaged, out clean));
            store.MarkCleanExit(true);
            False(System.IO.File.Exists(path));
        });
    }

    private static void FileSaveFailureKeepsExistingData()
    {
        WithTemporaryDirectory(delegate(string directory)
        {
            string path = System.IO.Path.Combine(directory, "hosts.txt");
            ProxyHostCatalog catalog = ProxyHostCatalog.Defaults();
            catalog.Save(path);
            string original = System.IO.File.ReadAllText(path);
            using (System.IO.FileStream locked = new System.IO.FileStream(path, System.IO.FileMode.Open,
                System.IO.FileAccess.Read, System.IO.FileShare.None))
            {
                bool failed = false;
                try { catalog.Save(path); }
                catch (System.IO.IOException) { failed = true; }
                Equal(true, failed);
            }
            Equal(original, System.IO.File.ReadAllText(path));
            Equal(1, System.IO.Directory.GetFiles(directory).Length);
        });
    }

    private static void WithTemporaryDirectory(Action<string> action)
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ProxyShareTests_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        try { action(directory); }
        finally
        {
            foreach (string path in System.IO.Directory.GetFiles(directory)) System.IO.File.Delete(path);
            System.IO.Directory.Delete(directory);
        }
    }

    private static void HostSwitchUpdatesToggleImmediately()
    {
        foreach (bool enabled in new bool[] { false, true })
        {
            ProxyConfiguration current = Disabled("192.168.1.4:7892");
            if (enabled) current.UseManualProxy("192.168.1.4:7892", "<local>");
            FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(current);
            using (MainForm form = new MainForm(new ProxyTakeoverLifecycle(store), ProxyHostCatalog.Defaults(), delegate { }))
            {
                System.Windows.Forms.ComboBox combo = FindControl<System.Windows.Forms.ComboBox>(form);
                ToggleSwitch toggle = FindControl<ToggleSwitch>(form);
                combo.SelectedIndex = 0;
                Equal("192.168.1.72:7897", store.Current.ProxyServer);
                Equal(enabled, toggle.IsOn);
                combo.SelectedIndex = 1;
                Equal("192.168.1.4:7892", store.Current.ProxyServer);
                Equal(enabled, toggle.IsOn);
            }
        }
    }

    private static void HostSwitchRereadsLatestSystemState()
    {
        foreach (bool disable in new bool[] { false, true })
        {
            ProxyConfiguration current = Disabled("192.168.1.4:7892");
            current.UseManualProxy("192.168.1.4:7892", "<local>");
            FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(current);
            using (MainForm form = new MainForm(new ProxyTakeoverLifecycle(store), ProxyHostCatalog.Defaults(), delegate { }))
            {
                bool changed = false;
                // Another proxy changes Windows settings just after our write verification.
                store.AfterRead = delegate(ProxyConfiguration read)
                {
                    if (changed || !read.UsesEndpoint(new ProxyEndpoint("192.168.1.72", 7897))) return;
                    changed = true;
                    if (disable) store.Current.DisableManualProxy();
                    else store.Current.SetProxyServer("external:9000");
                };
                FindControl<System.Windows.Forms.ComboBox>(form).SelectedIndex = 0;
                Equal(true, changed);
                False(FindControl<ToggleSwitch>(form).IsOn);
                Equal(true, HasLabel(form, disable ? "已关闭" : "未生效"));
            }
        }
    }

    private static void FailedHostSwitchUpdatesToggle()
    {
        ProxyConfiguration current = Disabled("192.168.1.4:7892");
        current.UseManualProxy("192.168.1.4:7892", "<local>");
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(current);
        using (MainForm form = new MainForm(new ProxyTakeoverLifecycle(store), ProxyHostCatalog.Defaults(), delegate { }))
        {
            Equal(true, FindControl<ToggleSwitch>(form).IsOn);
            store.FailNextWrite = true;
            FindControl<System.Windows.Forms.ComboBox>(form).SelectedIndex = 0;
            Equal("192.168.1.4:7892", store.Current.ProxyServer);
            False(FindControl<ToggleSwitch>(form).IsOn);
            Equal(true, HasLabel(form, "未生效"));
        }
    }

    private static void HostSwitchReadFailureDoesNotShowOn()
    {
        ProxyConfiguration current = Disabled("192.168.1.4:7892");
        current.UseManualProxy("192.168.1.4:7892", "<local>");
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(current);
        using (MainForm form = new MainForm(new ProxyTakeoverLifecycle(store), ProxyHostCatalog.Defaults(), delegate { }))
        {
            store.AfterRead = delegate(ProxyConfiguration read)
            {
                if (read.UsesEndpoint(new ProxyEndpoint("192.168.1.72", 7897))) store.FailNextRead = true;
            };
            FindControl<System.Windows.Forms.ComboBox>(form).SelectedIndex = 0;
            False(FindControl<ToggleSwitch>(form).IsOn);
            Equal(true, HasLabel(form, "无法确认"));
        }
    }

    private static T FindControl<T>(System.Windows.Forms.Control root) where T : System.Windows.Forms.Control
    {
        foreach (System.Windows.Forms.Control child in root.Controls)
        {
            T found = child as T ?? FindControl<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    private static bool HasLabel(System.Windows.Forms.Control root, string text)
    {
        foreach (System.Windows.Forms.Control child in root.Controls)
            if (child is System.Windows.Forms.Label && child.Text == text || HasLabel(child, text)) return true;
        return false;
    }

    private static void SelectWhileDisabled()
    {
        ProxyConfiguration original = Disabled("old:80");
        original.SetAutoDetect(true, 1);
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(original);
        ProxyTakeoverLifecycle lifecycle = new ProxyTakeoverLifecycle(store);
        ProxyTakeoverResult selected = lifecycle.SelectEndpoint(new ProxyEndpoint("192.168.1.72", 7897));
        Equal(ProxyTakeoverNoticeKind.None, selected.NoticeKind);
        Equal("192.168.1.72:7897", store.Current.ProxyServer);
        False(store.Current.ProxyEnabled);
        ProxyConfiguration expected = original.Clone();
        expected.SetProxyServer("192.168.1.72:7897");
        Equal(expected, store.Current);
        lifecycle.Enable(new ProxyEndpoint("192.168.1.4", 7892));
        Equal(original, lifecycle.DisableOrRestore().Configuration);
    }

    private static void SelectWhileEnabled()
    {
        ProxyConfiguration original = Disabled("old:80");
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(original);
        ProxyTakeoverLifecycle lifecycle = new ProxyTakeoverLifecycle(store);
        lifecycle.Enable(new ProxyEndpoint("192.168.1.4", 7892));
        ProxyTakeoverResult result = lifecycle.SelectEndpoint(new ProxyEndpoint("192.168.1.72", 7897));
        Equal(ProxyTakeoverNoticeKind.None, result.NoticeKind);
        Equal(true, result.Configuration.UsesEndpoint(new ProxyEndpoint("192.168.1.72", 7897)));
        False(result.Configuration.UsesEndpoint(new ProxyEndpoint("192.168.1.4", 7892)));
        Equal(original, lifecycle.DisableOrRestore().Configuration);
    }

    private static void FailedSelectionKeepsSnapshot()
    {
        ProxyConfiguration original = Disabled("old:80");
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(original);
        ProxyTakeoverLifecycle lifecycle = new ProxyTakeoverLifecycle(store);
        lifecycle.Enable(new ProxyEndpoint("192.168.1.4", 7892));
        store.FailNextNotify = true;
        ProxyTakeoverResult result = lifecycle.SelectEndpoint(new ProxyEndpoint("192.168.1.72", 7897));
        Equal(ProxyTakeoverNoticeKind.Error, result.NoticeKind);
        Equal(true, store.Current.UsesEndpoint(new ProxyEndpoint("192.168.1.4", 7892)));
        Equal(original, lifecycle.DisableOrRestore().Configuration);
    }

    private static void FailedDisabledSelectionRollsBack()
    {
        ProxyConfiguration original = Disabled("old:80");
        FakeProxyTakeoverStore store = new FakeProxyTakeoverStore(original);
        store.FailNextNotify = true;
        ProxyTakeoverResult result = new ProxyTakeoverLifecycle(store).SelectEndpoint(new ProxyEndpoint("192.168.1.72", 7897));
        Equal(ProxyTakeoverNoticeKind.Error, result.NoticeKind);
        Equal(original, store.Current);
        False(store.HasTakeover);
    }

    private static void HostCatalogRoundTrip()
    {
        ProxyHostCatalog catalog = ProxyHostCatalog.Defaults();
        Equal("瘦羊羊", catalog.Hosts[0].Name);
        Equal("192.168.1.72:7897", catalog.Hosts[0].Endpoint.GetServerAddress());
        Equal("David·博", catalog.Hosts[1].Name);
        Equal("192.168.1.4:7892", catalog.Hosts[1].Endpoint.GetServerAddress());
        ProxyHost edited;
        string error;
        Equal(true, ProxyHost.TryCreate("custom-office", "赵博 | 办公室", "proxy.local", "8080", out edited, out error));
        catalog.Hosts.Add(edited);
        catalog.SelectedId = edited.Id;
        ProxyHostCatalog loaded = ProxyHostCatalog.Parse(catalog.Serialize());
        Equal(3, loaded.Hosts.Count);
        Equal("赵博 | 办公室", loaded.Selected.Name);
        Equal("proxy.local:8080", loaded.Selected.Endpoint.GetServerAddress());
    }

    private static void DeletedSelectionFallsBack()
    {
        ProxyHostCatalog catalog = ProxyHostCatalog.Defaults();
        catalog.Hosts.Add(new ProxyHost("deleted", "待删除", new ProxyEndpoint("proxy.local", 80)));
        catalog.SelectedId = "deleted";
        catalog.Hosts.RemoveAt(2);
        catalog.SelectedId = "zhaobo";
        ProxyHostCatalog loaded = ProxyHostCatalog.Parse(catalog.Serialize());
        Equal("David·博", loaded.Selected.Name);
    }

    private static void InvalidHostCatalogRejected()
    {
        try { ProxyHostCatalog.Parse(new string[] { "x", "invalid-data" }); }
        catch (FormatException) { return; }
        throw new Exception("损坏配置应被拒绝");
    }

    private static void InvalidHostRejected()
    {
        ProxyHost host;
        string error;
        False(ProxyHost.TryCreate("x", "", "127.0.0.1", "7890", out host, out error));
        False(ProxyHost.TryCreate("x", "主机", "http://127.0.0.1", "7890", out host, out error));
        False(ProxyHost.TryCreate("x", "主机", "127.0.0.1", "65536", out host, out error));
        False(ProxyHost.TryCreate("x", "主机", "127.0.0.1", "0", out host, out error));
        False(ProxyHost.TryCreate("x", "主机", "host:7890", "7890", out host, out error));
        Equal(true, ProxyHost.TryCreate("x", "IPv6", "::1", "7890", out host, out error));
        Equal("[::1]:7890", host.Endpoint.GetServerAddress());
        Equal(true, ProxyHost.TryCreate("x", "IPv6", "[::1]", "7890", out host, out error));
        Equal("[::1]:7890", host.Endpoint.GetServerAddress());
    }

    private static void OtherProxyProcessesRecognized()
    {
        foreach (string name in new string[] { "clash-verge", "Clash Verge Service", "clash_party", "FlClash", "mihomo", "verge-mihomo-alpha" })
            Equal(true, OtherProxyDetector.IsProxyProcess(name));
        False(OtherProxyDetector.IsProxyProcess("ProxyShare"));
        False(OtherProxyDetector.IsProxyProcess("explorer"));
    }

    private static void GoogleProbeMeasuresResponseDelay()
    {
        using (FakeGoogleProxy proxy = new FakeGoogleProxy(204, 180))
        {
            int latency = ProxyHostProbe.Measure(proxy.Endpoint, 2000);
            if (latency < 150) throw new Exception("延迟必须包含 Google 响应等待，实际仅 " + latency + " ms");
            Equal(true, proxy.RequestReceived.WaitOne(1000));
            Equal("GET http://www.google.com/generate_204 HTTP/1.1", proxy.RequestLine);
        }
    }

    private static void GoogleProbeRejectsUnexpectedResponse()
    {
        foreach (int status in new int[] { 200, 302, 502 })
        {
            using (FakeGoogleProxy proxy = new FakeGoogleProxy(status, 0))
            {
                bool rejected = false;
                try { ProxyHostProbe.Measure(proxy.Endpoint, 2000); }
                catch (System.Net.WebException) { rejected = true; }
                catch (InvalidOperationException) { rejected = true; }
                Equal(true, rejected);
                Equal(true, proxy.RequestReceived.WaitOne(1000));
                Equal("GET http://www.google.com/generate_204 HTTP/1.1", proxy.RequestLine);
            }
        }
    }

    private static void GoogleProbeTimesOut()
    {
        using (FakeGoogleProxy proxy = new FakeGoogleProxy(204, 500))
        {
            bool timedOut = false;
            try { ProxyHostProbe.Measure(proxy.Endpoint, 100); }
            catch (System.Net.WebException ex) { timedOut = ex.Status == System.Net.WebExceptionStatus.Timeout; }
            Equal(true, timedOut);
            Equal(true, proxy.RequestReceived.WaitOne(1000));
        }
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

    private static void ProxyEndpointComparison()
    {
        ProxyConfiguration configuration = Disabled("old:80");
        configuration.UseManualProxy("proxy.local:7897", null);

        True(configuration.UsesEndpoint(new ProxyEndpoint("PROXY.LOCAL", 7897)));
        False(configuration.UsesEndpoint(new ProxyEndpoint("proxy.local", 8080)));
        Equal("[2001:db8::1]:7897", new ProxyEndpoint("2001:db8::1", 7897).GetServerAddress());
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

// A local forward-proxy fixture verifies the actual Google request without internet access.
internal sealed class FakeGoogleProxy : IDisposable
{
    private readonly System.Net.Sockets.TcpListener listener;
    private readonly System.Threading.Thread worker;
    public readonly ProxyEndpoint Endpoint;
    public readonly System.Threading.ManualResetEvent RequestReceived = new System.Threading.ManualResetEvent(false);
    public string RequestLine;

    public FakeGoogleProxy(int status, int delay)
    {
        listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        Endpoint = new ProxyEndpoint("127.0.0.1", ((System.Net.IPEndPoint)listener.LocalEndpoint).Port);
        worker = new System.Threading.Thread(delegate()
        {
            try
            {
                using (System.Net.Sockets.TcpClient client = listener.AcceptTcpClient())
                using (System.Net.Sockets.NetworkStream stream = client.GetStream())
                {
                    stream.ReadTimeout = 2000;
                    stream.WriteTimeout = 2000;
                    System.IO.StreamReader reader = new System.IO.StreamReader(stream);
                    RequestLine = reader.ReadLine();
                    string line;
                    while (!string.IsNullOrEmpty(line = reader.ReadLine())) { }
                    RequestReceived.Set();
                    System.Threading.Thread.Sleep(delay);
                    byte[] response = System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 " + status +
                        " Test\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                    stream.Write(response, 0, response.Length);
                }
            }
            catch (System.IO.IOException) { }
            catch (System.Net.Sockets.SocketException) { }
            catch (ObjectDisposedException) { }
        });
        worker.IsBackground = true;
        worker.Start();
    }

    public void Dispose()
    {
        listener.Stop();
        if (worker.Join(3000)) RequestReceived.Dispose();
    }
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
    public Action<ProxyConfiguration> AfterRead;
    public FakeProxyTakeoverStore(ProxyConfiguration current) { Current = current.Clone(); }
    public ProxyConfiguration ReadCurrent()
    {
        ReadCount++;
        if (FailNextRead || ReadCount == FailReadNumber) { FailNextRead = false; throw new InvalidOperationException("模拟读取失败"); }
        ProxyConfiguration read = Current.Clone();
        if (AfterRead != null) AfterRead(read);
        return read;
    }
    public void WriteCurrent(ProxyConfiguration value) { if (WritesToFail > 0) { WritesToFail--; throw new InvalidOperationException("模拟写入失败"); } if (FailNextWrite) { FailNextWrite = false; throw new InvalidOperationException("模拟写入失败"); } if (IgnoreNextWrite) { IgnoreNextWrite = false; return; } Current = value.Clone(); }
    public void NotifyChanged() { if (FailNextNotify) { FailNextNotify = false; throw new InvalidOperationException("模拟系统刷新通知失败"); } }
    public bool TryLoad(out ProxyConfiguration snapshot, out ProxyConfiguration managed, out bool cleanExit) { if (FailNextLoad) { FailNextLoad = false; throw new InvalidOperationException("模拟接管状态读取失败"); } snapshot = Snapshot == null ? null : Snapshot.Clone(); managed = Managed == null ? null : Managed.Clone(); cleanExit = CleanExit; return HasTakeover; }
    public void Save(ProxyConfiguration snapshot, ProxyConfiguration managed) { if (FailNextSave) { FailNextSave = false; throw new InvalidOperationException("模拟快照保存失败"); } Snapshot = snapshot.Clone(); Managed = managed.Clone(); HasTakeover = true; CleanExit = false; }
    public void Clear() { if (FailNextClear) { FailNextClear = false; throw new InvalidOperationException("模拟快照清理失败"); } HasTakeover = false; CleanExit = true; Snapshot = null; Managed = null; }
    public void MarkCleanExit(bool clean) { if (FailNextMarkCleanExit) { FailNextMarkCleanExit = false; throw new InvalidOperationException("模拟退出状态保存失败"); } if (HasTakeover) CleanExit = clean; }
}
