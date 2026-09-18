# Glyphoré 6.0.1 Linux — build fix for GTK WinForms layer

This revision addresses the GTKSystem.Windows.Forms API gaps reported by the Linux .NET 10 build:

- Adds a local WinForms compatibility surface for FlatStyle/FlatAppearance, TextRenderer, ControlPaint, SystemInformation, RichTextBox options, DrawMode/DrawItem, AutoEllipsis, AutoSizeMode, SplitContainer sizing/events, Form Accept/Cancel buttons, SaveFileDialog overwrite behavior, and ContextMenuStrip display.
- Adds System.Windows.Extensions for System.Media.SystemSounds on .NET 10.
- Uses NativeGl.ClearColor/Clear, matching the delegate fields loaded by NativeGl.
- Removes the remaining TextBoxBase dependency from the GTK build path.
- Keeps the OpenGL preview as the only preview renderer; no Avalonia UI or overlay TextBlock is used.
