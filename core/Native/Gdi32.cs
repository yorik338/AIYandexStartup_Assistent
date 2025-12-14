using System.Runtime.InteropServices;

namespace JarvisCore.Native;

/// <summary>
/// Win32 Gdi32.dll P/Invoke declarations for graphics operations
/// </summary>
public static class Gdi32
{
    public const int SRCCOPY = 0x00CC0020;

    /// <summary>
    /// Creates a memory device context compatible with the specified device
    /// </summary>
    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    /// <summary>
    /// Creates a bitmap compatible with the specified device context
    /// </summary>
    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    /// <summary>
    /// Selects an object into the specified device context
    /// </summary>
    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    /// <summary>
    /// Deletes a device context
    /// </summary>
    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hdc);

    /// <summary>
    /// Deletes a GDI object
    /// </summary>
    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    /// <summary>
    /// Performs a bit-block transfer
    /// </summary>
    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);
}
