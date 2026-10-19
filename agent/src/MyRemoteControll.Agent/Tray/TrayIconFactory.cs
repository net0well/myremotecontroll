using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using MyRemoteControll.Agent.Hosting;

namespace MyRemoteControll.Agent.Tray;

/// <summary>Ícones da bandeja desenhados em tempo de execução, um por estado (cores do design system).</summary>
internal static class TrayIconFactory
{
    private const int Size = 32;

    public static Icon Create(AgentStatus status) => Create(ColorFor(status));

    private static Color ColorFor(AgentStatus status) =>
        status switch
        {
            AgentStatus.Running => ColorTranslator.FromHtml("#3D7BFF"),
            AgentStatus.Paused => ColorTranslator.FromHtml("#F59E0B"),
            AgentStatus.Faulted => ColorTranslator.FromHtml("#EF4444"),
            _ => ColorTranslator.FromHtml("#6B7280"),
        };

    private static Icon Create(Color color)
    {
        using var bitmap = new Bitmap(Size, Size);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var background = new SolidBrush(color))
        using (var font = new Font("Segoe UI", 17, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);
            graphics.FillEllipse(background, 1, 1, Size - 2, Size - 2);
            graphics.DrawString("M", font, Brushes.White, new RectangleF(0, 1, Size, Size), format);
        }

        return FromPng(bitmap);
    }

    // Um .ico com uma imagem PNG embutida: evita GetHicon(), que exige DestroyIcon via P/Invoke para não vazar handles.
    private static Icon FromPng(Bitmap bitmap)
    {
        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);

        using var ico = new MemoryStream();
        using (var writer = new BinaryWriter(ico, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((short)0);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write((byte)Size);
            writer.Write((byte)Size);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write((int)png.Length);
            writer.Write(6 + 16);
            writer.Write(png.ToArray());
        }

        ico.Position = 0;
        return new Icon(ico);
    }
}
