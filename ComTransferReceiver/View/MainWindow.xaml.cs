using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace ComTransfer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml (receiver edition)
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ComPort port;

        public MainWindow()
        {
            PCOMM.InitializeLibrary();

            port = new ComPort();

            DataContext = port;

            InitializeComponent();

            Title = "SeriCom - Receiver Only - " + TransferSettings.Engine;
            port.AddLog("Startup", "Role: Receiver; Engine: " + TransferSettings.Engine);
            if (TransferSettings.Engine == TransferEngineKind.OneWay)
            {
                port.AddLog("Startup", "Open the receiver COM port before sending. Files are saved only after complete validation.");
            }

            CheckPipeline();

            CheckAutoRun();
        }

        private void CheckAutoRun()
        {
            if (ConfigurationManager.AppSettings["autorun"]?.ToUpper() == "TRUE")
            {
                if (!int.TryParse(ConfigurationManager.AppSettings["waittime"], out int wait))
                {
                    wait = 10000;
                }
                if (!int.TryParse(ConfigurationManager.AppSettings["retrycount"], out int retry))
                {
                    retry = 5;
                }
                port.DelayOpen(1000, wait, retry);
            }
        }

        private void CheckPipeline()
        {
            if (ConfigurationManager.AppSettings["usepipe"]?.ToUpper() == "TRUE")
            {
                PipelineManager.Initialize(this, port);
            }
        }

        #region ShowLog
        private void ShowSpecialLog()
        {
            string data = "nZW7btswFIb3PgU1aaGpZqXsPEHmLh20F548dJELMFURJE6MBIWDBr0EBWqgQ5aiCNAOlgLkUUQEHfkK/UmKust2S0gWRR6e75yfh7IUQv7jRSJKGRvl4up1Lq7xGI1UuiaUqPS3ypbP9vKhb0omWKg2l9y1kPvmGZUj4zGX4kKlQiapFMc7nBN3T6VYPZ3ccZlkCJBXTSY35qlDNq8ZHBMzpDabvWLHpV23G1x1xl6o7CzA02vYpftJhIVBsUZl77TE9mWOQKMmJV1HkedBn2EM6bs1wsUWHkVwrHtT8nh/OLECmXeVvvdnKjs5qOexbyokNnnM3SKVfaQThAoYIhYqO/cjmx8mn359eaUNVLboEXgPWn763eP4cawFXJ1Lcd0OaoqtWarsVmUfHu8x4If/wdP6cVMLIbyhT0DW4z6JcEqc8MmdPL6sFnkd0laU26ii6W7oYUEcEbW5JXrvC9u3a5TbkTsrxfIe2gCurArqk8rW6uI1yobO8tUNzhUu4mPg0BzjBydtD3F7OQYdHvcMx3xlyJjK5CsqRg/ha2H2ywqbDvJqwE7V01z8bJtrnh6nAASacIFBSBmbQJBfFfkbsg+1A+3bCM5jJA8k4wGMsLVM2zL+0h6NBXl+gM50uHJq0G6elX2zuqdBAAY3TFjNZLLJV1eAItHPjrWdOSjwQJ585kPE2HSDoqIZUQ/f0Gf6oC6Nxktd1CikZal4v85dsSvVhyIIsJ0M+p6VfH0xEzVjhcUnhpjIn9WP+a78h/h11f0anVhEccJ0n8EB9r6YMLky5nSMt50m2xhr4+u50zY7cDYoc2LXVxZ6Ym6DslbzHfw6vPmJqjrASpEUNgEJbW3X4mptsc1+F5mzRrWXuErtQB/i+jxjrpxD/V8mxSkpyzxJnVu6C83+Ag==";
            byte[] buffer = Convert.FromBase64String(data);
            string text = "";

            using (MemoryStream outputStream = new MemoryStream())
            {
                using (MemoryStream inputStream = new MemoryStream(buffer))
                {
                    using (DeflateStream deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress))
                    {
                        try
                        {
                            deflateStream.CopyTo(outputStream);
                            text = Encoding.UTF8.GetString(outputStream.ToArray());
                        }
                        catch
                        {
                            return;
                        }
                    }
                }
            }

            foreach (string log in text.Split('\n'))
            {
                port.AddSimpleLog(log);
            }
        }
        #endregion

        private void Button_Start_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            button.IsEnabled = true;

            if (port.IsOpen)
            {
                port.ClosePort();
            }
            else
            {
                port.OpenPort();
            }

            Task.Factory.StartNew(() => {
                Thread.Sleep(3000);
                Application.Current.Dispatcher?.Invoke((Action)(() =>
                {
                    button.IsEnabled = true;
                }));
            });
        }

        private void Button_Option_Click(object sender, RoutedEventArgs e)
        {
            new ConfigWindow(this).ShowDialog();

            try
            {
                port.InitialPort();
            }
            catch
            {

            }
        }

        private void Button_ClearRecord_Click(object sender, RoutedEventArgs e)
        {
            port.ClearRecord();
        }
        private void Button_ClearLog_Click(object sender, RoutedEventArgs e)
        {
            port.ClearLog();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!PipelineManager.IsConnected)
            {
                string password = ConfigurationManager.AppSettings["exitpassword"] ?? "exit";
                bool? result = PasswordWindow.Show(this, "Enter the exit password", password, "Password Required", false);
                if (result != true)
                {
                    e.Cancel = true;
                    return;
                }
            }
            port.Dispose();
        }

        public void SubmitCommand(string command, string param)
        {
            port.SubmitCommand(command, param);
        }

        public string LastCommand
        {
            get
            {
                return port.LastCommand;
            }
            set
            {
                port.LastCommand = value;
            }
        }
        private void OpenHelpFile()
        {
            string helpFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "README.html");

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo(helpFile);
                    process.Start();
                }
            }
            catch
            {

            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsRepeat)
            {
                return;
            }
            if (e.Key == Key.F1)
            {
                OpenHelpFile();
            }
            else if (Keyboard.IsKeyDown(Key.M) && Keyboard.IsKeyDown(Key.I) && Keyboard.IsKeyDown(Key.K) && Keyboard.IsKeyDown(Key.U))
            {
                ShowSpecialLog();
            }
        }

        private void Label_Folder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Label label = sender as Label;

            try
            {
                string folder = label.Content.ToString().Split('>')[1].TrimStart('[').TrimEnd(']');
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo(folder);
                    process.Start();
                }
            }
            catch
            {

            }
        }
        private void Label_Com_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                string command = "/K mode COM" + port.PortID;
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo("cmd.exe", command) { WorkingDirectory = "/" };
                    process.Start();
                }
            }
            catch
            {

            }
        }

        private void Border_StopSending_MouseUp(object sender, MouseButtonEventArgs e)
        {
            port.ForceStopSending();
        }

        private void Border_StopReceiving_MouseUp(object sender, MouseButtonEventArgs e)
        {
            port.ForceStopReceiving();
        }
        #region Auto-detect COM port
        private volatile bool detecting;

        /// <summary>
        /// List the available COM port numbers
        /// </summary>
        private static List<int> GetComPorts()
        {
            List<int> ports = new List<int>();
            try
            {
                foreach (string name in SerialPort.GetPortNames())
                {
                    string upper = name.ToUpper().Trim();
                    int id;
                    if (upper.StartsWith("COM") && int.TryParse(upper.Substring(3), out id) && id > 0 && !ports.Contains(id))
                    {
                        ports.Add(id);
                    }
                }
            }
            catch
            {

            }
            ports.Sort();
            return ports;
        }

        /// <summary>
        /// Detect the serial port of the device by unplugging and replugging it
        /// </summary>
        private void Button_Detect_Click(object sender, RoutedEventArgs e)
        {
            if (port.IsOpen)
            {
                MessageBox.Show("Close the serial port before detecting.", "Auto-detect COM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (detecting)
            {
                detecting = false;
                port.AddSimpleLog("COM port auto-detection canceled.");
                return;
            }

            List<int> snapshot = GetComPorts();

            MessageBoxResult result = MessageBox.Show(
                "1) Unplug the device cable (or USB adapter) now.\n2) Click OK.\n3) Plug it back in when the log asks.\n\nIf the device is not connected yet, just plug it in after clicking OK.",
                "Auto-detect COM port",
                MessageBoxButton.OKCancel, MessageBoxImage.Information);
            if (result != MessageBoxResult.OK)
            {
                return;
            }

            detecting = true;
            port.AddSimpleLog("COM auto-detection started, waiting for a device change...");

            Task.Factory.StartNew(() => DetectComWorker(snapshot));
        }

        /// <summary>
        /// Background worker that compares the port list before and after replug
        /// </summary>
        private void DetectComWorker(List<int> baseline)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(30);
            List<int> missing = null;

            while (detecting && DateTime.UtcNow < deadline)
            {
                List<int> now = GetComPorts();
                if (!now.SequenceEqual(baseline))
                {
                    List<int> appeared = now.Except(baseline).ToList();
                    missing = baseline.Except(now).ToList();
                    if (appeared.Count > 0)
                    {
                        DetectFinished(appeared);
                        return;
                    }
                    baseline = now;
                    break;
                }
                Thread.Sleep(300);
            }

            if (!detecting)
            {
                return;
            }

            if (missing == null)
            {
                DetectFailed("No change was detected. Unplug the device cable after clicking OK, then try again.");
                return;
            }

            Application.Current.Dispatcher?.Invoke((Action)(() =>
            {
                port.AddSimpleLog("Device unplugged: " + string.Join(", ", missing.Select(item => "COM" + item)) + ". Please plug it back in now...");
            }));

            deadline = DateTime.UtcNow.AddSeconds(45);
            while (detecting && DateTime.UtcNow < deadline)
            {
                List<int> appeared = GetComPorts().Except(baseline).ToList();
                if (appeared.Count > 0)
                {
                    DetectFinished(appeared);
                    return;
                }
                Thread.Sleep(300);
            }

            if (detecting)
            {
                DetectFailed("No serial device was plugged back in. Please try again.");
            }
        }

        /// <summary>
        /// Apply the detected port and ask to open it
        /// </summary>
        private void DetectFinished(List<int> appeared)
        {
            Application.Current.Dispatcher?.Invoke((Action)(() =>
            {
                detecting = false;

                if (appeared.Count != 1)
                {
                    string list = string.Join(", ", appeared.Select(item => "COM" + item));
                    port.AddLog("Fault", "Cannot auto-detect the serial port, multiple candidates: " + list);
                    MessageBox.Show("Multiple serial ports appeared: " + list + "\nConnect only the target device and try again.", "Auto-detect COM", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int id = appeared[0];
                SaveComPortID(id);

                try
                {
                    port.InitialPort();
                }
                catch
                {

                }

                port.AddLog("Operation", "Serial port auto-detected: COM" + id);

                if (MessageBox.Show("Serial port detected: COM" + id + "\nSaved to configuration. Open the port now?", "Auto-detect COM", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    port.OpenPort();
                }
            }));
        }

        /// <summary>
        /// Report a failed detection
        /// </summary>
        private void DetectFailed(string message)
        {
            Application.Current.Dispatcher?.Invoke((Action)(() =>
            {
                detecting = false;
                port.AddLog("Fault", "COM port auto-detection failed: " + message);
                MessageBox.Show(message, "Auto-detect COM", MessageBoxButton.OK, MessageBoxImage.Warning);
            }));
        }

        /// <summary>
        /// Save the detected COM number into the program configuration
        /// </summary>
        private void SaveComPortID(int id)
        {
            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (config.AppSettings.Settings["com"] == null)
                {
                    config.AppSettings.Settings.Add("com", id.ToString());
                }
                else
                {
                    config.AppSettings.Settings["com"].Value = id.ToString();
                }
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch
            {

            }
        }
        #endregion
    }
}
