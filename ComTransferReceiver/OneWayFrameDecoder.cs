using System;
using System.Collections.Generic;

namespace ComTransfer
{
    internal sealed class OneWayFrameDecoder
    {
        private readonly List<byte> buffer = new List<byte>();

        private readonly Action<
            OneWayFrameType, Guid, uint, byte[]> onFrame;

        private readonly Action<string> onError;
        private bool reportedNoise;

        internal OneWayFrameDecoder(
            Action<OneWayFrameType, Guid, uint, byte[]> onFrame,
            Action<string> onError)
        {
            if (onFrame == null)
                throw new ArgumentNullException(nameof(onFrame));

            if (onError == null)
                throw new ArgumentNullException(nameof(onError));

            this.onFrame = onFrame;
            this.onError = onError;
        }

        internal bool HasBufferedData => buffer.Count > 0;

        internal void Reset()
        {
            buffer.Clear();
            reportedNoise = false;
        }

        internal void Feed(byte[] data, int count)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (count < 0 || count > data.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            int offset = 0;

            while (offset < count)
            {
                int take = Math.Min(1024, count - offset);

                for (int i = 0; i < take; i++)
                    buffer.Add(data[offset + i]);

                offset += take;
                Process();
            }
        }

        private void Process()
        {
            while (buffer.Count >= OneWayProtocol.Magic.Length)
            {
                int start = FindMagic();

                if (start < 0)
                {
                    buffer.RemoveRange(0, buffer.Count - 7);
                    ReportNoise(
                        "Discarded bytes that do not match SCOW0001.");
                    return;
                }

                if (start > 0)
                {
                    buffer.RemoveRange(0, start);
                    ReportNoise(
                        "Skipped invalid bytes before a packet.");
                }

                if (buffer.Count < 33)
                    return;

                byte type = buffer[8];
                int length = ReadInt32(29);

                if (length < 0 ||
                    length > OneWayProtocol.MaxPayloadSize ||
                    type < 1 || type > 3)
                {
                    buffer.RemoveAt(0);
                    ReportNoise(
                        "Invalid packet header or payload length.");
                    continue;
                }

                int totalLength = 37 + length;

                if (buffer.Count < totalLength)
                    return;

                byte[] body =
                    buffer.GetRange(8, 25 + length).ToArray();

                uint receivedCrc =
                    unchecked((uint)ReadInt32(33 + length));

                if (receivedCrc !=
                    OneWayProtocol.CalculateCrc32(body))
                {
                    buffer.RemoveAt(0);
                    ReportNoise(
                        "Packet CRC32 mismatch; resend the whole file.");
                    continue;
                }

                Guid id =
                    new Guid(buffer.GetRange(9, 16).ToArray());

                uint sequence =
                    unchecked((uint)ReadInt32(25));

                byte[] payload =
                    buffer.GetRange(33, length).ToArray();

                buffer.RemoveRange(0, totalLength);
                reportedNoise = false;

                if (id == Guid.Empty)
                {
                    onError("Empty transfer ID.");
                    continue;
                }

                onFrame(
                    (OneWayFrameType)type,
                    id,
                    sequence,
                    payload);
            }
        }

        private void ReportNoise(string message)
        {
            if (reportedNoise)
                return;

            reportedNoise = true;
            onError(message);
        }

        private int ReadInt32(int offset)
        {
            return buffer[offset]
                | buffer[offset + 1] << 8
                | buffer[offset + 2] << 16
                | buffer[offset + 3] << 24;
        }

        private int FindMagic()
        {
            for (int i = 0;
                i <= buffer.Count - OneWayProtocol.Magic.Length;
                i++)
            {
                int matched = 0;

                while (matched < OneWayProtocol.Magic.Length &&
                    buffer[i + matched] ==
                    OneWayProtocol.Magic[matched])
                {
                    matched++;
                }

                if (matched == OneWayProtocol.Magic.Length)
                    return i;
            }

            return -1;
        }
    }
}