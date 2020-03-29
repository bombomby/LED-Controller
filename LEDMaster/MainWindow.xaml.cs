using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LEDMaster
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public class ArduinoController : INotifyPropertyChanged, IDisposable
		{
			public SerialPort Port { get; set; }

			const int BaudRage = 9600;

			public ArduinoController()
			{
				Init();
				Reset();
			}

			public void ClosePort()
			{
				if (Port != null)
				{
					Port.Close();
					Port.DataReceived -= Port_DataReceived;
					Port = null;
				}
			}

			public void Init()
			{
				string[] ports = SerialPort.GetPortNames();

				foreach (String p in ports)
				{
					Debug.WriteLine("Available COM Port: {0}", p);
				}

				if (Port != null)
				{
					Port.Close();
					Port = null;
				}

				if (ports.Length > 0)
				{
					String portName = ports[0];
					try
					{
						Port = new SerialPort(portName, BaudRage);
						Port.Open();
						Port.DataReceived += Port_DataReceived;
						MessageReceived?.Invoke(String.Format("Connected COM Port {0}!\n", portName));
					}
					catch (IOException ex)
					{
						MessageReceived?.Invoke(String.Format("Can't connect {0}: {1}\n", portName, ex.Message));
						ClosePort();
					}
				}
			}

			private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
			{
				SerialPort port = (SerialPort)sender;
				string data = port.ReadExisting();
				MessageReceived?.Invoke(data);
			}

			public bool CheckPort()
			{
				if (Port == null || !Port.IsOpen)
				{
					MessageReceived?.Invoke("Reconnecting COM Port...\n");
					Init();
				}
				return Port != null && Port.IsOpen;
			}

			public void Reset()
			{
				Brightness = 170;
				Frequency = 15;
			}

			private void UpdateBrightness(int value)
			{
				if (CheckPort())
				{
					Port.WriteLine(String.Format("b {0}", value));
				}
			}

			public void UpdateFrequency(int value)
			{
				if (CheckPort())
				{
					Port.WriteLine(String.Format("f {0}", value));
				}
			}

			public event PropertyChangedEventHandler PropertyChanged;

			private bool isEnabled = true;
			public bool IsEnabled
			{
				get { return isEnabled; }
				set
				{
					isEnabled = value;
					UpdateBrightness(isEnabled ? brightness : 0);
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsEnabled"));
				}
			}


			private int brightness;
			public int Brightness
			{
				get { return brightness; }
				set
				{
					brightness = value;
					UpdateBrightness(isEnabled ? brightness : 0);
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Brightness"));
				}
			}

			private int frequency;
			public int Frequency
			{
				get { return (int)frequency; }
				set
				{
					frequency = value;
					UpdateFrequency(value);
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Frequency"));
				}
			}

			public void Dispose()
			{
				if (Port != null)
				{
					Port.Close();
				}
			}

			public delegate void MessageReceivedDelegate(String messate);
			public event MessageReceivedDelegate MessageReceived;
		}

		public ArduinoController Controller { get; set; }

		private System.Windows.Forms.NotifyIcon notifyIcon = null;

		public MainWindow()
		{
			InitializeComponent();

			Closed += MainWindow_Closed;
			Loaded += MainWindow_Loaded;
			StateChanged += MainWindow_StateChanged;

			Controller = new ArduinoController();
			Controller.MessageReceived += OnMessageReceived;

			DataContext = Controller;

			PreviewKeyDown += MainWindow_PreviewKeyDown;

			SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged; ;
		}

		private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
		{
			switch (e.Mode)
			{
				case PowerModes.Resume:
					Controller.IsEnabled = true;
					break;
				case PowerModes.Suspend:
					Controller.IsEnabled = false;
					break;
			}
		}

		private void OnMessageReceived(String message)
		{
			Application.Current.Dispatcher.Invoke(new Action(() => { LogArea.Text = LogArea.Text + message; LogArea.ScrollToEnd(); }));
		}

		private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				this.WindowState = WindowState.Minimized;
			}
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			e.Cancel = true;
			this.WindowState = WindowState.Minimized;
			base.OnClosing(e);
		}

		private void MainWindow_StateChanged(object sender, EventArgs e)
		{
			if (this.WindowState == WindowState.Minimized)
			{
				this.Topmost = false;
				this.ShowInTaskbar = false;
				this.Visibility = Visibility.Hidden;
				notifyIcon.Visible = true;
			}
			else
			{
				notifyIcon.Visible = true;
				this.ShowInTaskbar = true;
				this.Visibility = Visibility.Visible;
				this.Topmost = true;
			}
		}

		private void MainWindow_Loaded(object sender, RoutedEventArgs e)
		{
			notifyIcon.Visible = true;
		}

		Icon LED_Green;
		Icon LED_Red;

		protected override void OnInitialized(EventArgs e)
		{
			notifyIcon = new System.Windows.Forms.NotifyIcon();
			notifyIcon.MouseClick += NotifyIcon_MouseClick;

			Stream greenStream = Application.GetResourceStream(new Uri("pack://application:,,,/LEDMaster;component/Icons/LED_Green.ico")).Stream;
			Stream redStream = Application.GetResourceStream(new Uri("pack://application:,,,/LEDMaster;component/Icons/LED_Red.ico")).Stream;
			LED_Green = new Icon(greenStream);
			LED_Red = new Icon(redStream);

			notifyIcon.Icon = LED_Green;

			base.OnInitialized(e);
		}

		private void NotifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			if (e.Button == System.Windows.Forms.MouseButtons.Right)
			{
				if (this.WindowState == WindowState.Minimized)
				{
					this.WindowState = WindowState.Normal;
				}
				this.Activate();
				this.Show();
			}
			else
			{
				Controller.IsEnabled = !Controller.IsEnabled;
				notifyIcon.Icon = Controller.IsEnabled ? LED_Green : LED_Red;
			}
		}


		private void MainWindow_Closed(object sender, EventArgs e)
		{
			Controller.Dispose();
		}

		private void Reset_Click(object sender, RoutedEventArgs e)
		{
			Controller.Reset();
		}
	}
}
