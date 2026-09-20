namespace Glyphore;

internal sealed class BrandMarkControl : Control
{
    private readonly Image? _brandImage;
    private readonly Icon? _appIcon;
    private int _logicalWidth = 44;
    private int _logicalHeight = 44;
    private int _logicalLeft;
    private int _logicalTop;
    private AnchorStyles _logicalAnchor = AnchorStyles.Top | AnchorStyles.Left;

    public new AnchorStyles Anchor
    {
        get => LinuxControlInitialization.IsHandleCreated(this) ? base.Anchor : _logicalAnchor;
        set
        {
            _logicalAnchor = value;
            if (!LinuxControlInitialization.IsHandleCreated(this)) return;
            try { base.Anchor = value; } catch (Exception ex) { LinuxDiagnostics.Exception($"{GetType().Name}.Anchor", ex); }
        }
    }

    public new int Left
    {
        get => LinuxControlInitialization.IsHandleCreated(this) ? base.Left : _logicalLeft;
        set
        {
            _logicalLeft = value;
            if (!LinuxControlInitialization.IsHandleCreated(this)) return;
            try { base.Left = value; } catch (Exception ex) { LinuxDiagnostics.Exception($"{GetType().Name}.Left", ex); }
        }
    }

    public new int Top
    {
        get => LinuxControlInitialization.IsHandleCreated(this) ? base.Top : _logicalTop;
        set
        {
            _logicalTop = value;
            if (!LinuxControlInitialization.IsHandleCreated(this)) return;
            try { base.Top = value; } catch (Exception ex) { LinuxDiagnostics.Exception($"{GetType().Name}.Top", ex); }
        }
    }

    public new int Width
    {
        get
        {
            if (!LinuxControlInitialization.IsHandleCreated(this)) return _logicalWidth;
            try { return base.Width; } catch { return _logicalWidth; }
        }
        set
        {
            _logicalWidth = Math.Max(0, value);
            if (!LinuxControlInitialization.IsHandleCreated(this)) return;
            try { base.Width = _logicalWidth; } catch (Exception ex) { LinuxDiagnostics.Exception($"{GetType().Name}.Width", ex); }
        }
    }

    public new int Height
    {
        get
        {
            if (!LinuxControlInitialization.IsHandleCreated(this)) return _logicalHeight;
            try { return base.Height; } catch { return _logicalHeight; }
        }
        set
        {
            _logicalHeight = Math.Max(0, value);
            if (!LinuxControlInitialization.IsHandleCreated(this)) return;
            try { base.Height = _logicalHeight; } catch (Exception ex) { LinuxDiagnostics.Exception($"{GetType().Name}.Height", ex); }
        }
    }

    private void ApplyLogicalSize(object? sender, EventArgs e)
    {
        try
        {
            base.Size = new Size(_logicalWidth, _logicalHeight);
            base.Location = new Point(_logicalLeft, _logicalTop);
            base.Anchor = _logicalAnchor;
        }
        catch (Exception ex) { LinuxDiagnostics.Exception($"{GetType().Name}.HandleCreated.Size", ex); }
    }

    protected override Size DefaultSize => new(44, 44);

    public BrandMarkControl()
    {
        HandleCreated += ApplyLogicalSize;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        LinuxControlInitialization.Defer(this, () => BackColor = Color.Transparent, nameof(BackColor));

        try
        {
            using Stream? stream = typeof(BrandMarkControl).Assembly.GetManifestResourceStream("Glyphore.BrandIcon.png");
            if (stream is not null)
            {
                using Image source = Image.FromStream(stream, useEmbeddedColorManagement: true, validateImageData: true);
                _brandImage = new Bitmap(source);
            }
        }
        catch
        {
            _brandImage = null;
        }

        // The embedded PNG is the portable source. Avoid platform-specific executable icon extraction.
        _appIcon = null;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = Majorsilence.Forms.Drawing.Drawing2D.SmoothingMode.HighQuality;
        e.Graphics.InterpolationMode = Majorsilence.Forms.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = Majorsilence.Forms.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

        int side = Math.Max(1, Math.Min(Width, Height) - 2);
        var rect = new Rectangle((Width - side) / 2, (Height - side) / 2, side, side);
        if (_brandImage is not null)
        {
            e.Graphics.DrawImage(_brandImage, rect);
            return;
        }
        if (_appIcon is not null)
        {
            e.Graphics.DrawIcon(_appIcon, rect);
            return;
        }

        using var bg = new SolidBrush(Theme.BrandSurface);
        using var accent = new Pen(Theme.Accent, 3.2f);
        using var path = Theme.RoundedRect(rect, Math.Max(4, rect.Width / 5));
        e.Graphics.FillPath(bg, path);
        e.Graphics.DrawPath(accent, path);
        using var font = new Font("Segoe UI", Math.Max(9f, Height * .45f), FontStyle.Bold, GraphicsUnit.Pixel);
        TextRenderer.DrawText(e.Graphics, "G", font, rect, Theme.AccentText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _brandImage?.Dispose();
            _appIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
