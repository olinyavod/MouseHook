using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MouseHook
{
	public class UserActivityHook
	{
		#region Private static fields

		private static HookProc KeyboardHookProcedure;

		#endregion

		#region Windows structure

		[StructLayout(LayoutKind.Sequential)]
		struct Point
		{
			public int X;
			public int Y;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct MOUSEHOOKSTRUCT
		{
			public Point pt;
			public IntPtr hwnd;
			public uint wHitTestCode;
			public IntPtr dwExtraInfo;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct MouseHookStructEx
		{
			public MOUSEHOOKSTRUCT mouseHookStruct;
			public int MouseData;
		}


		[StructLayout(LayoutKind.Sequential)]
		private class KeyboardHookStruct
		{
			public int vkCode;

			public int scanCode;

			public int flags;

			public int time;

			public int dwExtraInfo;
		}

		#endregion

		#region Constants

		private const int WH_MOUSE_LL = 14;

		private const int WH_KEYBOARD_LL = 13;

		private const int WH_MOUSE = 7;

		private const int WH_KEYBOARD = 2;

		private const int WM_MOUSEMOVE = 0x200;

		private const int WM_LBUTTONDOWN = 0x201;

		private const int WM_RBUTTONDOWN = 0x204;

		private const int WM_MBUTTONDOWN = 0x207;

		private const int WM_LBUTTONUP = 0x202;

		private const int WM_RBUTTONUP = 0x205;

		private const int WM_MBUTTONUP = 0x208;

		private const int WM_LBUTTONDBLCLK = 0x203;

		private const int WM_RBUTTONDBLCLK = 0x206;

		private const int WM_MBUTTONDBLCLK = 0x209;

		private const int WM_MOUSEWHEEL = 0x020A;

		private const int WM_KEYDOWN = 0x100;

		private const int WM_KEYUP = 0x101;

		private const int WM_SYSKEYDOWN = 0x104;

		private const int WM_SYSKEYUP = 0x105;

		private const byte VK_SHIFT = 0x10;
		private const byte VK_CONTROL = 0x11;
		private const byte VK_MENU = 0x12;
		private const byte VK_CAPITAL = 0x14;
		private const byte VK_NUMLOCK = 0x90;

		#endregion

		#region WinAPI functions

		private delegate int HookProc(int nCode, int wParam, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
		private static extern int SetWindowsHookEx(
			int idHook,
			HookProc lpfn,
			IntPtr hMod,
			int dwThreadId);

		[DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
		private static extern int UnhookWindowsHookEx(int idHook);

		[DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
		private static extern int CallNextHookEx(
			int idHook,
			int nCode,
			int wParam,
			IntPtr lParam);

		[DllImport("user32")]
		private static extern int ToAscii(
			int uVirtKey,
			int uScanCode,
			byte[] lpbKeyState,
			byte[] lpwTransKey,
			int fuState);

		[DllImport("user32")]
		private static extern int GetKeyboardState(byte[] pbKeyState);

		[DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
		private static extern short GetKeyState(int vKey);

		#endregion

		#region Public events

		public event MouseEventHandler OnMouseActivity;

		public event KeyEventHandler KeyDown;

		public event KeyPressEventHandler KeyPress;

		public event KeyEventHandler KeyUp;

		#endregion

		#region Private fields

		private int hMouseHook = 0;

		private int hKeyboardHook = 0;

		private static HookProc MouseHookProcedure;

		#endregion

		#region Public constructors

		public UserActivityHook()
		{
			Start();
		}

		public UserActivityHook(bool InstallMouseHook, bool InstallKeyboardHook)
		{
			Start(InstallMouseHook, InstallKeyboardHook);
		}

		#endregion

		~UserActivityHook()
		{
			Stop(true, true, false);
		}

		#region Public methods

		public void Start(bool installMouseHook = true, bool installKeyboardHook = true)
		{
			var modules = Assembly.GetEntryAssembly().GetModules();
			var hModule = Marshal.GetHINSTANCE(modules.First());

			if (hMouseHook == 0 && installMouseHook)
			{
				MouseHookProcedure = MouseHookProc;
				
				hMouseHook = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProcedure, hModule, 0);
				
				if (hMouseHook == 0)
				{
					var errorCode = Marshal.GetLastWin32Error();
					
					Stop(true, false, false);
					throw new Win32Exception(errorCode);
				}
			}

			if (hKeyboardHook == 0 && installKeyboardHook)
			{
				KeyboardHookProcedure = KeyboardHookProc;
				hKeyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, KeyboardHookProcedure, hModule, 0);
				
				if (hKeyboardHook == 0)
				{
					var errorCode = Marshal.GetLastWin32Error();

					Stop(false, true, false);
					throw new Win32Exception(errorCode);
				}
			}
		}

		public void Stop(bool uninstallMouseHook = true, bool uninstallKeyboardHook = true, bool throwExceptions = true)
		{
			if (hMouseHook != 0 && uninstallMouseHook)
			{
				var retMouse = UnhookWindowsHookEx(hMouseHook);
				hMouseHook = 0;
				
				if (retMouse == 0 && throwExceptions)
				{
					var errorCode = Marshal.GetLastWin32Error();
					
					throw new Win32Exception(errorCode);
				}
			}

			if (hKeyboardHook != 0 && uninstallKeyboardHook)
			{
				var retKeyboard = UnhookWindowsHookEx(hKeyboardHook);
				hKeyboardHook = 0;
				
				if (retKeyboard == 0 && throwExceptions)
				{
					var errorCode = Marshal.GetLastWin32Error();
					
					throw new Win32Exception(errorCode);
				}
			}
		}

		#endregion

		#region Private methods

		private int MouseHookProc(int nCode, int wParam, IntPtr lParam)
		{
			if (nCode >= 0 && OnMouseActivity != null)
			{
				var mouseHookStruct = (MouseHookStructEx)Marshal.PtrToStructure(lParam, typeof(MouseHookStructEx));

				var button = MouseButtons.None;
				short mouseDelta = 0;
				switch (wParam)
				{
					case WM_LBUTTONDOWN:
						button = MouseButtons.Left;
						break;
					case WM_RBUTTONDOWN:
						button = MouseButtons.Right;
						break;
					case WM_MOUSEWHEEL:
						mouseDelta = (short)((mouseHookStruct.MouseData >> 16) & 0xffff);
						break;
				}

				var clickCount = 0;
				if (button != MouseButtons.None)
					if (wParam == WM_LBUTTONDBLCLK || wParam == WM_RBUTTONDBLCLK)
						clickCount = 2;
					else
						clickCount = 1;

				var e = new MouseEventArgs(button, clickCount, mouseHookStruct.mouseHookStruct.pt.X, mouseHookStruct.mouseHookStruct.pt.Y, mouseDelta);
				
				OnMouseActivity(this, e);
			}

			return CallNextHookEx(hMouseHook, nCode, wParam, lParam);
		}

		private int KeyboardHookProc(int nCode, Int32 wParam, IntPtr lParam)
		{
			var handled = false;
			
			if ((nCode >= 0) && (KeyDown != null || KeyUp != null || KeyPress != null))
			{
				var myKeyboardHookStruct = (KeyboardHookStruct)Marshal.PtrToStructure(lParam, typeof(KeyboardHookStruct));
				
				if (KeyDown != null && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
				{
					var keyData = (Keys)myKeyboardHookStruct.vkCode;
					keyData |= ((GetKeyState(VK_SHIFT) & 0x80) == 0x80 ? Keys.Shift : Keys.None);
					keyData |= ((GetKeyState(VK_CONTROL) & 0x80) == 0x80 ? Keys.Control : Keys.None);
					keyData |= ((GetKeyState(VK_MENU) & 0x80) == 0x80 ? Keys.Menu : Keys.None);
					Console.WriteLine(keyData);
					KeyEventArgs e = new KeyEventArgs(keyData);

					KeyDown(this, e);
					
					handled = handled || e.Handled;
				}

				if (KeyPress != null && wParam == WM_KEYDOWN)
				{
					bool isDownShift = ((GetKeyState(VK_SHIFT) & 0x80) == 0x80 ? true : false);
					bool isDownCapslock = (GetKeyState(VK_CAPITAL) != 0 ? true : false);

					var keyState = new byte[256];
					
					GetKeyboardState(keyState);
					
					var inBuffer = new byte[2];
					
					if (ToAscii(myKeyboardHookStruct.vkCode, myKeyboardHookStruct.scanCode, keyState, inBuffer, myKeyboardHookStruct.flags) == 1)
					{
						char key = (char)inBuffer[0];
						if ((isDownCapslock ^ isDownShift) && Char.IsLetter(key))
							key = Char.ToUpper(key);
						KeyPressEventArgs e = new KeyPressEventArgs(key);
						KeyPress(this, e);
						handled = handled || e.Handled;
					}
				}

				if (KeyUp != null && (wParam == WM_KEYUP || wParam == WM_SYSKEYUP))
				{
					Keys keyData = (Keys)myKeyboardHookStruct.vkCode;
					KeyEventArgs e = new KeyEventArgs(keyData);
					KeyUp(this, e);
					handled = handled || e.Handled;
				}

			}

			if (handled)
				return 1;
			else
				return CallNextHookEx(hKeyboardHook, nCode, wParam, lParam);
		}

		#endregion
	}
}
