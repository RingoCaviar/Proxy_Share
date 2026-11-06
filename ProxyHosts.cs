using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.IO;

internal sealed class ProxyHost
{
    public readonly string Id;
    public readonly string Name;
    public readonly ProxyEndpoint Endpoint;
    public bool IsBuiltIn { get { return IsBuiltInId(Id); } }
    public static bool IsBuiltInId(string id)
    {
        return string.Equals(id, "yifan", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(id, "zhaobo", StringComparison.OrdinalIgnoreCase);
    }

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
    private bool hasCustomHosts;
    public readonly List<ProxyHost> Hosts = new List<ProxyHost>();
    public string SelectedId;
    public ProxyHost Selected
    {
        get { return Hosts.Find(delegate(ProxyHost host) { return host.Id == SelectedId; }); }
    }

    public static ProxyHostCatalog Defaults()
    {
        ProxyHostCatalog catalog = new ProxyHostCatalog();
        catalog.Hosts.Add(new ProxyHost("yifan", "瘦羊羊", new ProxyEndpoint("192.168.1.72", 7897)));
        catalog.Hosts.Add(new ProxyHost("zhaobo", "David·博", new ProxyEndpoint("192.168.1.4", 7892)));
        catalog.SelectedId = "zhaobo";
        return catalog;
    }

    public string[] Serialize()
    {
        List<string> rows = new List<string>();
        rows.Add(SelectedId);
        foreach (ProxyHost host in Hosts)
        {
            if (host.IsBuiltIn) continue;
            rows.Add(host.Id + "|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(host.Name)) +
                "|" + host.Endpoint.Address + "|" + host.Endpoint.Port);
        }
        return rows.ToArray();
    }

    public static ProxyHostCatalog Parse(string[] rows)
    {
        if (rows == null || rows.Length < 1) throw new FormatException("主机列表为空或格式不正确。");
        if (rows.Length == 1 && !ProxyHost.IsBuiltInId(rows[0])) throw new FormatException("主机配置格式不正确。");
        ProxyHostCatalog catalog = Defaults();
        catalog.SelectedId = rows[0];
        for (int i = 1; i < rows.Length; i++)
        {
            string[] fields = rows[i].Split('|');
            if (fields.Length == 4 && ProxyHost.IsBuiltInId(fields[0])) continue;
            ProxyHost host;
            string error;
            if (fields.Length != 4 || fields[0].Length == 0 ||
                catalog.Hosts.Exists(delegate(ProxyHost item) { return item.Id == fields[0]; }))
                throw new FormatException("主机数据格式不正确。");
            if (!ProxyHost.TryCreate(fields[0], Encoding.UTF8.GetString(Convert.FromBase64String(fields[1])),
                fields[2], fields[3], out host, out error)) throw new FormatException(error);
            catalog.Hosts.Add(host);
        }
        if (catalog.Selected == null) catalog.SelectedId = Defaults().SelectedId;
        return catalog;
    }

    public static ProxyHostCatalog Load(out string warning)
    {
        try { return Load(ApplicationDataFiles.HostsPath, out warning); }
        catch (Exception ex)
        {
            warning = "无法定位主机配置文件，暂用内置主机：" + ex.Message;
            return Defaults();
        }
    }

    internal static ProxyHostCatalog Load(string path, out string warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(path)) return LoadLegacyFiles(path, out warning);
            ProxyHostCatalog catalog = Parse(File.ReadAllLines(path, Encoding.UTF8));
            catalog.hasCustomHosts = true;
            return catalog;
        }
        catch (Exception ex)
        {
            warning = "读取主机列表失败，暂用内置主机：" + ex.Message;
            return Defaults();
        }
    }

    public void Save()
    {
        Save(ApplicationDataFiles.HostsPath);
    }

    private static ProxyHostCatalog LoadLegacyFiles(string path, out string warning)
    {
        warning = null;
        ProxyHostCatalog merged = Defaults();
        string directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!Directory.Exists(directory)) return merged;
        string[] files = Directory.GetFiles(directory, "hosts-*.txt");
        Array.Sort(files, delegate(string a, string b) { return File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)); });
        foreach (string file in files)
        {
            ProxyHostCatalog candidate;
            try
            {
                candidate = Parse(File.ReadAllLines(file, Encoding.UTF8));
            }
            catch (Exception ex) { warning = "部分旧主机文件读取失败：" + ex.Message; continue; }
            candidate.Save(path);
            return candidate;
        }
        return merged;
    }

    internal void Save(string path)
    {
        ApplicationDataFiles.Write(path, Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, Serialize())));
        hasCustomHosts = true;
    }

    public void SaveSelection()
    {
        if (hasCustomHosts) Save();
    }
}

internal static class ApplicationDataFiles
{
    public static string DirectoryPath
    {
        get
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(documents)) throw new IOException("无法定位用户文档目录。");
            return Path.Combine(documents, "ProxyShare");
        }
    }

    public static string HostsPath { get { return Path.Combine(DirectoryPath, "custom-hosts.txt"); } }
    public static string TakeoverPath { get { return Path.Combine(DirectoryPath, "takeover.bin"); } }

    public static void Write(string path, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(data, 0, data.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

internal static class ProxyHostProbe
{
    // Measure an actual Google request through this endpoint, independently of system proxy state.
    public static int Measure(ProxyEndpoint endpoint, int timeout)
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://www.google.com/generate_204");
        request.Proxy = new WebProxy(endpoint.Address, endpoint.Port);
        request.Timeout = timeout;
        request.ReadWriteTimeout = timeout;
        request.AllowAutoRedirect = false;
        request.KeepAlive = false;
        request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.NoContent)
                    throw new InvalidOperationException("Google 未返回预期的 204 响应。");
                return (int)Math.Max(1, watch.ElapsedMilliseconds);
            }
        }
        catch (WebException ex)
        {
            if (ex.Response != null) ex.Response.Close();
            throw;
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
