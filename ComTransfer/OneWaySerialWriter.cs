using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ComTransfer
{
    public sealed class OneWaySerialWriter : IDisposable
    {
        private readonly int portId;
        private readonly int stallTimeoutMilliseconds;
        private readonly int previousWriteTimeout;

        private bool disposed;

        public OneWaySerialWriter(
            int portId,
            int stallTimeoutMilliseconds = 30000)
        {
            if (portId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(portId));
            }

            if (stallTimeoutMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stallTimeoutMilliseconds));
            }

            this.portId = portId;
            this.stallTimeoutMilliseconds =
                stallTimeoutMilliseconds;

            int previous = 0;

            int result = PCOMM.sio_GetWriteTimeouts(
                portId,
                ref previous);

            CheckResult(result, "Read write timeout");

            previousWriteTimeout = previous;

            // MAXDWORD: return without waiting indefinitely.
            result = PCOMM.sio_SetWriteTimeouts(portId, -1);

            CheckResult(result, "Set non-blocking write mode");
        }

        public void WriteFrame(
            byte[] frame,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            int offset = 0;
            var stalled = Stopwatch.StartNew();

            while (offset < frame.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int count = Math.Min(
                    1024,
                    frame.Length - offset);

                byte[] pending = new byte[count];

                Buffer.BlockCopy(
                    frame,
                    offset,
                    pending,
                    0,
                    count);

                int written = PCOMM.sio_write(
                    portId,
                    pending,
                    pending.Length);

                if (written > pending.Length)
                {
                    throw new IOException(
                        "PCOMM returned an invalid write length.");
                }

                if (written > 0)
                {
                    offset += written;
                    stalled.Restart();
                    continue;
                }

                // No buffer space: wait briefly and retry.
                if (written < 0 && written != PCOMM.SIO_NODATA)
                {
                    throw CreateError(
                        "Write serial data",
                        written);
                }

                WaitForProgress(stalled, cancellationToken);
            }
        }

        public void WaitForDrain(
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            var stalled = Stopwatch.StartNew();
            int previousPending = -1;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int pending = PCOMM.sio_oqueue(portId);

                if (pending < 0)
                {
                    throw CreateError(
                        "Read output queue",
                        pending);
                }

                if (pending == 0)
                {
                    return;
                }

                if (previousPending < 0 ||
                    pending < previousPending)
                {
                    stalled.Restart();
                }

                previousPending = pending;

                WaitForProgress(stalled, cancellationToken);
            }
        }

        private void WaitForProgress(
            Stopwatch stalled,
            CancellationToken cancellationToken)
        {
            if (stalled.ElapsedMilliseconds >=
                stallTimeoutMilliseconds)
            {
                throw new TimeoutException(
                    "Serial output made no progress within " +
                    stallTimeoutMilliseconds +
                    " milliseconds.");
            }

            if (cancellationToken.WaitHandle.WaitOne(10))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    nameof(OneWaySerialWriter));
            }
        }

        private static void CheckResult(
            int result,
            string operation)
        {
            if (result != PCOMM.SIO_OK)
            {
                throw CreateError(operation, result);
            }
        }

        private static IOException CreateError(
            string operation,
            int errorCode)
        {
            return new IOException(
                operation + " failed: " +
                PCOMM.GetErrorMessage(errorCode) +
                " (code " + errorCode + ").");
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            int result = PCOMM.sio_SetWriteTimeouts(
                portId,
                previousWriteTimeout);

            CheckResult(result, "Restore write timeout");
        }
    }
}