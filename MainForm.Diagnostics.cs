namespace Glyphore;

internal sealed partial class MainForm
{
    private void Benchmark()
    {
        try
        {
            double ms = _preview.BenchmarkGpu();
            string message = Localization.English
                ? $"Cross-platform preview\n\n{_preview.GpuInfo}\n{_settings.Width}×{_settings.Height} ASCII over {_preview.Width}×{_preview.Height} px\n\n{ms:0.000} ms/frame renderer\n~{1000.0 / ms:0} measured FPS (without target limit)"
                : $"Cross-platform preview\n\n{_preview.GpuInfo}\n{_settings.Width}×{_settings.Height} ASCII sobre {_preview.Width}×{_preview.Height} px\n\n{ms:0.000} ms/frame renderer\n~{1000.0 / ms:0} FPS medidos sin límite de target";
            MessageBox.Show(message, "Benchmark", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Benchmark", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
