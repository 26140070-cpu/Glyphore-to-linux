namespace Glyphore;





internal sealed class DetachedWindowManager : IDisposable
{
    private readonly Form _mainWindow;
    private readonly Dictionary<string, Form> _windows = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposing;

    public DetachedWindowManager(Form mainWindow)
    {
        _mainWindow = mainWindow;
        _mainWindow.FormClosing += MainWindowOnFormClosing;
    }

    public bool TryActivate(string key)
    {
        if (!_windows.TryGetValue(key, out Form? window)) return false;
        if (window.IsDisposed)
        {
            _windows.Remove(key);
            return false;
        }

        if (window.WindowState == FormWindowState.Minimized)
            window.WindowState = FormWindowState.Normal;
        window.Show();
        window.BringToFront();
        window.Activate();
        return true;
    }

    public T ShowSingle<T>(string key, Func<T> factory, bool centerOnMain = true) where T : Form
    {
        if (TryActivate(key) && _windows[key] is T existing) return existing;
        T window = factory();
        ShowSingle(key, window, centerOnMain);
        return window;
    }

    public void ShowSingle(string key, Form window, bool centerOnMain = true)
    {
        if (TryActivate(key))
        {
            window.Dispose();
            return;
        }



        window.ShowInTaskbar = true;
        if (centerOnMain) PositionNearMain(window);

        _windows[key] = window;
        window.FormClosed += (_, _) =>
        {
            if (_windows.TryGetValue(key, out Form? current) && ReferenceEquals(current, window))
                _windows.Remove(key);
        };

        window.Show();
        window.BringToFront();
        window.Activate();
    }

    private void PositionNearMain(Form window)
    {
        window.StartPosition = FormStartPosition.Manual;
        Rectangle work = new Rectangle(0, 0, Math.Max(1, _mainWindow.Bounds.Width), Math.Max(1, _mainWindow.Bounds.Height));
        Rectangle basis = _mainWindow.WindowState == FormWindowState.Minimized ? work : _mainWindow.Bounds;
        int x = basis.Left + (basis.Width - window.Width) / 2;
        int y = basis.Top + (basis.Height - window.Height) / 2;
        x = Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - window.Width));
        y = Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - window.Height));
        window.Location = new Point(x, y);
    }

    private void MainWindowOnFormClosing(object? sender, FormClosingEventArgs e)
    {
        CloseAll();
    }

    private void CloseAll()
    {
        if (_disposing) return;
        _disposing = true;
        try
        {
            foreach (Form window in _windows.Values.ToArray())
            {
                try
                {
                    if (!window.IsDisposed) window.Close();
                }
                catch
                {
                    try { window.Dispose(); } catch { }
                }
            }
            _windows.Clear();
        }
        finally
        {
            _disposing = false;
        }
    }

    public void Dispose()
    {
        _mainWindow.FormClosing -= MainWindowOnFormClosing;
        CloseAll();
    }
}
