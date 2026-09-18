namespace Glyphore;

internal class SafeGeometryControl : Control
{
    private int _pendingWidth = 100;
    private int _pendingHeight = 23;
    private readonly System.Windows.Forms.Timer _syncTimer;

    public SafeGeometryControl()
    {
        _syncTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _syncTimer.Tick += (_, _) => TrySyncSize();
        _syncTimer.Start();
        Disposed += (_, _) => _syncTimer.Dispose();
    }

    public new int Width
    {
        get => _pendingWidth;
        set
        {
            _pendingWidth = value;
            TrySyncSize();
        }
    }

    public new int Height
    {
        get => _pendingHeight;
        set
        {
            _pendingHeight = value;
            TrySyncSize();
        }
    }

    public new Size Size
    {
        get => new Size(_pendingWidth, _pendingHeight);
        set
        {
            _pendingWidth = value.Width;
            _pendingHeight = value.Height;
            TrySyncSize();
        }
    }

    private void TrySyncSize()
    {
        try
        {
            base.Size = new Size(_pendingWidth, _pendingHeight);
            _syncTimer.Stop();
        }
        catch (NullReferenceException)
        {
        }
    }
}

internal class SafeGeometryUserControl : UserControl
{
    private int _pendingWidth = 100;
    private int _pendingHeight = 23;
    private readonly System.Windows.Forms.Timer _syncTimer;

    public SafeGeometryUserControl()
    {
        _syncTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _syncTimer.Tick += (_, _) => TrySyncSize();
        _syncTimer.Start();
        Disposed += (_, _) => _syncTimer.Dispose();
    }

    public new int Width
    {
        get => _pendingWidth;
        set
        {
            _pendingWidth = value;
            TrySyncSize();
        }
    }

    public new int Height
    {
        get => _pendingHeight;
        set
        {
            _pendingHeight = value;
            TrySyncSize();
        }
    }

    public new Size Size
    {
        get => new Size(_pendingWidth, _pendingHeight);
        set
        {
            _pendingWidth = value.Width;
            _pendingHeight = value.Height;
            TrySyncSize();
        }
    }

    private void TrySyncSize()
    {
        try
        {
            base.Size = new Size(_pendingWidth, _pendingHeight);
            _syncTimer.Stop();
        }
        catch (NullReferenceException)
        {
        }
    }
}
