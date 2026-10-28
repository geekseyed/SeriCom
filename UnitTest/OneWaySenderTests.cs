using ComTransfer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ComTransfer.Tests
{
    [TestClass]
    public class OneWaySenderTests
    {
        [TestMethod]
        public void PacketMatchesReferenceBytes()
        {
            var id = new Guid(
                "00112233-4455-6677-8899-aabbccddeeff");

            byte[] frame = OneWayProtocol.CreateFrame(
                OneWayFrameType.Data,
                id,
                7,
                new byte[] { 0, 1, 2, 255 });

            // Expected bytes calculated independently.
            string expected =
                "53-43-4F-57-30-30-30-31-02-" +
                "33-22-11-00-55-44-77-66-" +
                "88-99-AA-BB-CC-DD-EE-FF-" +
                "07-00-00-00-04-00-00-00-" +
                "00-01-02-FF-F6-E1-1A-E9";

            Assert.AreEqual(
                expected,
                BitConverter.ToString(frame));
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(4096)]
        [DataRow(10000)]
        public void FileFramesReconstructOriginalFile(int length)
        {
            string folder = Path.Combine(
                Path.GetTempPath(),
                "SeriCom-Test-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(folder);

            try
            {
                string path = Path.Combine(folder, "sample.bin");

                byte[] original = new byte[length];

                for (int i = 0; i < original.Length; i++)
                {
                    original[i] = (byte)(i % 251);
                }

                File.WriteAllBytes(path, original);

                var frames = new List<byte[]>();

                long lastProcessed = -1;
                long lastTotal = -1;

                Guid id = OneWayFileSender.SendFile(
                    path,
                    frame => frames.Add(frame),
                    CancellationToken.None,
                    (processed, total) =>
                    {
                        Assert.IsTrue(processed >= lastProcessed);
                        Assert.IsTrue(processed <= total);

                        lastProcessed = processed;
                        lastTotal = total;
                    });

                int expectedChunks =
                    (length + OneWayProtocol.MaxPayloadSize - 1) /
                    OneWayProtocol.MaxPayloadSize;

                Assert.AreEqual(
                    expectedChunks + 2,
                    frames.Count);

                Assert.AreNotEqual(Guid.Empty, id);

                Assert.AreEqual((long)length, lastProcessed);
                Assert.AreEqual((long)length, lastTotal);

                byte[] start = ReadPayload(
                    frames[0],
                    OneWayFrameType.Start,
                    id,
                    0);

                using (var reader = new BinaryReader(
                    new MemoryStream(start)))
                {
                    int nameLength = reader.ReadInt32();

                    string name = Encoding.UTF8.GetString(
                        reader.ReadBytes(nameLength));

                    Assert.AreEqual("sample.bin", name);
                    Assert.AreEqual((long)length, reader.ReadInt64());

                    Assert.AreEqual(
                        OneWayProtocol.MaxPayloadSize,
                        reader.ReadInt32());

                    Assert.AreEqual(
                        reader.BaseStream.Length,
                        reader.BaseStream.Position);
                }

                using (var reconstructed = new MemoryStream())
                {
                    for (int i = 1; i <= expectedChunks; i++)
                    {
                        byte[] data = ReadPayload(
                            frames[i],
                            OneWayFrameType.Data,
                            id,
                            (uint)i);

                        Assert.IsTrue(data.Length > 0);

                        reconstructed.Write(data, 0, data.Length);
                    }

                    CollectionAssert.AreEqual(
                        original,
                        reconstructed.ToArray());
                }

                byte[] end = ReadPayload(
                    frames[frames.Count - 1],
                    OneWayFrameType.End,
                    id,
                    (uint)(expectedChunks + 1));

                using (var reader = new BinaryReader(
                    new MemoryStream(end)))
                using (var sha = SHA256.Create())
                {
                    Assert.AreEqual(
                        (long)length,
                        reader.ReadInt64());

                    Assert.AreEqual(
                        (uint)expectedChunks,
                        reader.ReadUInt32());

                    CollectionAssert.AreEqual(
                        sha.ComputeHash(original),
                        reader.ReadBytes(32));

                    Assert.AreEqual(
                        reader.BaseStream.Length,
                        reader.BaseStream.Position);
                }

                // Sending must preserve the source file.
                CollectionAssert.AreEqual(
                    original,
                    File.ReadAllBytes(path));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void CanceledTransferProducesNoFrames()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                int frameCount = 0;

                Assert.ThrowsException<OperationCanceledException>(
                    () =>
                    {
                        OneWayFileSender.SendFile(
                            "unused.bin",
                            frame => frameCount++,
                            cancellation.Token);
                    });

                Assert.AreEqual(0, frameCount);
            }
        }

        private static byte[] ReadPayload(
            byte[] frame,
            OneWayFrameType expectedType,
            Guid expectedId,
            uint expectedSequence)
        {
            Assert.IsTrue(frame.Length >= 37);

            using (var reader = new BinaryReader(
                new MemoryStream(frame)))
            {
                Assert.AreEqual(
                    "SCOW0001",
                    Encoding.ASCII.GetString(reader.ReadBytes(8)));

                Assert.AreEqual(
                    (byte)expectedType,
                    reader.ReadByte());

                Assert.AreEqual(
                    expectedId,
                    new Guid(reader.ReadBytes(16)));

                Assert.AreEqual(
                    expectedSequence,
                    reader.ReadUInt32());

                int length = reader.ReadInt32();

                Assert.IsTrue(length >= 0);
                Assert.IsTrue(
                    length <= OneWayProtocol.MaxPayloadSize);

                Assert.AreEqual(length + 37, frame.Length);

                byte[] payload = reader.ReadBytes(length);

                Assert.AreEqual(length, payload.Length);

                // CRC is checked against a fixed reference
                // in PacketMatchesReferenceBytes.
                reader.ReadUInt32();

                Assert.AreEqual(
                    reader.BaseStream.Length,
                    reader.BaseStream.Position);

                return payload;
            }
        }
    }
}