using System.Runtime.InteropServices;

namespace JarvisCore.Native;

/// <summary>
/// Win32 User32.dll P/Invoke declarations for window manipulation and capture
/// </summary>
public static class User32
{
    #region Constants

    // Window Styles
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x80000;
    public const int LWA_ALPHA = 0x2;

    // ShowWindow commands
    public const int SW_HIDE = 0;
    public const int SW_SHOWNORMAL = 1;
    public const int SW_SHOWMINIMIZED = 2;
    public const int SW_SHOWMAXIMIZED = 3;
    public const int SW_RESTORE = 9;
    public const int SW_MINIMIZE = 6;

    // PrintWindow flags
    public const uint PW_CLIENTONLY = 1;
    public const uint PW_RENDERFULLCONTENT = 2;

    #endregion

    #region Structures

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    #endregion

    #region Delegates

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    #endregion

    #region Window Finding

    /// <summary>
    /// Finds a window by class name and/or window name
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    /// <summary>
    /// Enumerates all top-level windows
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    /// <summary>
    /// Gets the window text (title)
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    /// <summary>
    /// Gets the length of the window text
    /// </summary>
    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    /// <summary>
    /// Gets the process ID that created the window
    /// </summary>
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// Checks if window is visible
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    #endregion

    #region Window State

    /// <summary>
    /// Checks if window is minimized (iconic)
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    /// <summary>
    /// Checks if window is maximized (zoomed)
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool IsZoomed(IntPtr hWnd);

    /// <summary>
    /// Shows or hides a window
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    /// <summary>
    /// Shows window without activating it
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    #endregion

    #region Window Dimensions

    /// <summary>
    /// Gets the window rectangle (position and size)
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>
    /// Gets the client area rectangle
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    #endregion

    #region Window Styling (for transparency trick)

    /// <summary>
    /// Gets window style attributes
    /// </summary>
    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    /// <summary>
    /// Sets window style attributes
    /// </summary>
    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>
    /// Sets layered window attributes (transparency)
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    #endregion

    #region Device Context

    /// <summary>
    /// Gets the device context for the entire window
    /// </summary>
    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowDC(IntPtr hWnd);

    /// <summary>
    /// Gets the device context for the client area
    /// </summary>
    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    /// <summary>
    /// Releases a device context
    /// </summary>
    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    #endregion

    #region Window Capture

    /// <summary>
    /// Captures a window to a device context (works even if window is behind others)
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    #endregion

    #region Animation Control

    [StructLayout(LayoutKind.Sequential)]
    public struct ANIMATIONINFO
    {
        public uint cbSize;
        public int iMinAnimate;
    }

    public const uint SPI_GETANIMATION = 0x0048;
    public const uint SPI_SETANIMATION = 0x0049;
    public const uint SPIF_SENDCHANGE = 0x0002;

    /// <summary>
    /// Gets or sets system parameters (used to disable window animation)
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref ANIMATIONINFO pvParam, uint fWinIni);

    #endregion

    #region System Metrics

    /// <summary>
    /// Gets system metrics (screen size, etc.)
    /// </summary>
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    // System metric constants
    public const int SM_CXSCREEN = 0;  // Primary screen width
    public const int SM_CYSCREEN = 1;  // Primary screen height

    #endregion
}
