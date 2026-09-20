global using System;
global using System.Collections.Generic;
global using System.Drawing;
global using System.IO;
global using System.Linq;
global using Majorsilence.Forms;
global using Majorsilence.Forms.Drawing;

// Keep System.Drawing only for cross-platform primitive structs (Color, Point,
// Rectangle, Size, etc.). Force every GDI+/drawing object to the Skia-backed
// Majorsilence implementation so Linux never resolves Graphics/Font/Icon/Image
// to System.Drawing.Common.
global using Graphics = Majorsilence.Forms.Drawing.Graphics;
global using Font = Majorsilence.Forms.Drawing.Font;
global using FontFamily = Majorsilence.Forms.Drawing.FontFamily;
global using FontStyle = Majorsilence.Forms.Drawing.FontStyle;
global using GraphicsUnit = Majorsilence.Forms.Drawing.GraphicsUnit;
global using Icon = Majorsilence.Forms.Drawing.Icon;
global using Image = Majorsilence.Forms.Drawing.Image;
global using Bitmap = Majorsilence.Forms.Drawing.Bitmap;
global using Brush = Majorsilence.Forms.Drawing.Brush;
global using SolidBrush = Majorsilence.Forms.Drawing.SolidBrush;
global using Pen = Majorsilence.Forms.Drawing.Pen;
global using StringFormat = Majorsilence.Forms.Drawing.StringFormat;
global using StringAlignment = Majorsilence.Forms.Drawing.StringAlignment;
global using StringTrimming = Majorsilence.Forms.Drawing.StringTrimming;
global using StringFormatFlags = Majorsilence.Forms.Drawing.StringFormatFlags;
global using SmoothingMode = Majorsilence.Forms.Drawing.Drawing2D.SmoothingMode;
global using DashStyle = Majorsilence.Forms.Drawing.Drawing2D.DashStyle;
global using InterpolationMode = Majorsilence.Forms.Drawing.Drawing2D.InterpolationMode;
global using GraphicsPath = Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath;
global using Timer = Majorsilence.Forms.Timer;
global using Button = Glyphore.CompatButton;
global using CheckBox = Glyphore.CompatCheckBox;
global using ComboBox = Glyphore.CompatComboBox;
global using TextBox = Glyphore.CompatTextBox;
global using RichTextBox = Glyphore.CompatRichTextBox;
global using Label = Glyphore.CompatLabel;
global using FlowLayoutPanel = Glyphore.CompatFlowLayoutPanel;
global using TableLayoutPanel = Glyphore.CompatTableLayoutPanel;
global using SplitContainer = Glyphore.CompatSplitContainer;
global using ListBox = Glyphore.CompatListBox;
global using Form = Glyphore.CompatForm;
global using ContextMenuStrip = Glyphore.CompatContextMenuStrip;
global using ToolTip = Glyphore.CompatToolTip;
