using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace BO2InjectorGUI;

/// <summary>
/// Windows 11 acrylic backdrop for a WPF window: the desktop behind the window is blurred and the
/// window's own background is made transparent so the glass panels sit on real frosted glass.
/// On older Windows the DWM call fails and the window keeps its gradient backdrop.
/// </summary>
public static class Glass
{
	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

	private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
	private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
	private const int DWMSBT_TRANSIENTWINDOW = 3; // acrylic

	public static bool TryApply(Window window)
	{
		try
		{
			IntPtr hwnd = new WindowInteropHelper(window).Handle;
			if (hwnd == IntPtr.Zero)
			{
				return false;
			}
			int dark = 1;
			DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
			int backdrop = DWMSBT_TRANSIENTWINDOW;
			if (DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int)) != 0)
			{
				return false;
			}
			HwndSource? source = HwndSource.FromHwnd(hwnd);
			if (source?.CompositionTarget == null)
			{
				return false;
			}
			source.CompositionTarget.BackgroundColor = Colors.Transparent;
			window.Background = Brushes.Transparent;
			return true;
		}
		catch
		{
			return false;
		}
	}
}
