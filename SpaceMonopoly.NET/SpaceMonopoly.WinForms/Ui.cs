namespace SpaceMonopoly.WinForms;

/// <summary>Small helpers for building the code-only forms consistently.</summary>
internal static class Ui
{
    /// <summary>
    /// Creates a dialog with layout suspended. Callers add controls, then call ResumeLayout(false) before
    /// showing it, which is when Windows applies high-DPI scaling.
    /// </summary>
    public static Form Dialog(string title, int width, int height)
    {
        var form = new Form();
        form.SuspendLayout();
        form.Text = title;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.CenterParent;
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.AutoScaleDimensions = new SizeF(96f, 96f);
        form.ClientSize = new Size(width, height);
        form.Font = new Font("Segoe UI", 9f);
        return form;
    }

    public static Button Button(string text, EventHandler? onClick = null, int width = 100)
    {
        // Fixed logical sizes: auto-sized buttons end up scaled twice on high-DPI screens.
        var b = new Button { Text = text, Size = new Size(width, 30), Margin = new Padding(4) };
        if (onClick is not null) b.Click += onClick;
        return b;
    }

    public static Label Label(string text, float size = 9f, FontStyle style = FontStyle.Regular) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", size, style),
        Margin = new Padding(4),
    };

    public static ListView Table(params (string Header, int Width)[] columns)
    {
        var list = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Dock = DockStyle.Fill,
            MultiSelect = false,
            HideSelection = false,
        };
        foreach (var (header, width) in columns)
            list.Columns.Add(header, width).Tag = width;

        // Column widths aren't scaled with the form, so convert them once the control knows its DPI.
        list.HandleCreated += (_, _) =>
        {
            foreach (ColumnHeader column in list.Columns)
                if (column.Tag is int logical) column.Width = list.LogicalToDeviceUnits(logical);
        };
        return list;
    }

    public static ListViewItem Row(params string[] cells) => new(cells);

    /// <summary>A row of buttons aligned to the right, as at the bottom of a dialog.</summary>
    public static FlowLayoutPanel ButtonBar(params Control[] buttons)
    {
        var bar = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(8),
        };
        bar.Controls.AddRange(buttons);
        return bar;
    }
}
