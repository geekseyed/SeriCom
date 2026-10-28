using System;
using System.Configuration;

namespace ComTransfer
{
    public enum TransferEngineKind
    {
        Zmodem,
        OneWay
    }

    public static class TransferSettings
    {
        public static TransferEngineKind Engine { get; } = ReadEngine();

        private static TransferEngineKind ReadEngine()
        {
            string value =
                ConfigurationManager.AppSettings["transferengine"];

            // تنظیمات قدیمی، موتور فعلی را حفظ می‌کنند.
            if (string.IsNullOrWhiteSpace(value))
            {
                return TransferEngineKind.Zmodem;
            }

            if (string.Equals(
                value.Trim(),
                "Zmodem",
                StringComparison.OrdinalIgnoreCase))
            {
                return TransferEngineKind.Zmodem;
            }

            if (string.Equals(
                value.Trim(),
                "OneWay",
                StringComparison.OrdinalIgnoreCase))
            {
                return TransferEngineKind.OneWay;
            }

            throw new ConfigurationErrorsException(
                "Invalid transferengine. Use Zmodem or OneWay.");
        }
    }
}