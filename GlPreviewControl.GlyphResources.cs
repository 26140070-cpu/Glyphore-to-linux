using System.Text;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Glyphore;

internal sealed partial class GlPreviewControl
{
    private unsafe void SyncSceneGlyphResources(GlyphoreScene scene)
    {
        const int maxRunesPerLayer = 1024;
        const int maxUniqueRunes = 4096;

        string signature = string.Join("\u001e", scene.Layers.Select(layer => $"{layer.Id:N}\u001f{layer.Charset}"));
        if (signature == _sceneGlyphSignature && _glyphMapTex != 0) return;

        var atlasRunes = new List<Rune> { new(' ') };
        var atlasIndex = new Dictionary<int, int> { [' '] = 0 };
        var layerMaps = new List<int[]>(scene.Layers.Count);
        _layerGlyphRows.Clear();
        int mapWidth = 1;

        for (int slot = 0; slot < scene.Layers.Count; slot++)
        {
            var layer = scene.Layers[slot];
            var runeList = new List<Rune>();
            var localSeen = new HashSet<int>();

            void AddRunes(string source)
            {
                foreach (var rune in (string.IsNullOrEmpty(source) ? " " : source).EnumerateRunes())
                {
                    if (rune.Value is '\r' or '\n' or '\t') continue;
                    if (runeList.Count >= maxRunesPerLayer) break;
                    if (localSeen.Add(rune.Value)) runeList.Add(rune);
                }
            }

            AddRunes(layer.Charset);
            if (runeList.Count == 0) runeList.Add(new Rune(' '));

            Rune[] runes = runeList.ToArray();
            var map = new int[runes.Length];
            for (int i = 0; i < runes.Length; i++)
            {
                int value = runes[i].Value;
                if (!atlasIndex.TryGetValue(value, out int index))
                {
                    if (atlasRunes.Count < maxUniqueRunes)
                    {
                        index = atlasRunes.Count;
                        atlasRunes.Add(runes[i]);
                        atlasIndex[value] = index;
                    }
                    else index = 0;
                }
                map[i] = index;
            }

            layerMaps.Add(map);
            _layerGlyphRows[layer.Id] = (slot, map.Length);
            mapWidth = Math.Max(mapWidth, map.Length);
        }

        var rampBuilder = new StringBuilder();
        foreach (var rune in atlasRunes) rampBuilder.Append(rune.ToString());
        string atlasRamp = rampBuilder.ToString();
        if (atlasRamp != _atlasRamp) BuildGlyphAtlas(atlasRamp);
        else _atlasGlyphCount = atlasRunes.Count;

        int mapHeight = Math.Max(1, layerMaps.Count);
        var mapPixels = new byte[mapWidth * mapHeight * 4];
        for (int y = 0; y < layerMaps.Count; y++)
        {
            int[] map = layerMaps[y];
            for (int x = 0; x < map.Length; x++)
            {
                int index = map[x];
                int offset = (y * mapWidth + x) * 4;
                mapPixels[offset] = (byte)(index & 0xff);
                mapPixels[offset + 1] = (byte)((index >> 8) & 0xff);
                mapPixels[offset + 2] = (byte)((index >> 16) & 0xff);
                mapPixels[offset + 3] = 255;
            }
        }

        if (_glyphMapTex == 0)
        {
            uint texture;
            NativeGl.GenTextures(1, &texture);
            _glyphMapTex = texture;
        }
        NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, _glyphMapTex);
        NativeGl.TexParameteri(NativeGl.GL_TEXTURE_2D, NativeGl.GL_TEXTURE_MIN_FILTER, (int)NativeGl.GL_NEAREST);
        NativeGl.TexParameteri(NativeGl.GL_TEXTURE_2D, NativeGl.GL_TEXTURE_MAG_FILTER, (int)NativeGl.GL_NEAREST);
        NativeGl.PixelStorei(NativeGl.GL_UNPACK_ALIGNMENT, 1);
        fixed (byte* p = mapPixels)
            NativeGl.TexImage2D(NativeGl.GL_TEXTURE_2D, 0, (int)NativeGl.GL_RGBA8, mapWidth, mapHeight, 0, NativeGl.GL_RGBA, NativeGl.GL_UNSIGNED_BYTE, (IntPtr)p);

        _sceneGlyphSignature = signature;
    }

    private unsafe void BuildGlyphAtlas(string ramp)
    {
        var runes = ramp.EnumerateRunes().ToArray();
        if (runes.Length == 0) runes = " ".EnumerateRunes().ToArray();
        const int cw = 32, ch = 48;
        int cols = Math.Clamp((int)Math.Ceiling(Math.Sqrt(runes.Length * (ch / (double)cw))), 1, 64);
        int rows = (int)Math.Ceiling(runes.Length / (double)cols);

        using var bitmap = new SKBitmap(cols * cw, rows * ch, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Black);
            using var typeface = ResolveMonospaceTypeface();
            using var font = new SKFont(typeface, 26) { Edging = SKFontEdging.SubpixelAntialias };
            using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true, FilterQuality = SKFilterQuality.High };
            for (int i = 0; i < runes.Length; i++)
            {
                string text = runes[i].ToString();
                int x = (i % cols) * cw;
                int y = (i / cols) * ch;
                font.MeasureText(MemoryMarshal.Cast<char, ushort>(text.AsSpan()), out SKRect bounds, paint);
                float tx = x + (cw - bounds.Width) / 2f - bounds.Left;
                float ty = y + (ch - bounds.Height) / 2f - bounds.Top;
                canvas.DrawText(text, tx, ty, font, paint);
            }
        }

        IntPtr pixels = bitmap.GetPixels();
        if (_atlas == 0)
        {
            uint atlas;
            NativeGl.GenTextures(1, &atlas);
            _atlas = atlas;
        }
        NativeGl.BindTexture(NativeGl.GL_TEXTURE_2D, _atlas);
        NativeGl.PixelStorei(NativeGl.GL_UNPACK_ALIGNMENT, 1);
        NativeGl.TexParameteri(NativeGl.GL_TEXTURE_2D, NativeGl.GL_TEXTURE_MIN_FILTER, (int)NativeGl.GL_LINEAR);
        NativeGl.TexParameteri(NativeGl.GL_TEXTURE_2D, NativeGl.GL_TEXTURE_MAG_FILTER, (int)NativeGl.GL_LINEAR);
        NativeGl.TexImage2D(NativeGl.GL_TEXTURE_2D, 0, (int)NativeGl.GL_RGBA8, bitmap.Width, bitmap.Height, 0,
            NativeGl.GL_BGRA, NativeGl.GL_UNSIGNED_BYTE, pixels);

        _atlasCols = cols;
        _atlasRows = rows;
        _atlasGlyphCount = runes.Length;
        _atlasRamp = ramp;
    }

    private static SKTypeface ResolveMonospaceTypeface()
    {
        string[] candidates = ["Cascadia Mono", "Cascadia Code", "JetBrains Mono", "Fira Code", "DejaVu Sans Mono", "Noto Sans Mono", "monospace"];
        foreach (string name in candidates)
        {
            var tf = SKTypeface.FromFamilyName(name, SKFontStyle.Normal);
            if (tf is not null && !string.IsNullOrWhiteSpace(tf.FamilyName)) return tf;
        }
        return SKTypeface.Default;
    }


}
