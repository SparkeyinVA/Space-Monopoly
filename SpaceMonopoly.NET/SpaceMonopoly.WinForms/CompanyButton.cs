namespace SpaceMonopoly.WinForms;

/// <summary>
/// A company button that always shows the share price: the name is shortened with "…" rather than
/// wrapping onto the price's line. With <see cref="TwoLines"/> the price sits under the name,
/// otherwise it is drawn at the right-hand end.
/// </summary>
internal sealed class CompanyButton : Button
{
    private string _companyName = "";
    private string _price = "";
    private bool _twoLines;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool TwoLines
    {
        get => _twoLines;
        set { if (_twoLines == value) return; _twoLines = value; Invalidate(); }
    }

    public void SetText(string name, string price)
    {
        if (name == _companyName && price == _price) return;
        _companyName = name;
        _price = price;
        AccessibleName = price.Length == 0 ? name : $"{name}, {price}";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);   // Text is left empty, so this draws just the button

        var color = Enabled ? ForeColor : SystemColors.GrayText;
        int pad = LogicalToDeviceUnits(6);
        var area = Rectangle.Inflate(ClientRectangle, -pad, -LogicalToDeviceUnits(3));
        const TextFormatFlags Line = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;

        if (_price.Length == 0)
        {
            TextRenderer.DrawText(e.Graphics, _companyName, Font, area, color, Line | TextFormatFlags.VerticalCenter);
            return;
        }
        if (TwoLines)
        {
            int half = area.Height / 2;
            var top = new Rectangle(area.Left, area.Top, area.Width, half);
            var bottom = new Rectangle(area.Left, area.Top + half, area.Width, area.Height - half);
            TextRenderer.DrawText(e.Graphics, _companyName, Font, top, color, Line | TextFormatFlags.Bottom);
            TextRenderer.DrawText(e.Graphics, _price, Font, bottom, color, Line | TextFormatFlags.Top);
            return;
        }
        int priceWidth = TextRenderer.MeasureText(e.Graphics, _price, Font, area.Size, Line).Width;
        var nameArea = new Rectangle(area.Left, area.Top, Math.Max(0, area.Width - priceWidth - pad), area.Height);
        TextRenderer.DrawText(e.Graphics, _companyName, Font, nameArea, color, Line | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics, _price, Font, area, color, Line | TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
    }
}
