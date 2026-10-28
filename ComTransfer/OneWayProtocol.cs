using System;
using System.IO;
using System.Text;

namespace ComTransfer
{
    public enum OneWayFrameType : byte
    {
        Start = 1,
        Data = 2,
        End = 3
    }

    /// <summary>
    /// Creates packets for the one-way transfer engine.
    /// This class does not open or write to the serial port.
    /// </summary>
    public static class OneWayProtocol
    {
        public const int MaxPayloadSize = 4096;

        private static readonly byte[] Magic =
            Encoding.ASCII.GetBytes("SCOW0001");

        public static byte[] CreateFrame(
            OneWayFrameType frameType,
            Guid transferId,
            uint sequence,
            byte[] payload)
        {
            if (frameType != OneWayFrameType.Start &&
                frameType != OneWayFrameType.Data &&
                frameType != OneWayFrameType.End)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameType));
            }

            if (transferId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Transfer ID must not be empty.",
                    nameof(transferId));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (payload.Length > MaxPayloadSize)
            {
                throw new ArgumentException(
                    "Payload exceeds the maximum packet size.",
                    nameof(payload));
            }

            byte[] body;

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(
                    stream,
                    Encoding.UTF8,
                    true))
                {
                    writer.Write((byte)frameType);
                    writer.Write(transferId.ToByteArray());
                    writer.Write(sequence);
                    writer.Write(payload.Length);
                    writer.Write(payload);
                    writer.Flush();
                }

                body = stream.ToArray();
            }

            uint checksum = CalculateCrc32(body);

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(
                    stream,
                    Encoding.UTF8,
                    true))
                {
                    writer.Write(Magic);
                    writer.Write(body);
                    writer.Write(checksum);
                    writer.Flush();
                }

                return stream.ToArray();
            }
        }

        private static uint CalculateCrc32(byte[] data)
        {
            const uint polynomial = 0xEDB88320u;

            uint crc = 0xFFFFFFFFu;

            foreach (byte value in data)
            {
                crc ^= value;

                for (int bit = 0; bit < 8; bit++)
                {
                    if ((crc & 1u) != 0)
                    {
                        crc = (crc >> 1) ^ polynomial;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }

            return ~crc;
        }
    }
}