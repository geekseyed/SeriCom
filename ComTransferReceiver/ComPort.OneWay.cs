using System;
using System.IO;
using System.Threading;

namespace ComTransfer
{
    public partial class ComPort
    {
        private readonly object oneWayPortSync = new object();
        private int oneWayResetVersion;

        public bool IsReceiverOnly => true;

        private void RunOneWayReceiver()
        {
            byte[] buffer = new byte[8192];

            int observedVersion = -1;
            string lastPortError = null;

            using (var receiver = new OneWayFileReceiver(
                name => GetDirectory(Path.GetExtension(name), name),

                message => AddLog("Receive", message),

                (current, total) =>
                {
                    ReceiveCount = current;
                    ReceiveMax = total;

                    Notify(new
                    {
                        ReceiveCount,
                        ReceiveMax,
                        ReceiveProgress,
                        ReceivePercent,
                        ReceiveTimeText
                    });
                },

                path =>
                {
                    AddTransferRecord(false, path);

                    PipelineManager.SendCommand(
                        PipelineManager.CommandType.FileReceived,
                        path);

                    ReceiveHandler?.BeginInvoke(
                        this,
                        new FileSystemEventArgs(
                            WatcherChangeTypes.Created,
                            Path.GetDirectoryName(path),
                            Path.GetFileName(path)),
                        null,
                        null);
                },

                Math.Max(1L, SizeLimit) * 1024 * 1024))
            {
                try
                {
                    AddLog(
                        "Receive",
                        "OneWay receiver worker started.");

                    while (!cancellation.IsCancellationRequested)
                    {
                        int version =
                            Volatile.Read(ref oneWayResetVersion);

                        if (version != observedVersion)
                        {
                            receiver.Reset(
                                "Port state changed or receive " +
                                "cancellation was requested.");

                            observedVersion = version;
                        }

                        if (!IsStarted || !IsOpen)
                        {
                            IsReceiving = false;
                            cancellation.Token.WaitHandle.WaitOne(20);
                            continue;
                        }

                        try
                        {
                            int count = 0;

                            lock (oneWayPortSync)
                            {
                                if (IsStarted &&
                                    IsOpen &&
                                    observedVersion ==
                                        Volatile.Read(
                                            ref oneWayResetVersion))
                                {
                                    int queued =
                                        PCOMM.sio_iqueue(PortID);

                                    if (queued < 0)
                                    {
                                        throw new IOException(
                                            "Read input queue: " +
                                            PCOMM.GetErrorMessage(
                                                queued));
                                    }

                                    if (queued > 0)
                                    {
                                        int requested = Math.Min(
                                            queued, buffer.Length);

                                        count = PCOMM.sio_read(
                                            PortID, buffer, requested);

                                        if (count == PCOMM.SIO_NODATA)
                                            count = 0;

                                        if (count < 0 ||
                                            count > requested)
                                        {
                                            throw new IOException(
                                                "Read serial bytes failed " +
                                                "(code " + count + ").");
                                        }
                                    }
                                }
                            }

                            if (observedVersion !=
                                Volatile.Read(
                                    ref oneWayResetVersion))
                                continue;

                            if (count > 0)
                            {
                                receiver.Feed(buffer, count);
                                lastPortError = null;
                            }

                            receiver.CheckTimeout();

                            IsReceiving = receiver.IsReceiving;

                            cancellation.Token.WaitHandle.WaitOne(10);
                        }
                        catch (Exception e)
                        {
                            receiver.Reset(
                                "Serial input failed: " + e.Message);

                            IsReceiving = false;

                            if (lastPortError != e.Message)
                            {
                                AddLog(
                                    "Receive",
                                    "Serial input error: " + e.Message);

                                lastPortError = e.Message;
                            }

                            cancellation.Token.WaitHandle.WaitOne(200);
                        }
                    }
                }
                finally
                {
                    IsReceiving = false;
                    IsReceiveWaiting = false;

                    ReceiveCount = 0;
                    ReceiveMax = 0;

                    Notify(new
                    {
                        ReceiveCount,
                        ReceiveMax,
                        ReceiveProgress,
                        ReceivePercent,
                        ReceiveTimeText
                    });
                }
            }
        }
    }
}