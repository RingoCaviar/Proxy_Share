using System;
using System.Drawing;
using System.Windows.Forms;

internal sealed class ProxyHostEditor : Form
{
    private readonly DataGridView grid;
    private readonly Label errorLabel;
    private readonly string selectedId;
    private readonly Label emptyHint;
    private readonly Button emptyAdd;
    public ProxyHostCatalog Catalog { get; private set; }

    public ProxyHostEditor(ProxyHostCatalog catalog, ThemePalette palette)
    {
        selectedId = catalog.SelectedId;
        Text = "编辑代理主机";
        ClientSize = new Size(600, 360);
        MinimumSize = new Size(560, 340);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9F);
        BackColor = palette.Window;
        ForeColor = palette.Text;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;

        Label hint = new Label();
        hint.Text = "在此添加、编辑或删除自己的主机；内置主机在主界面选择。";
        hint.SetBounds(16, 14, 560, 24);
        hint.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        Controls.Add(hint);

        grid = new DataGridView();
        grid.SetBounds(16, 44, 568, 220);
        grid.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.BackgroundColor = palette.Card;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.GridColor = palette.Border;
        grid.DefaultCellStyle.BackColor = palette.Input;
        grid.DefaultCellStyle.ForeColor = palette.Text;
        grid.DefaultCellStyle.SelectionBackColor = palette.Accent;
        grid.DefaultCellStyle.SelectionForeColor = palette.AccentText;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = palette.Card;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = palette.Text;
        grid.RowHeadersVisible = false;
        grid.MultiSelect = false;
        grid.RowTemplate.Height = 30;
        grid.Columns.Add("Name", "主机名称");
        grid.Columns.Add("Address", "代理地址");
        grid.Columns.Add("Port", "端口");
        grid.Columns[1].FillWeight = 150;
        grid.Columns[2].FillWeight = 65;
        foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        foreach (ProxyHost host in catalog.Hosts)
        {
            if (host.IsBuiltIn) continue;
            int row = grid.Rows.Add(host.Name, host.Endpoint.Address, host.Endpoint.Port);
            grid.Rows[row].Tag = host.Id;
        }
        Controls.Add(grid);

        emptyHint = new Label();
        emptyHint.Text = "尚未添加自定义主机\r\n添加常用代理后，可在主界面快速切换。";
        emptyHint.TextAlign = ContentAlignment.MiddleCenter;
        emptyHint.ForeColor = palette.MutedText;
        emptyHint.BackColor = palette.Card;
        emptyHint.AutoSize = false;
        emptyHint.Size = new Size(420, 52);
        grid.Controls.Add(emptyHint);
        emptyAdd = new Button();
        emptyAdd.Text = "添加主机";
        emptyAdd.Size = new Size(110, 32);
        emptyAdd.FlatStyle = FlatStyle.Flat;
        emptyAdd.FlatAppearance.BorderColor = palette.Border;
        emptyAdd.BackColor = palette.Card;
        emptyAdd.ForeColor = palette.Text;
        grid.Controls.Add(emptyAdd);
        grid.Resize += delegate { UpdateEmptyState(); };
        grid.RowsAdded += delegate { UpdateEmptyState(); };
        grid.RowsRemoved += delegate { UpdateEmptyState(); };

        errorLabel = new Label();
        errorLabel.ForeColor = palette.Error;
        errorLabel.SetBounds(16, 272, 568, 36);
        errorLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        Controls.Add(errorLabel);
        Button add = MakeButton("添加", 16, palette);
        add.Click += delegate
        {
            int row = grid.Rows.Add("新主机", "", "7890");
            grid.Rows[row].Tag = Guid.NewGuid().ToString("N");
            grid.CurrentCell = grid.Rows[row].Cells[0];
            grid.BeginEdit(true);
        };
        emptyAdd.Click += delegate { add.PerformClick(); };
        UpdateEmptyState();
        Button remove = MakeButton("删除", 104, palette);
        grid.SelectionChanged += delegate
        {
            remove.Enabled = grid.CurrentRow != null && !ProxyHost.IsBuiltInId(Convert.ToString(grid.CurrentRow.Tag));
        };
        remove.Enabled = grid.CurrentRow != null && !ProxyHost.IsBuiltInId(Convert.ToString(grid.CurrentRow.Tag));
        remove.Click += delegate
        {
            if (grid.CurrentRow == null) return;
            if (ProxyHost.IsBuiltInId(Convert.ToString(grid.CurrentRow.Tag))) return;
            grid.Rows.Remove(grid.CurrentRow);
            errorLabel.Text = "";
        };
        Button save = MakeButton("保存", 408, palette);
        save.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        save.Click += SaveClick;
        Button cancel = MakeButton("取消", 496, palette);
        cancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        cancel.DialogResult = DialogResult.Cancel;
        CancelButton = cancel;
    }

    private Button MakeButton(string text, int x, ThemePalette palette)
    {
        Button button = new Button();
        button.Text = text;
        button.SetBounds(x, 312, 80, 32);
        button.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = palette.Border;
        button.BackColor = palette.Card;
        button.ForeColor = palette.Text;
        Controls.Add(button);
        return button;
    }

    private void UpdateEmptyState()
    {
        if (emptyHint == null || emptyAdd == null) return;
        bool empty = grid.Rows.Count == 0;
        emptyHint.Visible = empty;
        emptyAdd.Visible = empty;
        emptyHint.SetBounds(12, Math.Max(30, (grid.Height - 84) / 2), grid.Width - 24, 52);
        emptyAdd.Location = new Point((grid.Width - emptyAdd.Width) / 2, emptyHint.Bottom + 4);
    }

    private void SaveClick(object sender, EventArgs e)
    {
        grid.EndEdit();
        ProxyHostCatalog catalog = ProxyHostCatalog.Defaults();
        catalog.SelectedId = selectedId;
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (ProxyHost.IsBuiltInId(Convert.ToString(row.Tag))) continue;
            ProxyHost host;
            string error;
            if (!ProxyHost.TryCreate((string)row.Tag, Convert.ToString(row.Cells[0].Value),
                Convert.ToString(row.Cells[1].Value), Convert.ToString(row.Cells[2].Value), out host, out error))
            {
                grid.CurrentCell = row.Cells[0];
                errorLabel.Text = "第 " + (row.Index + 1) + " 行：" + error;
                return;
            }
            catalog.Hosts.Add(host);
        }
        if (catalog.Selected == null) catalog.SelectedId = ProxyHostCatalog.Defaults().SelectedId;
        try { catalog.Save(); }
        catch (Exception ex) { errorLabel.Text = "保存失败，请重试：" + ex.Message; return; }
        Catalog = catalog;
        DialogResult = DialogResult.OK;
    }
}
