using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using MColor = System.Windows.Media.Color;
using DColor = System.Drawing.Color;
using Point = System.Drawing.Point;

namespace House_Click
{
	public partial class MainWindow : Window
	{
		[DllImport("user32.dll")]
		private static extern IntPtr GetDC(IntPtr hwnd);

		[DllImport("user32.dll")]
		private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

		[DllImport("gdi32.dll")]
		private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

		[DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
		private static extern IntPtr FindWindowByCaption(IntPtr zeroOnly, string lpWindowName);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool GetCursorPos(ref Win32Point pt);

		[DllImport("user32.dll")]
		private static extern bool SetCursorPos(int x, int y);

		[DllImport("user32.dll")]
		private static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

		[DllImport("user32.dll")]
		internal static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

#pragma warning disable 649
		internal struct INPUT
		{
			public UInt32 Type;
			public MOUSEKEYBDHARDWAREINPUT Data;
		}

		[StructLayout(LayoutKind.Explicit)]
		internal struct MOUSEKEYBDHARDWAREINPUT
		{
			[FieldOffset(0)]
			public MOUSEINPUT Mouse;
		}

		internal struct MOUSEINPUT
		{
			public Int32 X;
			public Int32 Y;
			public UInt32 MouseData;
			public UInt32 Flags;
			public UInt32 Time;
			public IntPtr ExtraInfo;
		}
#pragma warning restore 649

		[StructLayout(LayoutKind.Sequential)]
		internal struct Win32Point
		{
			public Int32 X;
			public Int32 Y;
		};
		public static Point GetMousePosition()
		{
			var w32Mouse = new Win32Point();
			GetCursorPos(ref w32Mouse);
			return new Point(w32Mouse.X, w32Mouse.Y);
		}

		public static void ClickOnPoint(IntPtr wndHandle, Point clientPoint)
		{
			ClientToScreen(wndHandle, ref clientPoint);
			SetCursorPos(clientPoint.X, clientPoint.Y);

			var inputMouseDown = new INPUT { Type = 0 };
			inputMouseDown.Data.Mouse.Flags = 0x0002;

			var inputMouseUp = new INPUT { Type = 0 };
			inputMouseUp.Data.Mouse.Flags = 0x0004;

			var inputs = new[] { inputMouseDown, inputMouseUp };
			SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
		}

		private static IntPtr FindWindowByCaption(string caption)
		{
			return FindWindowByCaption(IntPtr.Zero, caption);
		}

		public static DColor GetPixelColor(IntPtr hwnd, int x, int y)
		{
			var hdc = GetDC(hwnd);
			var pixel = GetPixel(hdc, x, y);
			ReleaseDC(hwnd, hdc);
			var color = DColor.FromArgb((int)(pixel & 0x000000FF),
				(int)(pixel & 0x0000FF00) >> 8,
				(int)(pixel & 0x00FF0000) >> 16);
			return color;
		}

		public static MColor ToMediaColor(DColor color)
		{
			return MColor.FromArgb(color.A, color.R, color.G, color.B);
		}

		private readonly IntPtr _hwnd = FindWindowByCaption("archeage");
		private MColor _actualColor;

		public Xy Coord;

		private int _xF;
		private int _yF;

		private int _xBuild;
		private int _yBuild;

		private int _click;

		private Thread _grabThread;
		private bool _isRunning;

		public MainWindow()
		{
			InitializeComponent();

			Coord = JsonConvert.DeserializeObject<Xy>(File.ReadAllText(@"Save.json"));
			if (Coord.X > 1 && Coord.Y > 1)
			{
				CoordXFinal.Text = Coord.X.ToString();
				CoordYFinal.Text = Coord.Y.ToString();
			}
			if (Coord.Xb > 1 && Coord.Yb > 1)
			{
				CoordBuildX.Text = Coord.Xb.ToString();
				CoordBuildY.Text = Coord.Yb.ToString();
			}
			if (Coord.Click > 1)
			{
				ClickCount.Text = Coord.Click.ToString();
			}

			var mouseP = new Thread(() => {
				while (true)
				{
					var point = GetMousePosition();
					Dispatcher.Invoke(() => {
						CoordX.Text = point.X.ToString();
						CoordY.Text = point.Y.ToString();
						int.TryParse(CoordXFinal.Text, out _xF);
						int.TryParse(CoordYFinal.Text, out _yF);
						int.TryParse(CoordBuildX.Text, out _xBuild);
						int.TryParse(CoordBuildY.Text, out _yBuild);
						int.TryParse(ClickCount.Text, out _click);
					});
					if (_xF > 1 && _yF > 1)
					{
						Coord.X = _xF;
						Coord.Y = _yF;
						_xF = _xF + 63;
						_yF = _yF + 13;
						_actualColor = ToMediaColor(GetPixelColor(_hwnd, _xF, _yF));
						Dispatcher.Invoke(() => {
							Color.Fill = new SolidColorBrush(_actualColor);
							ColorRgb.Text = $"{_actualColor.R} {_actualColor.G} {_actualColor.B}";
						});
					}
					if (_xBuild > 1 && _yBuild > 1)
					{
						Coord.Xb = _xBuild;
						Coord.Yb = _yBuild;
					}
					if (_click > 1)
					{
						Coord.Click = _click;
					}
					Thread.Sleep(5);
				}
			});
			mouseP.Start();
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			var btn = sender as Button;
			if (btn.Content == "STOP")
			{
				_isRunning = false;
				Thread.Sleep(10);
				_grabThread.Abort();
				((Button) sender).Content = "START";
				Green.Text = false.ToString();
				Red.Text = false.ToString();
				Gray.Text = false.ToString();
				Done.Text = false.ToString();
				return;
			}
			if (string.IsNullOrEmpty(CoordXFinal.Text) || string.IsNullOrEmpty(CoordYFinal.Text)) return;

			using (var sw = File.CreateText(@"Save.json"))
			{
				var js = new JsonSerializer();
				js.Serialize(sw, Coord);
			}

			((Button)sender).Content = "STOP";
			_grabThread = new Thread(() => {
				_isRunning = true;
				var gotGreen = false;
				var gotRed = false;
				var gotGray = false;
				var mcGreen = MColor.FromArgb(255, 48, 116, 29);
				var mcRed = MColor.FromArgb(255, 134, 29, 29);
				var mcBroke = MColor.FromArgb(255, 55, 42, 17);
				var allDone = false;

				while (allDone == false && _isRunning)
				{
					if (gotGreen == false && _actualColor == mcGreen)
					{
						if (_actualColor == mcGreen)
						{
							gotGreen = true;
							Dispatcher.Invoke(() => { Green.Text = true.ToString(); });

						}
					}
					if (gotRed == false)
					{
						if (_actualColor == mcRed)
						{
							gotRed = true;
							Dispatcher.Invoke(() => { Red.Text = true.ToString(); });
						}
					}
					if (gotGray == false)
					{
						if (_actualColor == mcBroke)
						{
							gotGray = true;
							Dispatcher.Invoke(() => { Gray.Text = true.ToString(); });
						}
					}

					if (gotGray && (_actualColor != mcGreen && _actualColor != mcRed && _actualColor != mcBroke))
					{
						var point = new Point(Coord.Xb, Coord.Yb);
						Coord.Click = Coord.Click < 30 ? 100 : Coord.Click;

						for (var i = 0; i < Coord.Click; i++)
						{
							ClickOnPoint(_hwnd, point);
							Thread.Sleep(10);
						}

						allDone = true;
						Dispatcher.Invoke(() => {
							Done.Text = true.ToString();
							Thread.Sleep(1000);
							Button_Click(sender, e);
						});
					}
					Thread.Sleep(20);
				}

			});
			_grabThread.Start();
		}

		private void Button_Click_1(object sender, RoutedEventArgs e)
		{
			WindowState = WindowState.Minimized;
		}
	}

	public class Xy
	{
		[JsonProperty("x")]
		public int X { get; set; }

		[JsonProperty("y")]
		public int Y { get; set; }

		[JsonProperty("xb")]
		public int Xb { get; set; }

		[JsonProperty("yb")]
		public int Yb { get; set; }

		[JsonProperty("click")]
		public int Click { get; set; }
	}
}
