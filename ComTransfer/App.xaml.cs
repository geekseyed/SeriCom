using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace ComTransfer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Set the window show state (normal, maximized, minimized, etc.)
        /// </summary>
        /// <param name="hWnd">window handle</param>
        /// <param name="nCmdShow">window show state</param>
        /// <returns>result</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// Bring the window to the foreground
        /// </summary>
        /// <param name="hWnd">window handle</param>
        /// <returns>result</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        /// <summary>
        /// Application startup event handler
        /// </summary>
        /// <param name="e">event arguments</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            // Check for an already running instance of this process; if found, close this new instance and activate the original main window
            var processCurrent = Process.GetCurrentProcess();
            Process process = Process.GetProcessesByName(processCurrent.ProcessName).FirstOrDefault(item => item.Id != processCurrent.Id && item.MainWindowHandle != IntPtr.Zero);
            if (process != null)
            {
                ShowWindow(process.MainWindowHandle, 9);
                SetForegroundWindow(process.MainWindowHandle);
                Environment.Exit(0);
                return;
            }
        }
    }
}
