using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ComTransfer
{
    public sealed class OneWayFileReceiver : IDisposable
    {
        private readonly Func<string, string> resolveFolder;
        private readonly Action<string> log;
        private readonly Action<long, long> progress;
        private readonly Action<string> completed;
        private readonly long maxFileSize;
        private readonly int timeoutMilliseconds;
        private readonly OneWayFrameDecoder decoder;

        private readonly Stopwatch lastInput = Stopwatch.StartNew();
        private readonly Stopwatch lastFrame = Stopwatch.StartNew();

        private FileStream file;
        private SHA256 hash;
        private Guid transferId;

        private string temporaryPath;
        private string finalPath;

        private long expectedLength;
        private long receivedLength;
        private int chunkSize;
        private uint chunks;
        private uint nextSequence;

        private bool disposed;
        private bool reportedOrphan;

        public bool IsReceiving => file != null;

        public OneWayFileReceiver(
            Func<string, string> resolveFolder,
            Action<string> log,
            Action<long, long> progress,
            Action<string> completed,
            long maxFileSize = 50L * 1024 * 1024,
            int timeoutMilliseconds = 30000)
        {
            if (resolveFolder == null)
                throw new ArgumentNullException(nameof(resolveFolder));

            if (maxFileSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxFileSize));

            if (timeoutMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutMilliseconds));

            this.resolveFolder = resolveFolder;
            this.log = log;
            this.progress = progress;
            this.completed = completed;
            this.maxFileSize = maxFileSize;
            this.timeoutMilliseconds = timeoutMilliseconds;

            decoder = new OneWayFrameDecoder(
                OnFrame,
                message =>
                {
                    if (IsReceiving)
                        AbortTransfer(message);
                    else
                        WriteLog("Protocol: " + message);
                });
        }

        public void Feed(byte[] data, int count)
        {
            ThrowIfDisposed();
            CheckTimeout();

            if (count > 0)
                lastInput.Restart();

            decoder.Feed(data, count);
        }

        public void CheckTimeout()
        {
            ThrowIfDisposed();

            if (IsReceiving &&
                lastFrame.ElapsedMilliseconds >= timeoutMilliseconds)
            {
                AbortTransfer(
                    "Receive timeout: a valid next packet did not " +
                    "arrive. Resend the whole file.");

                decoder.Reset();
            }
            else if (decoder.HasBufferedData &&
                lastInput.ElapsedMilliseconds >= timeoutMilliseconds)
            {
                decoder.Reset();

                WriteLog(
                    "Incomplete packet discarded after receive timeout.");
            }
        }

        public void Reset(string reason)
        {
            ThrowIfDisposed();

            if (IsReceiving)
                AbortTransfer(reason);

            decoder.Reset();
            reportedOrphan = false;
        }

        private void OnFrame(
            OneWayFrameType type,
            Guid id,
            uint sequence,
            byte[] payload)
        {
            try
            {
                if (type == OneWayFrameType.Start)
                {
                    Begin(id, sequence, payload);
                    reportedOrphan = false;
                    return;
                }

                if (!IsReceiving || id != transferId)
                {
                    if (!reportedOrphan)
                    {
                        reportedOrphan = true;

                        WriteLog(
                            "Ignored packet without a matching active " +
                            "Start packet. Resend from the beginning.");
                    }

                    return;
                }

                if (sequence != nextSequence)
                    throw new InvalidDataException(
                        "Packet sequence mismatch.");

                if (type == OneWayFrameType.Data)
                    ReceiveData(payload);
                else if (type == OneWayFrameType.End)
                    Finish(payload);
            }
            catch (Exception e)
            {
                AbortTransfer(e.Message);
            }
        }

        private void Begin(Guid id, uint sequence, byte[] payload)
        {
            if (sequence != 0)
                throw new InvalidDataException(
                    "Start sequence must be zero.");

            string name;
            long length;
            int size;

            using (var reader =
                new BinaryReader(new MemoryStream(payload)))
            {
                int nameLength = reader.ReadInt32();

                if (nameLength < 1 ||
                    nameLength > 1024 ||
                    payload.Length != 16 + nameLength)
                {
                    throw new InvalidDataException(
                        "Invalid Start metadata length.");
                }

                name = new UTF8Encoding(false, true).GetString(
                    reader.ReadBytes(nameLength));

                length = reader.ReadInt64();
                size = reader.ReadInt32();
            }

            ValidateName(name);

            if (length < 0 || length >= maxFileSize)
                throw new InvalidDataException(
                    "File size exceeds the receiver limit.");

            if (size < 1 || size > OneWayProtocol.MaxPayloadSize)
                throw new InvalidDataException(
                    "Invalid chunk size.");

            long count =
                length / size + (length % size == 0 ? 0 : 1);

            if (count > uint.MaxValue - 1L)
                throw new InvalidDataException(
                    "Too many data packets.");

            if (IsReceiving)
                AbortTransfer(
                    "A new Start packet replaced an incomplete transfer.");

            string folder = resolveFolder(name);

            if (string.IsNullOrWhiteSpace(folder))
                throw new IOException(
                    "No receive folder is configured.");

            folder = Path.GetFullPath(folder);
            Directory.CreateDirectory(folder);

            temporaryPath = Path.Combine(
                folder,
                ".sericom-" + id.ToString("N") + "-" +
                Guid.NewGuid().ToString("N") + ".partial");

            finalPath = Path.Combine(
                folder,
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") +
                "_" + id.ToString("N").Substring(0, 8) + "_" + name);

            file = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                8192,
                FileOptions.SequentialScan);

            hash = SHA256.Create();

            transferId = id;
            expectedLength = length;
            receivedLength = 0;
            chunkSize = size;
            chunks = 0;
            nextSequence = 1;

            lastFrame.Restart();

            WriteLog(
                "Start receiving: " + name +
                " (" + length + " bytes). Destination: " + folder);

            ReportProgress(0, expectedLength);
        }

        private void ReceiveData(byte[] payload)
        {
            int expected = (int)Math.Min(
                chunkSize, expectedLength - receivedLength);

            if (expected <= 0 || payload.Length != expected)
                throw new InvalidDataException(
                    "Unexpected data-packet size.");

            file.Write(payload, 0, payload.Length);

            hash.TransformBlock(
                payload, 0, payload.Length, payload, 0);

            receivedLength += payload.Length;
            chunks++;
            nextSequence++;

            lastFrame.Restart();

            ReportProgress(receivedLength, expectedLength);
        }

        private void Finish(byte[] payload)
        {
            if (payload.Length != 44)
                throw new InvalidDataException(
                    "Invalid End metadata length.");

            byte[] expectedHash;

            using (var reader =
                new BinaryReader(new MemoryStream(payload)))
            {
                long finalLength = reader.ReadInt64();
                uint finalChunks = reader.ReadUInt32();

                if (finalLength != expectedLength ||
                    receivedLength != expectedLength ||
                    finalChunks != chunks)
                {
                    throw new InvalidDataException(
                        "Final file length or packet count mismatch.");
                }

                expectedHash = reader.ReadBytes(32);
            }

            hash.TransformFinalBlock(new byte[0], 0, 0);

            byte[] actualHash = hash.Hash;
            int difference = 0;

            for (int i = 0; i < 32; i++)
                difference |= actualHash[i] ^ expectedHash[i];

            if (difference != 0)
                throw new InvalidDataException(
                    "File SHA256 mismatch.");

            file.Flush(true);
            file.Dispose();

            string savedPath = finalPath;

            File.Move(temporaryPath, savedPath);
            hash.Dispose();

            ClearState();

            WriteLog("File verified and saved: " + savedPath);

            WriteLog(
                "SHA256: " +
                BitConverter.ToString(actualHash).Replace("-", ""));

            ReportProgress(0, 0);

            try
            {
                completed?.Invoke(savedPath);
            }
            catch (Exception e)
            {
                WriteLog(
                    "File is saved, but completion notification failed: " +
                    e.Message);
            }
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                name.Length > 180 ||
                name == "." ||
                name == ".." ||
                name.EndsWith(".") ||
                name.EndsWith(" "))
            {
                throw new InvalidDataException(
                    "Invalid or overly long filename.");
            }

            foreach (char value in name)
            {
                if (char.IsControl(value) ||
                    "<>:\"/\\|?*".IndexOf(value) >= 0)
                {
                    throw new InvalidDataException(
                        "Filename contains a path or invalid character.");
                }
            }

            string stem = name.Split('.')[0].ToUpperInvariant();

            if (stem == "CON" ||
                stem == "PRN" ||
                stem == "AUX" ||
                stem == "NUL" ||
                (stem.Length == 4 &&
                    (stem.StartsWith("COM") ||
                     stem.StartsWith("LPT")) &&
                    stem[3] >= '1' &&
                    stem[3] <= '9'))
            {
                throw new InvalidDataException(
                    "Reserved Windows filename.");
            }
        }

        private void AbortTransfer(string reason)
        {
            try
            {
                file?.Dispose();
            }
            catch (Exception e)
            {
                WriteLog(
                    "Failed to close partial file: " + e.Message);
            }

            try
            {
                hash?.Dispose();
            }
            catch
            {
            }

            try
            {
                if (temporaryPath != null &&
                    File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (Exception e)
            {
                WriteLog(
                    "Partial file could not be removed: " +
                    temporaryPath + ". " + e.Message);
            }

            ClearState();
            WriteLog("Receive failed/canceled: " + reason);
            ReportProgress(0, 0);
        }

        private void ClearState()
        {
            file = null;
            hash = null;
            temporaryPath = null;
            finalPath = null;
            transferId = Guid.Empty;
            expectedLength = 0;
            receivedLength = 0;
            chunks = 0;
            nextSequence = 1;
        }

        private void WriteLog(string message)
        {
            try
            {
                log?.Invoke(message);
            }
            catch
            {
            }
        }

        private void ReportProgress(long current, long total)
        {
            try
            {
                progress?.Invoke(current, total);
            }
            catch
            {
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(OneWayFileReceiver));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (IsReceiving)
                AbortTransfer(
                    "Receiver stopped before the file was complete.");

            decoder.Reset();
            disposed = true;
        }
    }
}