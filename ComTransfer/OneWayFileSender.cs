using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ComTransfer
{
    public static class OneWayFileSender
    {
        public static Guid SendFile(
            string filePath,
            Action<byte[]> transmit,
            CancellationToken cancellationToken,
            Action<long, long> reportProgress = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "File path is required.",
                    nameof(filePath));
            }

            if (transmit == null)
            {
                throw new ArgumentNullException(nameof(transmit));
            }

            cancellationToken.ThrowIfCancellationRequested();

            string fileName = Path.GetFileName(filePath);
            byte[] nameBytes = Encoding.UTF8.GetBytes(fileName);

            if (nameBytes.Length == 0 || nameBytes.Length > 1024)
            {
                throw new ArgumentException(
                    "File name must contain 1 to 1024 UTF-8 bytes.",
                    nameof(filePath));
            }

            using (var file = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            using (var sha256 = SHA256.Create())
            {
                long fileLength = file.Length;

                long chunkCount =
                    fileLength / OneWayProtocol.MaxPayloadSize;

                if (fileLength % OneWayProtocol.MaxPayloadSize != 0)
                {
                    chunkCount++;
                }

                if (chunkCount > uint.MaxValue - 1L)
                {
                    throw new IOException(
                        "File is too large for this protocol version.");
                }

                Guid transferId = Guid.NewGuid();
                uint sequence = 0;

                byte[] startPayload = CreateStartPayload(
                    nameBytes,
                    fileLength);

                transmit(OneWayProtocol.CreateFrame(
                    OneWayFrameType.Start,
                    transferId,
                    sequence,
                    startPayload));

                reportProgress?.Invoke(0, fileLength);

                byte[] buffer =
                    new byte[OneWayProtocol.MaxPayloadSize];

                long processedBytes = 0;

                while (processedBytes < fileLength)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int required = (int)Math.Min(
                        buffer.Length,
                        fileLength - processedBytes);

                    int filled = 0;

                    while (filled < required)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int read = file.Read(
                            buffer,
                            filled,
                            required - filled);

                        if (read == 0)
                        {
                            throw new EndOfStreamException(
                                "File ended before its expected length.");
                        }

                        filled += read;
                    }

                    sha256.TransformBlock(
                        buffer,
                        0,
                        filled,
                        buffer,
                        0);

                    byte[] payload = new byte[filled];

                    Buffer.BlockCopy(
                        buffer,
                        0,
                        payload,
                        0,
                        filled);

                    sequence++;

                    transmit(OneWayProtocol.CreateFrame(
                        OneWayFrameType.Data,
                        transferId,
                        sequence,
                        payload));

                    processedBytes += filled;

                    reportProgress?.Invoke(
                        processedBytes,
                        fileLength);
                }

                cancellationToken.ThrowIfCancellationRequested();

                sha256.TransformFinalBlock(new byte[0], 0, 0);

                byte[] endPayload = CreateEndPayload(
                    processedBytes,
                    sequence,
                    sha256.Hash);

                sequence++;

                transmit(OneWayProtocol.CreateFrame(
                    OneWayFrameType.End,
                    transferId,
                    sequence,
                    endPayload));

                return transferId;
            }
        }

        private static byte[] CreateStartPayload(
            byte[] nameBytes,
            long fileLength)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(
                    stream,
                    Encoding.UTF8,
                    true))
                {
                    writer.Write(nameBytes.Length);
                    writer.Write(nameBytes);
                    writer.Write(fileLength);
                    writer.Write(OneWayProtocol.MaxPayloadSize);
                    writer.Flush();
                }

                return stream.ToArray();
            }
        }

        private static byte[] CreateEndPayload(
            long fileLength,
            uint dataFrameCount,
            byte[] hash)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(
                    stream,
                    Encoding.UTF8,
                    true))
                {
                    writer.Write(fileLength);
                    writer.Write(dataFrameCount);
                    writer.Write(hash);
                    writer.Flush();
                }

                return stream.ToArray();
            }
        }
    }
}