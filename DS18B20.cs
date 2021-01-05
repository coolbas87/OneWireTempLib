using System;
using System.Collections.Generic;
using System.Text;

namespace OneWireTempLib
{
    public class DS18B20 : OneWireSensor
    {
        public static byte Resolution9Bit = 0x0;
        public static byte Resolution10Bit = 0x1;
        public static byte Resolution11Bit = 0x2;
        public static byte Resolution12Bit = 0x3;

        public override byte FamilyCode => 0x28;

        public DS18B20(UART_Adapter UARTAdapter, byte[] rom = null) :
            base(UARTAdapter, rom)
        {
            SetTempConv(GetResolution());
        }

        public override string Info()
        {
            StringBuilder stringBuilder = new StringBuilder(base.Info());
            Reset();
            byte[] scratchpad = ReadScratchpad();
            stringBuilder.AppendFormat("Alarms: high = {0:D} C, low = {1:D} C", (sbyte)scratchpad[2], (sbyte)scratchpad[3]);
            stringBuilder.Append(Environment.NewLine);
            stringBuilder.AppendFormat("Resolution: {0} bits", ((scratchpad[4] >> 5) & 0x3) + 9);

            return stringBuilder.ToString();
        }

        protected override float CalcTemperature(byte[] scratchpad)
        {
            byte resolution = (byte)((scratchpad[4] >> 5) & 0x3);
            int rawRes = scratchpad[0] | (scratchpad[1] << 8);
            float temp = 1;
            if ((rawRes & 0x8000) > 0)
            {
                rawRes = (rawRes ^ 0xffff) + 1;
                temp = -1;
            }
            if (resolution == Resolution12Bit)
                temp = temp * (rawRes / 16.0f);
            else if (resolution == Resolution11Bit)
                temp = temp * ((rawRes >> 1) / 8.0f);
            else if (resolution == Resolution10Bit)
                temp = temp * ((rawRes >> 2) / 4.0f);
            else if (resolution == Resolution9Bit)
                temp = temp * ((rawRes >> 3) / 2.0f);
            else
                throw new NotImplementedException();

            return temp;
        }

        public byte[] GetHighLowTemps()
        {
            Reset();
            byte[] scratchpad = ReadScratchpad();

            return new byte[] { scratchpad[2], scratchpad[3] };
        }

        public void SetHighLowTemps(int high = 125, int low = -55)
        {
            Reset();
            byte[] scratchpad = ReadScratchpad();
            byte[] buffer = new byte[] { (byte)high, (byte)low, scratchpad[4] };
            Reset();
            WriteScratchpad(buffer);
        }

        public byte GetResolution()
        {
            Reset();
            byte[] scratchpad = ReadScratchpad();

            return (byte)((scratchpad[4] >> 5) & 0x3);
        }

        public void SetResolution(byte resolution)
        {
            byte res = (byte)(resolution & 0x3);
            Reset();
            byte[] scratchpad = ReadScratchpad();
            byte[] buffer = new byte[] { scratchpad[2], scratchpad[3], (byte)((resolution << 5) | 0x1F) };
            Reset();
            WriteScratchpad(buffer);
            SetTempConv(resolution);
        }

        public void SetTempConv(byte resolution)
        {
            TempConversionTime = DefTempConversionTime / (8 >> resolution);
        }
    }
}
