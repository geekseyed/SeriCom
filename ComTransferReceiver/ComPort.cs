// Receiver project only: ComTransferReceiver\ComPort.cs
// Remove the linked ..\ComTransfer\ComPort.cs item from the receiver
// project before adding this independent file. Do not replace the sender file.
// Requires the previously supplied ComPort.OneWay.cs and OneWay receiver files.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ComTransfer
{
    /// <summary>
    /// ViewModel that manages the serial communication process
    /// </summary>
    public partial class ComPort : CustomINotifyPropertyChanged
    {
        /// <summary>
        /// Serial port parameters
        /// </summary>
        public string PortInfo => $"COM{PortID} {BaudRate}";
        /// <summary>
        /// Serial port status
        /// </summary>
        public int PortStatus { get; set; } = -1;
        /// <summary>
        /// Serial port open status
        /// </summary>
        public bool Status_Open => PortStatus >= 0;
        /// <summary>
        /// Serial port CTS status
        /// </summary>
        public bool Status_CTS => PortStatus >= 0 && (PortStatus & PCOMM.S_CTS) > 0;
        /// <summary>
        /// Serial port DSR status
        /// </summary>
        public bool Status_DSR => PortStatus >= 0 && (PortStatus & PCOMM.S_DSR) > 0;
        /// <summary>
        /// Serial port RI status
        /// </summary>
        public bool Status_RI => PortStatus >= 0 && (PortStatus & PCOMM.S_RI) > 0;
        /// <summary>
        /// Serial port CD status
        /// </summary>
        public bool Status_CD => PortStatus >= 0 && (PortStatus & PCOMM.S_CD) > 0;

        /// <summary>
        /// COM port
        /// </summary>
        public int PortID = 1;
        /// <summary>
        /// Baud rate
        /// </summary>
        public int BaudRate = 9600;
        /// <summary>
        /// Number of data bits
        /// </summary>
        public int DataBits = 8;
        /// <summary>
        /// Number of stop bits
        /// </summary>
        public int StopBits = 1;
        /// <summary>
        /// Parity
        /// </summary>
        public string Parity = "NONE";
        /// <summary>
        /// HW flag
        /// </summary>
        public bool IsHW = false;
        /// <summary>
        /// DW flag
        /// </summary>
        public bool IsSW = false;
        /// <summary>
        /// DTR flag
        /// </summary>
        public bool IsDTR = true;
        /// <summary>
        /// RTS flag
        /// </summary>
        public bool IsRTS = true;
        /// <summary>
        /// File tag
        /// </summary>
        public const int FileKey = 27;
        /// <summary>
        /// Serial file save location settings
        /// </summary>
        public List<KeyValuePair<string, string>> PortOption => directoryDict.Count == 0 ? new List<KeyValuePair<string, string>>() : directoryDict.Keys.Select(item => new KeyValuePair<string, string>(item, string.Format("{0}>[{1}]", item, directoryDict[item]))).ToList();
        /// <summary>
        /// File save location dictionary
        /// </summary>
        private readonly Dictionary<string, string> directoryDict = new Dictionary<string, string>();
        /// <summary>
        /// Default file storage folder
        /// </summary>
        private const string DefaultDirectory = @"C:\";
        /// <summary>
        /// File save location
        /// </summary>
        public string SaveDirectory = DefaultDirectory;
        /// <summary>
        /// Path of the file to send
        /// </summary>
        public string SelectedFilePath { get; set; }
        /// <summary>
        /// Path of the file to pull
        /// </summary>
        public string PullFilePath { get; set; }
        /// <summary>
        /// Working folder
        /// </summary>
        public const string WorkingDirectory = "files";

        /// <summary>
        /// Whether the serial port is open
        /// </summary>
        public bool IsOpen { get; set; }
        /// <summary>
        /// Whether the serial port is being opened
        /// </summary>
        public bool IsOpening { get; set; }

        /// <summary>
        /// Receive counter
        /// </summary>
        public long ReceiveCount { get; set; } = 0;
        /// <summary>
        /// Total received bytes
        /// </summary>
        public long ReceiveMax { get; set; } = 0;
        /// <summary>
        /// Receive progress text
        /// </summary>
        public string ReceiveProgress => string.Format("{0}/{1}", ReceiveCount, ReceiveMax);
        /// <summary>
        /// Receive progress percentage
        /// </summary>
        public double ReceivePercent => ReceiveMax == 0 ? 0 : (double)ReceiveCount / ReceiveMax;
        /// <summary>
        /// Receive time remaining
        /// </summary>
        public double ReceiveTime => ReceiveMax == 0 || ReceiveMax <= ReceiveCount ? 0 : (double)(ReceiveMax - ReceiveCount) / BaudRate * 8 * 1.7;
        /// <summary>
        /// Receive time-remaining text
        /// </summary>
        public string ReceiveTimeText => ReceiveTime > 60 ? string.Format("{0:0}m {1:0}s", ReceiveTime / 60, (int)ReceiveTime % 60) : string.Format("{0:0}s", ReceiveTime);
        public long SendCount { get; set; } = 0;
        public long SendMax { get; set; } = 0;
        public string SendProgress => string.Format("{0}/{1}", SendCount, SendMax);
        public double SendPercent => SendMax == 0 ? 0 : (double)SendCount / SendMax;
        public double SendTime => SendMax == 0 || SendMax <= SendCount ? 0 : (double)(SendMax - SendCount) / BaudRate * 8 * 1.7;
        public string SendTimeText => SendTime > 60 ? string.Format("{0:0}m {1:0}s", SendTime / 60, (int)SendTime % 60) : string.Format("{0:0}s", SendTime);
        public int SizeLimit = 50;

        /// <summary>
        /// Set basic serial port parameters
        /// </summary>
        /// <exception cref="Exception">Serial port parameter exception</exception>
        public void SetPort()
        {
            if (TransferSettings.Engine == TransferEngineKind.OneWay)
            {
                if (DataBits != 8)
                    throw new InvalidOperationException("OneWay requires 8 data bits.");
                if (IsHW || IsSW)
                    throw new InvalidOperationException("Disable hardware and software flow control before using OneWay.");
            }
            int result;
            int baudRate, dataBits, stopBits, parity;
            bool customBaudRate = false;

            switch (BaudRate)
            {
                case 50: baudRate = PCOMM.B50; break;
                case 75: baudRate = PCOMM.B75; break;
                case 110: baudRate = PCOMM.B110; break;
                case 134: baudRate = PCOMM.B134; break;
                case 150: baudRate = PCOMM.B150; break;
                case 300: baudRate = PCOMM.B300; break;
                case 600: baudRate = PCOMM.B600; break;
                case 1200: baudRate = PCOMM.B1200; break;
                case 1800: baudRate = PCOMM.B1800; break;
                case 2400: baudRate = PCOMM.B2400; break;
                case 4800: baudRate = PCOMM.B4800; break;
                case 7200: baudRate = PCOMM.B7200; break;
                case 9600: baudRate = PCOMM.B9600; break;
                case 19200: baudRate = PCOMM.B19200; break;
                case 38400: baudRate = PCOMM.B38400; break;
                case 57600: baudRate = PCOMM.B57600; break;
                case 115200: baudRate = PCOMM.B115200; break;
                case 230400: baudRate = PCOMM.B230400; break;
                case 460800: baudRate = PCOMM.B460800; break;
                case 921600: baudRate = PCOMM.B921600; break;
                default: baudRate = PCOMM.B9600; customBaudRate = true; break;
            }

            switch (DataBits)
            {
                case 5: dataBits = PCOMM.BIT_5; break;
                case 6: dataBits = PCOMM.BIT_6; break;
                case 7: dataBits = PCOMM.BIT_7; break;
                case 8: dataBits = PCOMM.BIT_8; break;
                default: dataBits = PCOMM.BIT_8; break;
            }

            switch (StopBits)
            {
                case 1: stopBits = PCOMM.STOP_1; break;
                case 2: stopBits = PCOMM.STOP_2; break;
                default: stopBits = PCOMM.STOP_1; break;
            }

            switch (Parity?.ToUpper())
            {
                case "NONE": parity = PCOMM.P_NONE; break;
                case "ODD": parity = PCOMM.P_ODD; break;
                case "EVEN": parity = PCOMM.P_EVEN; break;
                case "MARK": parity = PCOMM.P_MRK; break;
                case "SPACE": parity = PCOMM.P_SPC; break;
                default: parity = PCOMM.P_NONE; break;
            }

            int port = PortID;
            int mode = dataBits | stopBits | parity;
            int hw = IsHW ? 3 : 0;
            int sw = IsSW ? 12 : 0;

            if ((result = PCOMM.sio_ioctl(port, baudRate, mode)) != PCOMM.SIO_OK)
            {
                throw new Exception(PCOMM.GetErrorMessage(result));
            }

            if (customBaudRate)
            {
                if ((result = PCOMM.sio_baud(port, BaudRate)) != PCOMM.SIO_OK)
                {
                    throw new Exception(PCOMM.GetErrorMessage(result));
                }
            }

            if ((result = PCOMM.sio_flowctrl(port, hw | sw)) != PCOMM.SIO_OK)
            {
                throw new Exception(PCOMM.GetErrorMessage(result));
            }

            if ((result = PCOMM.sio_DTR(port, IsDTR ? 1 : 0)) != PCOMM.SIO_OK)
            {
                throw new Exception(PCOMM.GetErrorMessage(result));
            }

            if (!IsHW)
            {
                if ((result = PCOMM.sio_RTS(port, IsRTS ? 1 : 0)) != PCOMM.SIO_OK)
                {
                    throw new Exception(PCOMM.GetErrorMessage(result));
                }
            }

            if (TransferSettings.Engine == TransferEngineKind.OneWay)
            {
                result = PCOMM.sio_SetReadTimeouts(port, -1, 0);
                if (result != PCOMM.SIO_OK)
                    throw new IOException("Cannot enable non-blocking serial input: " + PCOMM.GetErrorMessage(result));
            }

            //PCOMM.sio_SetReadTimeouts(port, 60000, 1000);
            //PCOMM.sio_SetWriteTimeouts(port, 60000);

            if ((result = PCOMM.sio_flush(port, 2)) != PCOMM.SIO_OK)
            {
                throw new Exception(PCOMM.GetErrorMessage(result));
            }
        }

        /// <summary>
        /// Initialize serial port settings
        /// </summary>
        /// <returns>Whether initialization succeeded</returns>
        public bool InitialPort()
        {
            SizeLimit = int.Parse(ConfigurationManager.AppSettings["maxsize"] ?? "50");

            PortID = int.Parse(ConfigurationManager.AppSettings["com"]);
            BaudRate = int.Parse(ConfigurationManager.AppSettings["baudrate"]);
            DataBits = int.Parse(ConfigurationManager.AppSettings["databits"]);
            StopBits = int.Parse(ConfigurationManager.AppSettings["stopbits"]);
            Parity = ConfigurationManager.AppSettings["parity"].ToUpper();
            IsHW = ConfigurationManager.AppSettings["ishw"].ToUpper() == "TRUE";
            IsSW = ConfigurationManager.AppSettings["issw"].ToUpper() == "TRUE";
            IsDTR = ConfigurationManager.AppSettings["isdtr"].ToUpper() == "TRUE";
            IsRTS = ConfigurationManager.AppSettings["isrts"].ToUpper() == "TRUE";

            SaveDirectory = ConfigurationManager.AppSettings["directory"];

            InitialDirectory();

            InitialWorkingDirectory();

            PipelineManager.SendCommand(PipelineManager.CommandType.FolderList, GetDirectoryInfo());

            Notify(new { PortInfo, PortOption });

            return true;
        }
        public void RestartPort(int id, int baudrate)
        {
            if (IsOpen && !ClosePort()) return;

            Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            configuration.AppSettings.Settings["com"].Value = id.ToString();
            configuration.AppSettings.Settings["baudrate"].Value = baudrate.ToString();
            configuration.Save(ConfigurationSaveMode.Minimal, true);
            ConfigurationManager.RefreshSection("appSettings");
            OpenPort();
        }
        /// <summary>
        /// Initialize working folders
        /// </summary>
        private void InitialWorkingDirectory()
        {
            string workingfolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, WorkingDirectory);
            if (!Directory.Exists(workingfolder))
            {
                Directory.CreateDirectory(workingfolder);
            }
            File.SetAttributes(workingfolder, FileAttributes.Hidden);
            Directory.SetCurrentDirectory(workingfolder);
        }
        /// <summary>
        /// Initialize file save locations
        /// </summary>
        private void InitialDirectory()
        {
            directoryDict.Clear();
            if (SaveDirectory != null)
            {
                foreach (string option in SaveDirectory.Split('|'))
                {
                    string[] options = option.Split('>');
                    if (options.Length >= 2)
                    {
                        directoryDict.Add(options[0].ToUpper(), options[1]);
                    }
                }
            }
            if (!directoryDict.ContainsKey("*"))
            {
                directoryDict.Add("*", DefaultDirectory);
            }
        }

        /// <summary>
        /// Get file save location info
        /// </summary>
        /// <returns>file location info text</returns>
        public string GetDirectoryInfo()
        {
            return string.Join("|", directoryDict.Select(item => string.Format("{0}>{1}", item.Key, item.Value)));
        }
        /// <summary>
        /// Get the file save location by file extension
        /// </summary>
        /// <param name="extension">file extension</param>
        /// <param name="filename">file name</param>
        /// <returns>File save location</returns>
        public string GetDirectory(string extension, string filename)
        {
            string target;
            string path = GetLocation(filename);
            if (path != null)
            {
                target = path;
            }
            else if (extension == null)
            {
                target = DefaultDirectory;
            }
            else if (directoryDict == null)
            {
                target = DefaultDirectory;
            }
            else if (!directoryDict.ContainsKey(extension.ToUpper()))
            {
                target = directoryDict["*"];
            }
            else
            {
                target = directoryDict[extension.ToUpper()];
            }
            if (string.IsNullOrEmpty(target))
            {
                AddLog("Fault", "Cannot find target storage location: " + filename);
            }
            else
            {
                try
                {
                    if (!Directory.Exists(target))
                    {
                        AddLog("Operation", "Creating new folder: " + target);
                        Directory.CreateDirectory(target);
                    }
                }
                catch
                {
                    AddLog("Fault", "Cannot create folder: " + target);
                }
            }
            return target;
        }

        /// <summary>
        /// Open the serial port after a delay
        /// </summary>
        /// <param name="delay">delay</param>
        /// <param name="wait">retry wait time</param>
        /// <param name="retry">retry count</param>
        public void DelayOpen(int delay, int wait = 1000, int retry = 0)
        {
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(delay);

                if (IsOpen)
                {
                    return;
                }

                AddLog("Operation", "Auto-opening serial port: " + PortInfo);
                OpenPort();

                while (!IsOpen && retry > 0)
                {
                    AddLog("Operation", string.Format("Port open failed, retrying in {0} ms", wait));

                    Thread.Sleep(wait);

                    AddLog("Operation", "Retrying serial port open: " + PortInfo);
                    OpenPort();

                    retry -= 1;
                }
            });
        }

        /// <summary>
        /// Open the serial port
        /// </summary>
        /// <returns>Whether opening succeeded</returns>
        public bool OpenPort()
        {
            if (IsOpening || IsOpen || cancellation.IsCancellationRequested)
                return false;

            IsOpening = true;
            try
            {
                InitialPort();
            }
            catch (Exception e)
            {
                AddLog("Fault", "Failed to read configuration: " + e.Message);
                IsOpening = false;
                Notify(new { IsOpening });
                return false;
            }

            bool nativePortOpened = false;
            try
            {
                int result = PCOMM.sio_open(PortID);
                if (result != PCOMM.SIO_OK)
                {
                    AddLog("Fault", "Failed to open serial port: " + PCOMM.GetErrorMessage(result));
                    IsOpening = false;
                    Notify(new { IsOpening });
                    return false;
                }

                nativePortOpened = true;
                SetPort();
            }
            catch (Exception e)
            {
                if (nativePortOpened)
                {
                    lock (oneWayPortSync)
                    {
                        PCOMM.sio_close(PortID);
                    }
                }

                AddLog("Fault", "Failed to open serial port: " + e.Message);
                IsOpen = false;
                IsOpening = false;
                Notify(new { IsOpen, IsOpening });
                return false;
            }

            IsOpen = true;
            IsOpening = false;
            AddLog("Operation", "Serial port opened: " + PortInfo);
            Notify(new { IsOpen, IsOpening });
            StartTask();
            return true;
        }

        /// <summary>
        /// Close the serial port
        /// </summary>
        /// <returns>Whether closing succeeded</returns>
        public bool ClosePort()
        {
            int result;
            lock (oneWayPortSync)
            {
                StopTask();
                if (!IsOpen) return true;
                result = PCOMM.sio_close(PortID);
                if (result == PCOMM.SIO_OK) IsOpen = false;
            }

            if (result != PCOMM.SIO_OK)
            {
                AddLog("Fault", "Failed to close serial port: " + PCOMM.GetErrorMessage(result));
                return false;
            }

            AddLog("Operation", "Serial port closed: " + PortInfo);
            Notify(new { IsOpen });
            return true;
        }

        /// <summary>
        /// Send task buffer size
        /// </summary>
        private const int BufferSize = 100;
        /// <summary>
        /// Receive task
        /// </summary>
        private readonly Task receiveTask;
        /// <summary>
        /// Send task
        /// </summary>
        private readonly Task sendTask;
        /// <summary>
        /// File processing task
        /// </summary>
        private readonly Task fileTask;
        /// <summary>
        /// Status monitor task
        /// </summary>
        private readonly Task statusTask;
        /// <summary>
        /// Task exit flag
        /// </summary>
        private readonly CancellationTokenSource cancellation;
        /// <summary>
        /// File send list
        /// </summary>
        private readonly ConcurrentQueue<string> SendFileList;
        /// <summary>
        /// File receive list
        /// </summary>
        private readonly ConcurrentQueue<string> ReceiveFileList;
        /// <summary>
        /// File receive handler
        /// </summary>
        public EventHandler<FileSystemEventArgs> ReceiveHandler;
        /// <summary>
        /// Whether tasks have started
        /// </summary>
        public bool IsStarted { get; private set; }
        /// <summary>
        /// Whether a file is being sent
        /// </summary>
        public bool IsSending { get; private set; }
        /// <summary>
        /// Whether waiting to receive a file
        /// </summary>
        public bool IsReceiveWaiting { get; private set; }
        /// <summary>
        /// Whether a file is being received
        /// </summary>
        public bool IsReceiving { get; private set; }
        /// <summary>
        /// Name of the file being sent
        /// </summary>
        public string CurrentSendingFile { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public ComPort()
        {
            SendFileList = new ConcurrentQueue<string>();
            ReceiveFileList = new ConcurrentQueue<string>();

            try
            {
                InitialPort();
            }
            catch
            {

            }

            cancellation = new CancellationTokenSource();
            receiveTask = new Task(() =>
            {
                if (TransferSettings.Engine == TransferEngineKind.OneWay)
                {
                    RunOneWayReceiver();
                    return;
                }

                byte[] buffer = new byte[260];
                GCHandle gc_fn = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                IntPtr fnp = gc_fn.AddrOfPinnedObject();

                rCallBack cb = new rCallBack(ReceiveCallback);
                GCHandle gc = GCHandle.Alloc(cb);
                IntPtr cbp = Marshal.GetFunctionPointerForDelegate(cb);

                while (!cancellation.IsCancellationRequested)
                {
                    if (IsStarted && !IsSending)
                    {
                        string filename = string.Empty;
                        int fno = 1;
                        int result = 0;
                        try
                        {
                            result = PCOMM.sio_iqueue(PortID);
                            IsReceiving = false;
                            if (result > 0)
                            {
                                Array.Clear(buffer, 0, buffer.Length);
                                IsReceiveWaiting = true;
                                result = PCOMM.sio_FtZmodemRx(PortID, ref fnp, fno, cbp, FileKey);
                                filename = Encoding.Default.GetString(buffer).TrimEnd('\0');
                                if (!IsStarted)
                                {
                                    continue;
                                }
                                if (IsSending)
                                {
                                    continue;
                                }
                                if (result < 0)
                                {
                                    string message = PCOMM.GetTransferErrorMessage(result);
                                    AddLog("Receive", "File receive failed: " + message, filename);
                                }
                                else
                                {
                                    ReceiveFileList.Enqueue(filename);
                                    AddLog("Receive", "Processing file", filename);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            AddLog("Receive", "File receive failed: " + e.Message, filename);

                            SendErrorReport(string.Format("File {1} receive failed: {0}", e.Message, filename));
                        }
                        finally
                        {
                            IsReceiveWaiting = false;

                            ReceiveCount = 0;
                            ReceiveMax = 0;

                            Notify(new { ReceiveCount, ReceiveMax, ReceiveProgress, ReceivePercent, ReceiveTimeText });
                        }
                    }

                    Thread.Sleep(50);
                }

                gc_fn.Free();
                gc.Free();
            }, cancellation.Token, TaskCreationOptions.LongRunning);
            fileTask = new Task(() =>
            {
                while (!cancellation.IsCancellationRequested)
                {
                    if (IsStarted)
                    {
                        if (ReceiveFileList.Count > 0)
                        {
                            string filename = string.Empty;
                            ReceiveFileList.TryDequeue(out filename);
                            string shortname = filename;
                            string targetname = filename;

                            try
                            {
                                FileInfo fileInfo = new FileInfo(filename);
                                shortname = fileInfo.Name;
                                if (fileInfo.Extension.ToUpper() == ".GZ")
                                {
                                    AddLog("Receive", "Extracting file", shortname);
                                    using (FileStream originalFileStream = new FileStream(filename, FileMode.Open))
                                    {
                                        string currentFileName = fileInfo.FullName;
                                        string outputFileName = fileInfo.Name.Remove(fileInfo.Name.Length - fileInfo.Extension.Length);
                                        targetname = Path.Combine(GetDirectory(new FileInfo(outputFileName).Extension, outputFileName), fileInfo.Name.Remove(fileInfo.Name.Length - fileInfo.Extension.Length));

                                        using (FileStream decompressedFileStream = File.Create(targetname))
                                        {
                                            using (GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                                            {
                                                decompressionStream.CopyTo(decompressedFileStream);
                                            }
                                        }
                                    }
                                }
                                else if (fileInfo.Extension.ToUpper() == ".APPCOMMAND")
                                {
                                    AddLog("Receive", "Parsing command", shortname);
                                    try
                                    {
                                        string text = DecompressText(File.ReadAllBytes(filename));
                                        ResolveCommand(text);
                                    }
                                    catch
                                    {

                                    }
                                }
                                else
                                {
                                    AddLog("Receive", "Copying file", shortname);
                                    targetname = Path.Combine(GetDirectory(fileInfo.Extension, shortname), filename);
                                    File.Copy(filename, targetname, true);
                                }

                                File.Delete(filename);

                                AddLog("Receive", "File received successfully", shortname);
                                PipelineManager.SendCommand(PipelineManager.CommandType.FileReceived, targetname);
                                AddTransferRecord(false, targetname);

                                ReceiveHandler?.BeginInvoke(this, new FileSystemEventArgs(WatcherChangeTypes.Created, "", ""), null, null);
                            }
                            catch (Exception e)
                            {
                                AddLog("Receive", "File processing failed: " + e.Message, shortname);

                                SendErrorReport(string.Format("File {1} processing failed: {0}", e.Message, shortname));
                            }
                        }
                    }

                    Thread.Sleep(50);
                }
            }, cancellation.Token, TaskCreationOptions.LongRunning);
            sendTask = new Task(() =>
            {
                xCallBack cb = new xCallBack(SendCallback);
                GCHandle gc = GCHandle.Alloc(cb);
                IntPtr cbp = Marshal.GetFunctionPointerForDelegate(cb);

                while (!cancellation.IsCancellationRequested)
                {
                    if (IsStarted)
                    {
                        if (SendFileList.Count > 0)
                        {
                            string filename = string.Empty;
                            SendFileList.TryDequeue(out filename);
                            string shortname = filename;
                            string sourcename = filename;
                            string tempFolder = null;
                            try
                            {
                                FileInfo fileInfo = new FileInfo(filename);
                                shortname = fileInfo.Name;
                                CurrentSendingFile = shortname;
                                AddLog("Send", "Checking file attributes", shortname);
                                if (!fileInfo.Exists)
                                {
                                    TaskManager.Instance.FileSendedTaskHandler?.Invoke(this, new TaskManager.FileTaskEventArgs(filename, false));
                                    AddLog("Send", "File does not exist", shortname);
                                    SendErrorReport(string.Format("File not found: {0}", shortname), filename);
                                    filename = null;
                                }
                                if (fileInfo.Length >= 1024 * 1024 * SizeLimit)
                                {
                                    TaskManager.Instance.FileSendedTaskHandler?.Invoke(this, new TaskManager.FileTaskEventArgs(filename, false));
                                    AddLog("Send", "File size exceeds limit", shortname);
                                    SendErrorReport(string.Format("File size exceeds limit: {0}", shortname), filename);
                                    filename = null;
                                }
                                else if(fileInfo.Extension.ToUpper() != ".APPCOMMAND")
                                {
                                    AddLog("Send", "Compressing file", shortname);
                                    tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                                    Directory.CreateDirectory(tempFolder);
                                    string compressedFileName = Path.Combine(tempFolder, fileInfo.Name + ".gz");
                                    using (FileStream originalFileStream = new FileStream(filename, FileMode.Open))
                                    {
                                        using (FileStream compressedFileStream = File.Create(compressedFileName))
                                        {
                                            using (GZipStream compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress))
                                            {
                                                originalFileStream.CopyTo(compressionStream);
                                            }
                                        }
                                    }
                                    filename = compressedFileName;
                                }
                            }
                            catch (Exception e)
                            {
                                TaskManager.Instance.FileSendedTaskHandler?.Invoke(this, new TaskManager.FileTaskEventArgs(filename, false));
                                AddLog("Send", "File processing failed: " + e.Message, shortname);
                                SendErrorReport(string.Format("File {1} processing failed: {0}", e.Message, shortname), filename);
                                filename = null;
                                CurrentSendingFile = null;
                            }

                            if (filename != null)
                            {
                                AddLog("Send", "Sending file", shortname);
                                int result;
                                IsSending = true;
                                while (IsReceiveWaiting)
                                {
                                    Thread.Sleep(10);
                                }

                                try
                                {
                                    PCOMM.sio_flush(PortID, 2);
                                    result = PCOMM.sio_FtZmodemTx(PortID, filename, cbp, FileKey);

                                    if (result < 0)
                                    {
                                        string message = PCOMM.GetTransferErrorMessage(result);
                                        TaskManager.Instance.FileSendedTaskHandler?.Invoke(this, new TaskManager.FileTaskEventArgs(filename, false));
                                        AddLog("Send", "File send failed: " + message, shortname);
                                        SendErrorReport(string.Format("File {1} send failed: {0}", message, shortname), filename);
                                    }
                                    else
                                    {
                                        TaskManager.Instance.FileSendedTaskHandler?.Invoke(this, new TaskManager.FileTaskEventArgs(filename, true));
                                        PipelineManager.SendCommand(PipelineManager.CommandType.FileSended, sourcename);
                                        AddLog("Send", "File sent successfully", shortname);
                                        AddTransferRecord(true, sourcename);
                                    }
                                }
                                catch (Exception e)
                                {
                                    TaskManager.Instance.FileSendedTaskHandler?.Invoke(this, new TaskManager.FileTaskEventArgs(filename, false));
                                    AddLog("Send", "File send failed: " + e.Message, shortname);
                                    SendErrorReport(string.Format("File {1} send failed: {0}", e.Message, shortname), filename);
                                }
                                finally
                                {
                                    IsSending = false;
                                    CurrentSendingFile = null;

                                    try
                                    {
                                        File.Delete(filename);
                                        if (tempFolder != null)
                                        {
                                            Directory.Delete(tempFolder, true);
                                        }
                                    }
                                    catch
                                    {

                                    }

                                    SendCount = 0;
                                    SendMax = 0;

                                    Notify(new { SendCount, SendMax, SendProgress, SendPercent, SendTimeText });
                                }

                            }
                        }
                    }

                    Thread.Sleep(50);
                }

                gc.Free();
            }, cancellation.Token, TaskCreationOptions.LongRunning);
            statusTask = new Task(() =>
            {
                while (!cancellation.IsCancellationRequested)
                {
                    int result = PCOMM.sio_lstatus(PortID);
                    PortStatus = result;

                    Notify(new { PortStatus, Status_Open, Status_DSR, Status_CTS, Status_RI, Status_CD });

                    PipelineManager.SendCommand(PipelineManager.CommandType.ConnectParam, string.Format("COM{0},{1}", PortID, BaudRate));
                    PipelineManager.SendCommand(PipelineManager.CommandType.ConnectState, string.Join(",", Status_Open, Status_DSR, Status_CTS, Status_RI, Status_CD));

                    Thread.Sleep(500);
                }
            }, cancellation.Token, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Whether to issue the force-abort send command
        /// </summary>
        private bool IsForceStopSending;
        /// <summary>
        /// Whether to issue the force-abort receive command
        /// </summary>
        private bool IsForceStopReceiving;

        /// <summary>
        /// Force-abort file sending
        /// </summary>
        public void ForceStopSending()
        {
            IsForceStopSending = true;
        }

        /// <summary>
        /// Force-abort file receiving
        /// </summary>
        public void ForceStopReceiving()
        {
            if (TransferSettings.Engine == TransferEngineKind.OneWay)
            {
                lock (oneWayPortSync)
                {
                    Interlocked.Increment(ref oneWayResetVersion);
                    if (IsOpen) PCOMM.sio_flush(PortID, 0);
                }

                AddLog("Receive", "Receive cancellation requested. Resend the whole file to restart.");
                return;
            }

            IsForceStopReceiving = true;
        }

        /// <summary>
        /// Data send callback delegate
        /// </summary>
        /// <param name="xmitlen">length of data sent so far</param>
        /// <param name="buflen">length of the current data chunk</param>
        /// <param name="buf">content of the current data chunk</param>
        /// <param name="flen">total send length</param>
        /// <returns></returns>
        public delegate int xCallBack(int xmitlen, int buflen, byte[] buf, int flen);
        /// <summary>
        /// Data receive callback delegate
        /// </summary>
        /// <param name="recvlen">length of data received so far</param>
        /// <param name="buflen">length of the current data chunk</param>
        /// <param name="buf">content of the current data chunk</param>
        /// <param name="flen">total receive length</param>
        public delegate int rCallBack(int recvlen, int buflen, byte[] buf, int flen);

        /// <summary>
        /// Data receive callback
        /// </summary>
        /// <param name="recvlen">length of data received so far</param>
        /// <param name="buflen">length of the current data chunk</param>
        /// <param name="buf">content of the current data chunk</param>
        /// <param name="flen">total receive length</param>
        private int ReceiveCallback(int recvlen, int buflen, byte[] buf, int flen)
        {
            if (IsSending)
            {
                return -1;
            }

            if (IsStarted && !IsForceStopReceiving)
            {
                if (ReceiveCount == 0 && recvlen != 0)
                {
                    //AddLog("Receive", "Start receiving file");
                }
                if (recvlen != 0)
                {
                    IsReceiving = true;
                }
                else
                {
                    IsReceiving = false;
                }

                ReceiveCount = recvlen;
                ReceiveMax = flen;

                PipelineManager.SendCommand(PipelineManager.CommandType.DownloadProgress, string.Format("{0}/{1}", recvlen, flen));

                Notify(new { ReceiveCount, ReceiveMax, ReceiveProgress, ReceivePercent, ReceiveTimeText });

                return 0;
            }
            else
            {
                IsForceStopReceiving = false;

                ReceiveCount = 0;
                ReceiveMax = 0;

                Notify(new { ReceiveCount, ReceiveMax, ReceiveProgress, ReceivePercent, ReceiveTimeText });

                return -1;
            }
        }

        /// <summary>
        /// Data send callback
        /// </summary>
        /// <param name="xmitlen">length of data sent so far</param>
        /// <param name="buflen">length of the current data chunk</param>
        /// <param name="buf">content of the current data chunk</param>
        /// <param name="flen">total send length</param>
        private int SendCallback(int xmitlen, int buflen, byte[] buf, int flen)
        {
            if (IsStarted && !IsForceStopSending)
            {
                SendCount = xmitlen;
                SendMax = flen;

                if (!CurrentSendingFile.ToUpper().EndsWith(".APPCOMMAND"))
                {
                    PipelineManager.SendCommand(PipelineManager.CommandType.UploadProgress, string.Format("{0}/{1}", xmitlen, flen));
                }

                Notify(new { SendCount, SendMax, SendProgress, SendPercent, SendTimeText });

                return 0;
            }
            else
            {
                IsForceStopSending = false;

                SendCount = 0;
                SendMax = 0;

                Notify(new { SendCount, SendMax, SendProgress, SendPercent, SendTimeText });

                return -1;
            }
        }

        /// <summary>
        /// Scheduled-task file send event handler
        /// </summary>
        /// <param name="sender">event sender</param>
        /// <param name="e">event arguments</param>
        private void FileTaskHandler(object sender, TaskManager.FileTaskEventArgs e)
        {
            AddLog("Schedule", "Preparing to send", e.File);
            if (e.Message != null)
            {
                AddLog("Schedule", e.Message, e.File);
            }
            SendFile(e.File);
        }

        /// <summary>
        /// Start all tasks
        /// </summary>
        private void StartTask()
        {
            Interlocked.Increment(ref oneWayResetVersion);
            IsStarted = true;

            // Receiver deployment: never subscribe to scheduled sends.
            TaskManager.Instance.FileTaskHandler -= FileTaskHandler;

            try
            {
                if (receiveTask.Status == TaskStatus.Created)
                    receiveTask.Start();

                // OneWay validates and saves files directly.
                if (TransferSettings.Engine == TransferEngineKind.Zmodem &&
                    fileTask.Status == TaskStatus.Created)
                    fileTask.Start();

                // Receiver deployment: sendTask is intentionally never started.
                if (statusTask.Status == TaskStatus.Created)
                    statusTask.Start();
            }
            catch (Exception e)
            {
                AddLog("Fault", "Failed to start thread: " + e.Message);
            }
        }

        /// <summary>
        /// Stop all tasks
        /// </summary>
        private void StopTask()
        {
            Interlocked.Increment(ref oneWayResetVersion);
            TaskManager.Instance.FileTaskHandler -= FileTaskHandler;
            IsStarted = false;
            IsSending = false;
            IsReceiveWaiting = false;
            IsReceiving = false;
        }

        /// <summary>
        /// Release all managed resources
        /// </summary>
        public void Dispose()
        {
            cancellation.Cancel();
            if (IsOpen) ClosePort();
            else StopTask();
        }

        /// <summary>
        /// Set the remote file path
        /// </summary>
        /// <param name="filename">file path</param>
        public void SelectRemotePath(string filename)
        {
            if (IsReceiverOnly)
            {
                AddLog("Receiver", "Remote file selection is disabled in the receiver application.");
                return;
            }

            PullFilePath = filename;
            Notify(new { PullFilePath });
        }

        /// <summary>
        /// Set the local file path
        /// </summary>
        /// <param name="filename">file path</param>
        public void SelectFile(string filename)
        {
            SelectedFilePath = filename;
            Notify(new { SelectedFilePath });
        }

        /// <summary>
        /// Send File
        /// </summary>
        /// <param name="filename">file path</param>
        public void SendFile(string filename)
        {
            if (IsReceiverOnly)
            {
                AddLog("Receiver", "Sending is disabled in the receiver application.");
                return;
            }

            SendFileList.Enqueue(filename);

            AddLog("Send", "File send task added", filename);

            while (SendFileList.Count > BufferSize)
            {
                bool result = SendFileList.TryDequeue(out filename);

                if (result)
                {
                    AddLog("Send", "Expired file send task removed from buffer", filename);
                }
                else
                {
                    AddLog("Send", "Failed to remove expired send task", filename);
                }
            }
        }

        /// <summary>
        /// Whether a command is being processed
        /// </summary>
        private bool IsCommandBusy;

        /// <summary>
        /// Send an error report
        /// </summary>
        /// <param name="message">error message text</param>
        /// <param name="filename">file name</param>
        public void SendErrorReport(string message, string filename = null)
        {
            if (message==null || message.ToUpper().EndsWith(".APPCOMMAND"))
            {
                return;
            }
            if (filename != null && filename != LastFetchFile)
            {
                return;
            }
            SubmitCommand("errorreport", message);
        }

        /// <summary>
        /// Command to issue
        /// </summary>
        /// <param name="command">command text</param>
        /// <param name="param">command parameters</param>
        public void SubmitCommand(string command, string param)
        {
            if (IsReceiverOnly)
            {
                AddLog("Receiver", "Outgoing serial commands are disabled in the receiver application.");
                return;
            }

            if (command == null || command.Trim().Length == 0)
            {
                return;
            }
            if (param == null)
            {
                return;
            }

            if (IsCommandBusy)
            {
                return;
            }

            IsCommandBusy = true;

            string commandType;

            switch (command)
            {
                case "requestfile":
                    commandType = "Query Directory";
                    break;
                case "responsefile":
                    commandType = "Send Directory";
                    break;
                case "fetch":
                    commandType = "Pull File";
                    break;
                case "setlocation":
                    commandType = "Push File";
                    break;
                case "errorreport":
                    commandType = "Error Report";
                    break;
                default:
                    commandType = "Unknown";
                    break;
            }

            AddLog("Command", string.Format("Send {0} command to remote", commandType) );

            try
            {
                string tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempFolder);
                string filename = Path.Combine(tempFolder, DateTime.Now.ToString("yyyyMMddHHmmss") + ".APPCOMMAND");
                byte[] data = CompressTextToBytes(string.Format("{0} {1}", command, param));

                File.WriteAllBytes(filename, data);

                SendFile(filename);
            }
            catch (Exception e)
            {
                AddLog("Command", "Failed to send command: " + e.Message);
            }

            IsCommandBusy = false;
        }

        /// <summary>
        /// Last command text
        /// </summary>
        public string LastCommand;
        /// <summary>
        /// Last pulled file name
        /// </summary>
        private string LastFetchFile;
        /// <summary>
        /// Current file storage location
        /// </summary>
        private string setLocation;
        /// <summary>
        /// Timestamp of the current file storage location
        /// </summary>
        private DateTime setLocationTime;

        /// <summary>
        /// Get the current file save location
        /// </summary>
        /// <param name="filename">file name</param>
        /// <returns>target file path</returns>
        public string GetLocation(string filename)
        {
            if (setLocation == null || filename == null || DateTime.Now - setLocationTime > TimeSpan.FromMinutes(20) || !setLocation.EndsWith(filename))
            {
                return null;
            }
            string location = setLocation;
            ClearLocation();
            return location.Substring(0, location.Length - filename.Length);
        }

        /// <summary>
        /// Set the current file save location
        /// </summary>
        /// <param name="path">folder path</param>
        public void SetLocation(string path)
        {
            if (path.StartsWith("[") && path.Contains(@"]"))
            {
                return;
            }
            setLocation = path;
            setLocationTime = DateTime.Now;
        }

        /// <summary>
        /// Clear the current file save location
        /// </summary>
        public void ClearLocation()
        {
            setLocation = null;
        }

        /// <summary>
        /// Process command text
        /// </summary>
        /// <param name="command">command content</param>
        private void ResolveCommand(string command)
        {
            LastCommand = command;
            if (command == null)
            {
                return;
            }
            else if (command.StartsWith("fetch"))
            {
                string filename = command.Substring(5).Trim();
                LastFetchFile = filename;
                SendFile(filename);
            }
            else if (command.StartsWith("requestfile"))
            {
                string root = command.Substring(11).Trim();
                Task.Factory.StartNew(() => SubmitCommand("responsefile", GetFileTreeText(root)));
            }
            else if (command.StartsWith("responsefile"))
            {
                string result = command.Substring(12).Trim();
                PipelineManager.SendCommand(PipelineManager.CommandType.FileTreeResponse, result);
            }
            else if (command.StartsWith("setlocation"))
            {
                SetLocation(command.Substring(11).Trim());
            }
            else if (command.StartsWith("errorreport"))
            {
                string message = command.Substring(11).Trim();
                AddLog("Remote error", message);
            }
        }

        /// <summary>
        /// Compress a string
        /// </summary>
        /// <param name="text">string</param>
        /// <returns>compressed data</returns>
        private byte[] CompressTextToBytes(string text)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (DeflateStream deflateStream = new DeflateStream(stream, CompressionMode.Compress))
                    {
                        byte[] data = Encoding.Default.GetBytes(text);
                        deflateStream.Write(data, 0, data.Length);
                    }
                    return stream.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Decompress data to a string
        /// </summary>
        /// <param name="data">raw compressed data</param>
        /// <returns>string</returns>
        private string DecompressText(byte[] data)
        {
            try
            {
                using (MemoryStream outputStream = new MemoryStream())
                {
                    using (MemoryStream inputStream = new MemoryStream(data))
                    {
                        using (DeflateStream deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress))
                        {
                            deflateStream.CopyTo(outputStream);
                            return Encoding.Default.GetString(outputStream.ToArray());
                        }
                    }
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Build a string containing the directory listing info
        /// </summary>
        /// <param name="root">directory path</param>
        /// <returns>data string</returns>
        private string GetFileTreeText(string root)
        {
            try
            {
                if (root == null || root.Trim() == "")
                {
                    return "|" + string.Join("|", DriveInfo.GetDrives().Select(item => string.Format("D>[{0}]{1}", item.IsReady ? item.TotalSize : 0, item.Name)));
                }
                int count = 0;
                int rootLength = root.Length;
                StringBuilder stringBuilder = new StringBuilder(root + "|");
                foreach (string entry in Directory.EnumerateDirectories(root))
                {
                    FileInfo info = new FileInfo(entry);

                    if (info.Attributes.HasFlag(FileAttributes.Hidden))
                    {
                        continue;
                    }

                    stringBuilder.Append("F>");
                    stringBuilder.Append("[");
                    stringBuilder.Append(info.LastWriteTimeUtc);
                    stringBuilder.Append("]");
                    stringBuilder.Append(entry.Substring(rootLength).TrimStart('\\'));
                    stringBuilder.Append("|");

                    count += 1;
                    if (count >= 1000)
                    {
                        break;
                    }
                }
                foreach (string entry in Directory.EnumerateFiles(root))
                {
                    FileInfo info = new FileInfo(entry);

                    if (info.Attributes.HasFlag(FileAttributes.Hidden))
                    {
                        continue;
                    }

                    stringBuilder.Append("[");
                    stringBuilder.Append(info.Length);
                    stringBuilder.Append("]");
                    stringBuilder.Append("[");
                    stringBuilder.Append(info.LastWriteTimeUtc);
                    stringBuilder.Append("]");
                    stringBuilder.Append(entry.Substring(rootLength).TrimStart('\\'));
                    stringBuilder.Append("|");

                    count += 1;
                    if (count >= 1000)
                    {
                        break;
                    }
                }
                return stringBuilder.ToString();
            }
            catch
            {
            }
            return string.Empty;
        }

        /// <summary>
        /// Work log display limit
        /// </summary>
        private const int LogLimit = 200;
        /// <summary>
        /// Work log list
        /// </summary>
        public ObservableCollection<string> LogList { get; set; } = new ObservableCollection<string>();
        /// <summary>
        /// Transfer record display limit
        /// </summary>
        private const int RecordLimit = 100;
        /// <summary>
        /// Transfer record list
        /// </summary>
        public ObservableCollection<string> RecordList { get; set; } = new ObservableCollection<string>();
        
        /// <summary>
        /// Add a simple log entry
        /// </summary>
        /// <param name="message">log text</param>
        public void AddSimpleLog(string message)
        {
            try
            {
                Application.Current.Dispatcher?.Invoke((Action)(() =>
                {
                    LogList.Add(message);

                    while (LogList.Count > LogLimit)
                    {
                        LogList.RemoveAt(0);
                    }
                }));
            }
            catch
            {

            }
        }

        /// <summary>
        /// Add a work log entry
        /// </summary>
        /// <param name="brief">brief</param>
        /// <param name="message">message text</param>
        /// <param name="filename">file name</param>
        public void AddLog(string brief, string message, string filename = null)
        {
            if (TransferSettings.Engine == TransferEngineKind.Zmodem && filename != null && filename.ToUpper().EndsWith(".GZ"))
            {
                filename = filename.Remove(filename.Length - 3);
            }
            if (TransferSettings.Engine == TransferEngineKind.Zmodem && filename != null && filename.ToUpper().EndsWith(".APPCOMMAND"))
            {
                return;
            }
            string log = filename == null || filename.Length == 0 ? string.Format("[{0}] {1} {2}", DateTime.Now.ToString("MM-dd HH:mm:ss"), brief, message) : string.Format("[{0}] {1} {2} File: {3}", DateTime.Now.ToString("MM-dd HH:mm:ss"), brief, message, filename);

            try
            {
                Application.Current.Dispatcher?.Invoke((Action)(() =>
                {
                    LogList.Add(log);

                    while (LogList.Count > LogLimit)
                    {
                        LogList.RemoveAt(0);
                    }
                }));

                LogHelper.PushLog("SYSTEM", log);

                PipelineManager.SendCommand(PipelineManager.CommandType.WorkingLog, message);
            }
            catch
            {

            }
        }

        /// <summary>
        /// Add a file transfer record
        /// </summary>
        /// <param name="isSend">true for a send operation (false for receive)</param>
        /// <param name="filename">file name</param>
        public void AddTransferRecord(bool isSend, string filename)
        {
            if (TransferSettings.Engine == TransferEngineKind.Zmodem && filename != null && filename.ToUpper().EndsWith(".APPCOMMAND"))
            {
                return;
            }
            string message = string.Format("{0} file: {1}", isSend ? "Sent" : "Received", filename);
            string record = string.Format("[{0}] {1}", DateTime.Now.ToString("MM-dd HH:mm:ss"), message);

            try
            {
                Application.Current.Dispatcher?.Invoke((Action)(() =>
                {
                    RecordList.Add(record);

                    while (RecordList.Count > RecordLimit)
                    {
                        RecordList.RemoveAt(0);
                    }
                }));

                LogHelper.PushLog("RECORD", record);

                PipelineManager.SendCommand(PipelineManager.CommandType.TransferLog, message);
            }
            catch
            {

            }
        }

        /// <summary>
        /// Clear work log
        /// </summary>
        public void ClearLog()
        {
            LogList.Clear();
        }

        /// <summary>
        /// Clear transfer records
        /// </summary>
        public void ClearRecord()
        {
            RecordList.Clear();
        }
    }
}
