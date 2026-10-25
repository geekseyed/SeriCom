using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ComTransfer
{
    /// <summary>
    /// Named-pipe communication manager
    /// </summary>
    internal class PipelineManager
    {
        /// <summary>
        /// command type
        /// </summary>
        public enum CommandType
        {
            /// <summary>
            /// default command
            /// </summary>
            Default,
            /// <summary>
            /// Work Log
            /// </summary>
            WorkingLog,
            /// <summary>
            /// File transfer log
            /// </summary>
            TransferLog,
            /// <summary>
            /// Connection parameters
            /// </summary>
            ConnectParam,
            /// <summary>
            /// Connection status
            /// </summary>
            ConnectState,
            /// <summary>
            /// Upload progress
            /// </summary>
            UploadProgress,
            /// <summary>
            /// Download progress
            /// </summary>
            DownloadProgress,
            /// <summary>
            /// Directory listing request
            /// </summary>
            FileTreeRequest,
            /// <summary>
            /// Directory listing response
            /// </summary>
            FileTreeResponse,
            /// <summary>
            /// File send request
            /// </summary>
            FilePushRequest,
            /// <summary>
            /// File send response
            /// </summary>
            FilePushResponse,
            /// <summary>
            /// File pull request
            /// </summary>
            FilePullRequest,
            /// <summary>
            /// File pull response
            /// </summary>
            FilePullResponse,
            /// <summary>
            /// File sent
            /// </summary>
            FileSended,
            /// <summary>
            /// File received
            /// </summary>
            FileReceived,
            /// <summary>
            /// Directory listing
            /// </summary>
            FolderList,
            /// <summary>
            /// Stop sending
            /// </summary>
            StopSend,
            /// <summary>
            /// Stop receiving
            /// </summary>
            StopReceive
        }
        /// <summary>
        /// Pipe name
        /// </summary>
        public const string PipelineName = "PIPE_COMTRANSFER";
        /// <summary>
        /// Whether the named pipe is connected
        /// </summary>
        public static bool IsConnected => stream != null && stream.IsConnected;
        /// <summary>
        /// Named-pipe communication stream
        /// </summary>
        private static NamedPipeClientStream stream;
        /// <summary>
        /// Write stream
        /// </summary>
        private static StreamWriter writer = null;
        /// <summary>
        /// Read stream
        /// </summary>
        private static StreamReader reader = null;
        /// <summary>
        /// Command send buffer
        /// </summary>
        private static readonly Queue<string> MessageQueue = new Queue<string>();
        /// <summary>
        /// Main window
        /// </summary>
        private static MainWindow mainWindow;
        /// <summary>
        /// Serial communication manager
        /// </summary>
        private static ComPort comPort;
        /// <summary>
        /// Initialize named-pipe communication
        /// </summary>
        /// <param name="window">main window</param>
        /// <param name="port">serial communication instance</param>
        public static void Initialize(MainWindow window, ComPort port)
        {
            mainWindow = window;
            comPort = port;

            StartMonitoring();
        }
        /// <summary>
        /// Reset the named-pipe client
        /// </summary>
        private static void ResetClient()
        {
            stream?.Dispose();

            stream = new NamedPipeClientStream(".", PipelineName, PipeDirection.InOut, PipeOptions.Asynchronous);
            stream.Connect();
            reader = new StreamReader(stream);
            writer = new StreamWriter(stream) { AutoFlush = true };
        }
        /// <summary>
        /// Send command text
        /// </summary>
        /// <param name="command">command type</param>
        /// <param name="data">text data</param>
        public static void SendCommand(CommandType command, string data)
        {
            if (data != null && data.ToUpper().EndsWith(".APPCOMMAND"))
            {
                return;
            }

            string text = string.Format("{0}:{1}", command, data);
            WriteMessage(text);
        }

        /// <summary>
        /// Start the monitor task
        /// </summary>
        private static void StartMonitoring()
        {
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    if (stream == null || !stream.IsConnected)
                    {
                        ResetClient();
                    }

                    SendCommand(CommandType.FolderList, comPort.GetDirectoryInfo());

                    if (stream.CanRead && !reader.EndOfStream)
                    {
                        string message = reader.ReadLine();
                        ResolveMessage(message);
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
            }, TaskCreationOptions.LongRunning);

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    if (stream != null && stream.IsConnected && stream.CanWrite)
                    {
                        while (MessageQueue.Count > 0)
                        {
                            try
                            {
                                string message = MessageQueue.Dequeue();
                                writer.WriteLine(message);
                                stream.WaitForPipeDrain();
                            }
                            catch
                            {

                            }
                        }
                    }
                    
                    Thread.Sleep(100);
                }
            }, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Command text processing
        /// </summary>
        /// <param name="message">command text content</param>
        private static void ResolveMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            int offset = message.IndexOf(':');
            if (offset == -1)
            {
                return;
            }

            string command = message.Substring(0, offset);
            string data = message.Substring(offset + 1);

            CommandType commandType = CommandType.Default;
            try
            {
                commandType = (CommandType)Enum.Parse(typeof(CommandType), command);
            }
            catch
            {

            }

            switch (commandType)
            {
                case CommandType.Default:
                    break;
                case CommandType.ConnectParam:
                    {
                        try
                        {
                            string[] parameters = data.Split(',');
                            int.TryParse(parameters[0].Substring(3), out int id);
                            int.TryParse(parameters[1], out int baudrate);
                            comPort.RestartPort(id, baudrate);
                        }
                        catch
                        {
                        }
                    }
                    break;
                case CommandType.FileTreeRequest:
                    mainWindow.SubmitCommand("requestfile", data);
                    break;
                case CommandType.FilePushRequest:
                    if (!string.IsNullOrEmpty(data) && data.IndexOf('>') != -1)
                    {
                        int index = data.IndexOf('>');
                        string localPath = data.Substring(0, index);
                        string remotePath = data.Substring(index + 1);
                        try
                        {
                            comPort.SubmitCommand("setlocation", Path.Combine(remotePath, new FileInfo(localPath).Name));
                            comPort.SendFile(localPath);
                            SendCommand(CommandType.FilePushResponse, "");
                        }
                        catch
                        {

                        }
                    }
                    break;
                case CommandType.FilePullRequest:
                    if (!string.IsNullOrEmpty(data) && data.IndexOf('>') != -1)
                    {
                        int index = data.IndexOf('>');
                        string remotePath = data.Substring(0, index);
                        string localPath = data.Substring(index + 1);
                        try
                        {
                            comPort.SetLocation(Path.Combine(localPath, new FileInfo(remotePath).Name));
                            comPort.SubmitCommand("fetch", remotePath);
                            SendCommand(CommandType.FilePullResponse, "");
                        }
                        catch
                        {

                        }
                    }
                    break;
                case CommandType.StopReceive:
                    if (comPort.IsReceiving)
                    {
                        comPort.ForceStopReceiving();
                    }
                    break;
                case CommandType.StopSend:
                    if (comPort.IsSending)
                    {
                        comPort.ForceStopSending();
                    }
                    break;
            }
        }
        /// <summary>
        /// Write command text to the buffer
        /// </summary>
        /// <param name="message">command text</param>
        public static void WriteMessage(string message)
        {
            if (!IsConnected)
            {
                return;
            }

            MessageQueue.Enqueue(message);

            while (MessageQueue.Count > 256)
            {
                MessageQueue.Dequeue();
            }
        }
    }
}
