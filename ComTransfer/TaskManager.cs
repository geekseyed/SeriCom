using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ComTransfer
{
    /// <summary>
    /// Basic class that stores scheduled task info
    /// </summary>
    [Serializable]
    [XmlType(TypeName = "Task")]
    public class TaskNode
    {
        /// <summary>
        /// Trigger time
        /// </summary>
        [XmlAttribute]
        public int Time { get; set; }

        /// <summary>
        /// File modified-time start
        /// </summary>
        [XmlAttribute]
        public int FileTime_Head { get; set; }

        /// <summary>
        /// File modified-time end
        /// </summary>
        [XmlAttribute]
        public int FileTime_Tail { get; set; }

        /// <summary>
        /// Target folder
        /// </summary>
        [XmlAttribute]
        public string Folder { get; set; } = @"C:\";

        /// <summary>
        /// File extension
        /// </summary>
        [XmlAttribute]
        public string Extension { get; set; } = "*.*";

        /// <summary>
        /// Trigger time (hours)
        /// </summary>
        [XmlIgnore]
        public int Hour
        {
            get => Time / 60;
            set => Time = value * 60 + Time % 60;
        }
        /// <summary>
        /// Trigger time (minutes)
        /// </summary>
        [XmlIgnore]
        public int Minute
        {
            get => Time % 60;
            set => Time = Time - (Time % 60) + value;
        }
        /// <summary>
        /// Actual trigger time
        /// </summary>
        public DateTime TriggerTime => DateTime.Now.Date + TimeSpan.FromMinutes(Time);
        /// <summary>
        /// Actual file modified-time start
        /// </summary>
        public DateTime HeadTime => DateTime.Now - TimeSpan.FromMinutes(FileTime_Head);
        /// <summary>
        /// Actual file modified-time end
        /// </summary>
        public DateTime TailTime => DateTime.Now - TimeSpan.FromMinutes(FileTime_Tail);
        /// <summary>
        /// Text description of the trigger time
        /// </summary>
        public string TriggerTimeText => string.Format("{0:00}:{1:00}", Time / 60, Time % 60);
        /// <summary>
        /// Get the text info
        /// </summary>
        /// <returns>text string</returns>
        public override string ToString()
        {
            return string.Format("Send {2} files in folder {1} daily at {0}; file modified between {3:f1}h and {4:f1}h", TriggerTimeText, Folder, Extension, FileTime_Head / 60d, FileTime_Tail / 60d);
        }
        /// <summary>
        /// Get a shallow copy
        /// </summary>
        /// <returns>copy of the current instance</returns>
        public TaskNode Clone()
        {
            return MemberwiseClone() as TaskNode;
        }
    }
    /// <summary>
    /// Basic class describing a failed-to-send file
    /// </summary>
    [Serializable]
    [XmlType(TypeName = "File")]
    public class FailFileNode
    {
        /// <summary>
        /// file path
        /// </summary>
        [XmlAttribute]
        public string FilePath { get; set; }
        /// <summary>
        /// Failure time
        /// </summary>
        [XmlAttribute]
        public DateTime FailTime { get; set; }
        /// <summary>
        /// Next retry time
        /// </summary>
        [XmlAttribute]
        public DateTime RetryTime { get; set; }
        /// <summary>
        /// Failure count
        /// </summary>
        [XmlAttribute]
        public int FailCount { get; set; }
    }
    /// <summary>
    /// Scheduled task manager
    /// </summary>
    public class TaskManager
    {
        /// <summary>
        /// Singleton instance
        /// </summary>
        public static TaskManager Instance = new TaskManager();
        /// <summary>
        /// Scheduled task list
        /// </summary>
        public ObservableCollection<TaskNode> TaskList { get; set; }
        /// <summary>
        /// Failed-send file list
        /// </summary>
        public List<FailFileNode> FailFileList { get; set; }
        /// <summary>
        /// Read the scheduled task list data
        /// </summary>
        /// <returns>Scheduled task list</returns>
        public static List<TaskNode> ReadData()
        {
            try
            {
                using (StreamReader sr = new StreamReader("TaskList.xml", Encoding.UTF8))
                {
                    XmlSerializer reader = new XmlSerializer(typeof(List<TaskNode>));
                    return reader.Deserialize(sr) as List<TaskNode>;
                }
            }
            catch
            {
                return new List<TaskNode>();
            }
        }

        /// <summary>
        /// Save the scheduled task list data
        /// </summary>
        /// <param name="list">Scheduled task list</param>
        public static void SaveData(List<TaskNode> list)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter("TaskList.xml", false, Encoding.UTF8))
                {
                    XmlSerializer writer = new XmlSerializer(typeof(List<TaskNode>));
                    writer.Serialize(sw, list);
                }
            }
            catch
            {
            }
        }
        /// <summary>
        /// Read the failed-send file list data
        /// </summary>
        /// <returns>failed-send file list data</returns>
        public static List<FailFileNode> ReadFailFileData()
        {
            try
            {
                using (StreamReader sr = new StreamReader("FailFileList.xml", Encoding.UTF8))
                {
                    XmlSerializer reader = new XmlSerializer(typeof(List<FailFileNode>));
                    return reader.Deserialize(sr) as List<FailFileNode>;
                }
            }
            catch
            {
                return new List<FailFileNode>();
            }
        }
        /// <summary>
        /// Save the failed-send file list data
        /// </summary>
        /// <param name="list">failed-send file list data</param>
        public static void SaveFailFileData(List<FailFileNode> list)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter("FailFileList.xml", false, Encoding.UTF8))
                {
                    XmlSerializer writer = new XmlSerializer(typeof(List<FailFileNode>));
                    writer.Serialize(sw, list);
                }
            }
            catch
            {
            }
        }
        /// <summary>
        /// Scheduled-task file send event args
        /// </summary>
        public class FileTaskEventArgs : EventArgs
        {
            /// <summary>
            /// file path
            /// </summary>
            private readonly string file;
            /// <summary>
            /// whether the send succeeded
            /// </summary>
            private readonly bool succeed;
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="filename">file name</param>
            public FileTaskEventArgs(string filename)
            {
                file = filename;
            }
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="filename">file name</param>
            /// <param name="isSucceed">whether the send succeeded</param>
            public FileTaskEventArgs(string filename, bool isSucceed)
            {
                file = filename;
                succeed = isSucceed;
            }
            /// <summary>
            /// Error message
            /// </summary>
            public string Message { get; set; }

            /// <summary>
            /// file path
            /// </summary>
            public string File => file;
            /// <summary>
            /// whether the send succeeded
            /// </summary>
            public bool IsSucceed => succeed;
        }
        public EventHandler<FileTaskEventArgs> FileTaskHandler;
        /// <summary>
        /// Handler for file send completion
        /// </summary>
        public EventHandler<FileTaskEventArgs> FileSendedTaskHandler;
        /// <summary>
        /// Constructor
        /// </summary>
        private TaskManager()
        {
            TaskList = new ObservableCollection<TaskNode>();
            ReadData().ForEach(item => TaskList.Add(item));

            FailFileList = ReadFailFileData();

            Start();

            FileSendedTaskHandler += FileSendedHandler;
        }
        /// <summary>
        /// File send completion event handler
        /// </summary>
        /// <param name="sender">event sender</param>
        /// <param name="e">event arguments</param>
        private void FileSendedHandler(object sender, FileTaskEventArgs e)
        {
            string filename = e.File;
            FailFileNode node;

            lock (FailFileList)
            {
                node = FailFileList.FirstOrDefault(item => item.FilePath == filename && item.FailCount >= 0 && item.FailCount < 3);
            }

            if (node != null)
            {
                if (e.IsSucceed)
                {
                    node.FailCount = -1;
                }
                else
                {
                    node.FailCount++;
                    node.FailTime = DateTime.Now;
                    node.RetryTime = DateTime.Now + TimeSpan.FromHours(node.FailCount);
                }
            }
        }

        /// <summary>
        /// Refresh all scheduled tasks
        /// </summary>
        public void Refresh()
        {
            TaskList.Clear();
            ReadData().ForEach(item => TaskList.Add(item));
        }

        /// <summary>
        /// Start the monitor task
        /// </summary>
        private void Start()
        {
            Task.Factory.StartNew(() =>
            {
                DateTime currentTime = DateTime.Now;

                while (true)
                {
                    DateTime previousTime = currentTime;
                    currentTime = DateTime.Now;

                    while (FailFileList.Count > 100)
                    {
                        FailFileList.RemoveAt(0);
                    }

                    // remove invalid failed-file entries
                    FailFileList.RemoveAll(item => item.FailCount == -1 || item.FailCount >= 3);

                    // try to resend failed file entries
                    foreach (FailFileNode node in FailFileList)
                    {
                        if (node.FailCount == 0)
                        {
                            continue;
                        }
                        DateTime taskTime = node.RetryTime;
                        if (previousTime < taskTime && currentTime >= taskTime)
                        {
                            try
                            {
                                FileInfo fileInfo = new FileInfo(node.FilePath);
                                if (fileInfo.Exists)
                                {
                                    FileTaskHandler?.Invoke(this, new FileTaskEventArgs(fileInfo.FullName) { Message = string.Format("Retry count: {0}", node.FailCount) });
                                }
                            }
                            catch
                            {

                            }
                        }
                    }

                    List<TaskNode> list = TaskList.ToList();

                    // check scheduled task info
                    foreach (TaskNode task in TaskList)
                    {
                        if (task.Folder == null || task.Folder.Length == 0 || task.Extension == null || task.Extension.Length == 0)
                        {
                            continue;
                        }
                        DateTime taskTime = task.TriggerTime;
                        if (previousTime < taskTime && currentTime >= taskTime)
                        {
                            try
                            {
                                foreach(string file in Directory.GetFiles(task.Folder, task.Extension, SearchOption.TopDirectoryOnly))
                                {
                                    FileInfo fileInfo = new FileInfo(file);
                                    if (fileInfo.Exists)
                                    {
                                        DateTime timestamp = fileInfo.LastWriteTime;
                                        if (timestamp >= task.TailTime && timestamp <= task.HeadTime)
                                        {
                                            FailFileList.Add(new FailFileNode() { FilePath = fileInfo.FullName });
                                            FileTaskHandler?.Invoke(this, new FileTaskEventArgs(fileInfo.FullName));
                                        }
                                        else if (timestamp <= task.TailTime && timestamp >= task.HeadTime)
                                        {
                                            FailFileList.Add(new FailFileNode() { FilePath = fileInfo.FullName });
                                            FileTaskHandler?.Invoke(this, new FileTaskEventArgs(fileInfo.FullName));
                                        }
                                    }
                                }
                            }
                            catch
                            {

                            }
                        }
                    }


                    Thread.Sleep(60000);
                }
            }, TaskCreationOptions.LongRunning);
        }
    }
}
