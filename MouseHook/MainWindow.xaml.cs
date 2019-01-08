using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Application = System.Windows.Application;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace MouseHook
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		[DllImport("user32.dll")]
		private static extern IntPtr WindowFromPoint(System.Drawing.Point p);

		[DllImport("user32.dll", CharSet=CharSet.Auto)]
		private static extern int GetWindowTextLength(IntPtr hWnd);
		
		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

		private readonly UserActivityHook _userActivityHook;

		class FrameParameter
		{
			public DispatcherFrame Frame { get; set; }

			public object Args { get; set; }
		}

		public MainWindow()
		{
			InitializeComponent();
			LogText.Focus();

			_userActivityHook = new UserActivityHook(true, false);
			_userActivityHook.OnMouseActivity += UserActivityHookOnOnMouseActivity;
		}

		private void UserActivityHookOnOnMouseActivity(object sender, MouseEventArgs e)
		{
			try
			{
				var text = new StringBuilder();
				if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
				{
					var hWnd = WindowFromPoint(e.Location);
					var lenText = GetWindowTextLength(hWnd);
					var windowText = new StringBuilder(lenText);
					GetWindowText(hWnd, windowText, lenText);

					var hWndSource = HwndSource.FromHwnd(hWnd);
					var position = InputManager.Current.PrimaryMouseDevice.GetPosition(this);
					text.AppendFormat("[{0: hh:mm:ss.fff}] Hook point {1}; Screen: {2}; Control point: {3}; Buttons: {4}; Title: {5}; Find app window: {6}", DateTime.Now, e.Location, PointToScreen(position), position, e.Button, windowText, Application.Current.Windows.OfType<object>().Contains(hWndSource?.RootVisual));
				}
				else
				{
					text.AppendFormat("[{0: hh:mm:ss.fff}] Mouse point {1}; Buttons: {2};", DateTime.Now, e.Location, e.Button);
				}

				text.AppendLine();

				var logText = LogText.Text.Trim();
				var newLine = Environment.NewLine;
				
				foreach (var line in logText.Split(new[]{newLine}, StringSplitOptions.RemoveEmptyEntries)
					.Take(100))
				{
					text.AppendLine(line);
				}
				
				LogText.Text = text.ToString();


			}
			catch (Exception ex)
			{

			}
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
			_userActivityHook.Stop();
		}

		private void DoEvents()
		{
			var frame = new DispatcherFrame();
			Dispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(ExitFrame), frame);

			if (!frame.Dispatcher.HasShutdownFinished)
				Dispatcher.PushFrame(frame);
		}

		private object ExitFrame(object f)
		{
			var parameter = (DispatcherFrame) f;
			
			parameter.Continue = false;
			

			return (object) null;
		}

		private void Open_OnClick(object sender, RoutedEventArgs e)
		{
			var window = new Window();
			window.ShowDialog();
		}

		private void Save_OnClick(object sender, RoutedEventArgs e)
		{
			_userActivityHook.Stop();
			var saveDlg = new SaveFileDialog {Filter = "*.log|*.log"};
			if (saveDlg.ShowDialog(this) == true)
			{
				File.WriteAllText(saveDlg.FileName, LogText.Text);
			}


		}
	}
}
