using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace ComTransfer
{
    /// <summary>
    /// Logging helper
    /// </summary>
    internal static class LogHelper
    {
        /// <summary>
        /// Last log update time
        /// </summary>
        private static DateTime lastSaveTime = DateTime.MinValue;
        /// <summary>
        /// Log folder list
        /// </summary>
        private static readonly List<string> FolderList = new List<string>() { "SYSTEM", "RECORD" };
        /// <summary>
        /// Expiry time
        /// </summary>
        private static int expiredDays = 30;
        private static string lastMessage = "";
        private static int sameMessageCount = 0;

        /// <summary>
        /// Get the folder path
        /// </summary>
        /// <param name="folder">folder name</param>
        /// <returns>folder path</returns>
        public static string GetFolder(string folder)
        {
            return folder == null ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs") : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", folder);
        }
        /// <summary>
        /// Check all log folders and create missing ones
        /// </summary>
        /// <returns>Whether creation succeeded</returns>
        public static bool CheckFolder()
        {
            try
            {
                foreach (string folder in FolderList)
                {
                    Directory.CreateDirectory(GetFolder(folder));
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsInitialized;
        private static Queue<KeyValuePair<string, string>> logQueue = new Queue<KeyValuePair<string, string>>();

        public static void Initialize()
        {
            IsInitialized = true;

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    while (logQueue.Count > 0)
                    {
                        var pair = logQueue.Dequeue();
                        WriteLog(pair.Key, pair.Value);
                    }

                    Thread.Sleep(500);
                }
            }, TaskCreationOptions.LongRunning);
        }

        public static void PushLog(string name, string text)
        {
            if (!IsInitialized)
            {
                Initialize();
            }

            logQueue.Enqueue(new KeyValuePair<string, string>(name, text));

            while (logQueue.Count > 256)
            {
                logQueue.Dequeue();
            }
        }

        private static bool WriteLog(string name, string text)
        {
            if (name + text == lastMessage)
            {
                sameMessageCount++;

                if (sameMessageCount == 10)
                {
                    text = "Same log repeated many times, stop logging duplicates...";
                }
                else if (sameMessageCount > 10)
                {
                    return true;
                }
            }
            else
            {
                lastMessage = name + text;
                sameMessageCount = 0;
            }

            DateTime currentTime = DateTime.Now;
            name = name.ToUpper();
            if (!FolderList.Contains(name))
            {
                FolderList.Add(name);
                bool result = CheckFolder();
                if (!result)
                {
                    return false;
                }
            }
            if (lastSaveTime == DateTime.MinValue)
            {
                //check folders on first write
                bool result = CheckFolder();
                if (!result)
                {
                    return false;
                }
            }
            if (currentTime.Date != lastSaveTime.Date)
            {
                //delete expired logs on the first write of the day
                DeleteExpiredLogs();
            }
            lastSaveTime = currentTime;

            string dataString = text ?? string.Empty;
            string log = dataString;
            string path = Path.Combine(GetFolder(name), currentTime.ToString("yyyyMMdd") + ".log");
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                //stop writing when the log file exceeds 800MB
                if (fileInfo.Exists && fileInfo.Length > 800 * 1024 * 1024)
                {
                    return false;
                }
                using (StreamWriter sw = new StreamWriter(path, true, Encoding.UTF8))
                {
                    sw.WriteLine(log);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Set expiry days
        /// </summary>
        /// <param name="days">expiry days</param>
        public static void SetExpiredDays(int days)
        {
            expiredDays = days;
        }

        /// <summary>
        /// Delete expired logs
        /// </summary>
        /// <returns>Whether deletion succeeded</returns>
        public static bool DeleteExpiredLogs()
        {
            DateTime expireTime = DateTime.Now - TimeSpan.FromDays(expiredDays);
            try
            {
                foreach (string folder in FolderList)
                {
                    foreach (string file in Directory.GetFiles(GetFolder(folder), "*.log"))
                    {
                        if (File.GetCreationTime(file) < expireTime)
                        {
                            File.Delete(file);
                        }
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}