using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Win32;

internal sealed class ProxyHost
{
    public readonly string Id;
    public readonly string Name;
    public readonly ProxyEndpoint Endpoint;

    public ProxyHost(string id, string name, ProxyEndpoint endpoint)
    {
        Id = id;
        Name = name;
        Endpoint = endpoint;
    }

    public override string ToString() { return Name + "  ·  " + Endpoint.GetServerAddress(); }

    public static bool TryCreate(string id, string name, string address, string portText,
        out ProxyHost host, out string error)
    {
        host = null;
        name = (name ?? "").Trim();
        address = (address ?? "").Trim();
        int port;
        error = null;
        if (name.Length == 0 || name.Length > 60) error = "主机名称应为 1–60 个字符。";
        else if (!IsValidAddress(address)) error = "请输入合法的 IP 地址或主机名，不要包含协议或路径。";
        else if (!int.TryParse(portText, out port) || port < 1 || port > 65535)
            error = "端口必须是 1–65535 之间的整数。";
        else
        {
            IPAddress parsed;
            if (IPAddress.TryParse(address, out parsed)) address = parsed.ToString();
            host = new ProxyHost(id, name, new ProxyEndpoint(address, port));
        }
        return error == null;
    }

    private static bool IsValidAddress(string address)
    {
        if (address.Length == 0 || address.IndexOfAny(new char[] { '/', '\\', ' ', '\t', '\r', '\n', '@' }) >= 0) return false;
        IPAddress parsed;
        if (IPAddress.TryParse(address, out parsed)) return true;
        if (address.Length > 253 || address.StartsWith(".") || address.EndsWith(".")) return false;
        foreach (string label in address.Split('.'))
        {
            if (label.Length == 0 || label.Length > 63 || label[0] == '-' || label[label.Length - 1] == '-') return false;
            foreach (char c in label)
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-')) return false;
        }
        return true;
    }
}

internal sealed class ProxyHostCatalog
{
    public readonly List<ProxyHost> Hosts = new List<ProxyHost>();
    public string SelectedId;
    public ProxyHost Selected
    {
        get { return Hosts.Find(delegate(ProxyHost host) { return host.Id == SelectedId; }); }
    }

    public static ProxyHostCatalog Defaults()
    {
        ProxyHostCatalog catalog = new ProxyHostCatalog();
        catalog.Hosts.Add(new ProxyHost("yifan", "一帆", new ProxyEndpoint("192.168.1.72", 7897)));
        catalog.Hosts.Add(new ProxyHost("zhaobo", "赵博", new ProxyEndpoint("192.168.1.4", 7892)));
        catalog.SelectedId = "zhaobo";
        return catalog;
    }

    public string[] Serialize()
    {
        List<string> rows = new List<string>();
        rows.Add(SelectedId);
        foreach (ProxyHost host in Hosts)
            rows.Add(host.Id + "|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(host.Name)) +
                "|" + host.Endpoint.Address + "|" + host.Endpoint.Port);
        return rows.ToArray();
    }

    public static ProxyHostCatalog Parse(string[] rows)
    {
        if (rows == null || rows.Length < 2) throw new FormatException("主机列表为空或格式不正确。");
        ProxyHostCatalog catalog = new ProxyHostCatalog();
        catalog.SelectedId = rows[0];
        for (int i = 1; i < rows.Length; i++)
        {
            string[] fields = rows[i].Split('|');
            ProxyHost host;
            string error;
            if (fields.Length != 4 || fields[0].Length == 0 ||
                catalog.Hosts.Exists(delegate(ProxyHost item) { return item.Id == fields[0]; }))
                throw new FormatException("主机数据格式不正确。");
            if (!ProxyHost.TryCreate(fields[0], Encoding.UTF8.GetString(Convert.FromBase64String(fields[1])),
                fields[2], fields[3], out host, out error)) throw new FormatException(error);
            catalog.Hosts.Add(host);
        }
        if (catalog.Selected == null) catalog.SelectedId = catalog.Hosts[0].Id;
        return catalog;
    }

    public static ProxyHostCatalog Load(out string warning)
    {
        warning = null;
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\ProxyShare"))
            {
                if (key == null) return Defaults();
                object saved = key.GetValue("HostsV1");
                if (saved != null) return Parse(saved as string[]);
                ProxyHostCatalog catalog = Defaults();
                // Migrate the previously saved endpoint without losing either built-in host.
                string address = Convert.ToString(key.GetValue("LastAddress", ""));
                string port = Convert.ToString(key.GetValue("LastPort", ""));
                ProxyHost legacy;
                string error;
                if (ProxyHost.TryCreate("legacy", "原有代理", address, port, out legacy, out error))
                {
                    ProxyHost match = catalog.Hosts.Find(delegate(ProxyHost host) {
                        return host.Endpoint.GetServerAddress() == legacy.Endpoint.GetServerAddress(); });
                    if (match == null) catalog.Hosts.Add(legacy);
                    catalog.SelectedId = match == null ? legacy.Id : match.Id;
                }
                return catalog;
            }
        }
        catch (Exception ex)
        {
            warning = "读取主机列表失败，暂用内置主机：" + ex.Message;
            return Defaults();
        }
    }

    public void Save()
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\ProxyShare"))
            key.SetValue("HostsV1", Serialize(), RegistryValueKind.MultiString);
    }
}

internal static class ProxyHostProbe
{
    // TCP connection time to the actual proxy port; ICMP may be blocked on a working host.
    public static int Measure(ProxyEndpoint endpoint, int timeout)
    {
        using (TcpClient client = new TcpClient())
        {
            Stopwatch watch = Stopwatch.StartNew();
            IAsyncResult connect = client.BeginConnect(endpoint.Address, endpoint.Port, null, null);
            using (System.Threading.WaitHandle wait = connect.AsyncWaitHandle)
            {
                if (!wait.WaitOne(timeout)) throw new TimeoutException("连接超时");
                client.EndConnect(connect);
            }
            return (int)Math.Max(1, watch.ElapsedMilliseconds);
        }
    }
}

internal static class OtherProxyDetector
{
    public static bool IsProxyProcess(string name)
    {
        string normalized = name.ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "").Replace(".", "");
        return normalized.Contains("clashverge") || normalized.Contains("clashparty") ||
            normalized.Contains("flclash") || normalized.Contains("mihomo");
    }

    public static string[] FindRunning()
    {
        List<string> names = new List<string>();
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    string name = process.ProcessName;
                    if (IsProxyProcess(name) && !names.Contains(name)) names.Add(name);
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }
        return names.ToArray();
    }
}
