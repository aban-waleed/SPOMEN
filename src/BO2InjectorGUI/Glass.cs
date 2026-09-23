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

	[StructLayout(LayoutKind.Sequential)]
	private struct Margins
	{
		public int Left, Right, Top, Bottom;
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
	private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
	private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

	private const int GWL_STYLE = -16;
	private const long WS_SYSMENU = 0x80000;
	private const uint SWP_FRAMECHANGED = 0x0020, SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
	private const int WM_SYSCOMMAND = 0x0112;
	private const int SC_CLOSE = 0xF060;

	private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
	private const int DWMWA_CAPTION_COLOR = 35;
	private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
	private const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);
	private const int DWMSBT_TRANSIENTWINDOW = 3; // acrylic

	/// <summary>
	/// Removes the DWM caption buttons. The windows use WindowChrome with the frame extended over the whole
	/// client area (needed for the acrylic backdrop), and on that "sheet of glass" DWM keeps painting
	/// minimize/maximize/close unless the window has no system menu. Without WS_SYSMENU DefWindowProc also
	/// stops turning Alt+F4 and the taskbar's close command into WM_CLOSE, so both are handled here.
	/// The app draws its own mac-style buttons instead.
	/// </summary>
	public static void HideCaptionButtons(Window window)
	{
		IntPtr hwnd = new WindowInteropHelper(window).Handle;
		if (hwnd == IntPtr.Zero)
		{
			return;
		}
		// Otherwise DWM paints the title-bar colour across the top strip of the glass, behind the translucent surface.
		int none = DWMWA_COLOR_NONE;
		DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref none, sizeof(int));
		long style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
		SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(style & ~WS_SYSMENU));
		SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
		HwndSource.FromHwnd(hwnd)?.AddHook((IntPtr _, int msg, IntPtr wParam, IntPtr _, ref bool handled) =>
		{
			if (msg == WM_SYSCOMMAND && (wParam.ToInt64() & 0xFFF0) == SC_CLOSE)
			{
				handled = true;
				window.Close();
			}
			return IntPtr.Zero;
		});
		window.PreviewKeyDown += (_, e) =>
		{
			if (e.SystemKey == System.Windows.Input.Key.F4 && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0)
			{
				e.Handled = true;
				window.Close();
			}
		};
	}

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
			// The backdrop is only composed behind the DWM frame; extend the frame over the whole
			// client area so the blurred desktop shows through every transparent pixel we draw.
			Margins all = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
			if (DwmExtendFrameIntoClientArea(hwnd, ref all) != 0)
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
