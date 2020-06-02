using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace OneWireTempLib
{
    public class OneWireSensor : OneWireDevice
    {
        protected readonly int DefTempConversionTime = 750;
        protected readonly int DefWriteTime = 10;
        protected int TempConversionTime;
        protected int WriteTime;

        protected byte[] romCode;
        protected bool isParasiticPwr;
        protected bool isSingleMode;

        public string ROM
        {
            get
            {
                StringBuilder hex = new StringBuilder(romCode.Length * 2);
                foreach (byte b in romCode)
                    hex.AppendFormat("{0:x2} ", b);
                return hex.ToString().Trim().ToUpper();
            }
        }

        public long ROMInt
        {
            get
            {
                long result;

                byte[] reversed = new byte[romCode.Length];
                Array.Copy(romCode, 0, reversed, 0, romCode.Length);
                reversed = reversed.Reverse().ToArray();
                result = BitConverter.ToInt64(reversed, 0);
                return result;
            }
        }

        public virtual byte FamilyCode => 0x00;

        public OneWireSensor(UART_Adapter UARTAdapter, byte[] rom = null) :
            base(UARTAdapter)
        {
            TempConversionTime = DefTempConversionTime;
            WriteTime = DefWriteTime;

            if (rom == null)
            {
                isSingleMode = true;
                romCode = ReadROM();
            }
            else
            {
                isSingleMode = false;
                romCode = rom;
                if (!IsConnected(romToBits(romCode)))
                {
                    throw new DeviceError($"Device with ROM code {romCode} not found");
                }
                uart.Reset();
            }
            isParasiticPwr = IsPowerSupply();

            if (romCode[0] != FamilyCode)
                throw new DeviceError($"The device is not a {DeviceName(FamilyCode)}");
        }

        protected byte CRC8(byte[] bytes)
        {
            byte crc = 0x00;
            foreach (byte item in bytes)
            {
                byte byteItem = item;

                for (int i = 0; i < 8; i++)
                {
                    byte b = (byte)((crc ^ byteItem) & 0x01);
                    crc >>= 1;
                    if (b > 0)
                        crc ^= 0x8C;
                    byteItem >>= 1;
                }
            }

            return crc;
        }

        public virtual string Info()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append($"Device: {DeviceName(FamilyCode)} {Environment.NewLine}");
            stringBuilder.Append($"ROM Code: {ROM.ToString()} {Environment.NewLine}");
            stringBuilder.AppendFormat("Power Mode: {0}", isParasiticPwr ? "parasitic" : "external");
            stringBuilder.Append(Environment.NewLine);
            stringBuilder.AppendFormat("Connection Mode: {0}", isSingleMode ? "single-drop" : "multidrop");

            return stringBuilder.ToString();
        }

        public void SaveEeprom()
        {
            CopyScratchpad();
        }

        public void LoadEeprom()
        {
            Recall();
        }

        public float GetTemperature(int attempts = 3)
        {
            attempts = attempts > 1 ? attempts : 1;
            byte[] scratchpad = { };
            Reset();
            ConvertTemp();
            for (int i = 0; i < attempts; i++)
            {
                Reset();
                try
                {
                    scratchpad = ReadScratchpad();
                }
                catch (CRCError)
                {
                    if (i == attempts)
                        throw;
                }
            }

            return CalcTemperature(scratchpad);
        }

        /// <summary>
        /// CONVERT T [44h]
        /// This command initiates a single temperature conversion.Following the conversion, the resulting thermal
        /// data is stored in the 2-byte temperature register in the scratchpad memory and the DS18B20 returns to its
        /// low-power idle state.If the device is being used in parasite power mode, within 10µs(max) after this
        /// command is issued the master must enable a strong pullup on the 1-Wire bus for the duration of the
        /// conversion (tCONV) as described in the Powering the DS18B20 section.If the DS18B20 is powered by an
        /// external supply, the master can issue read time slots after the Convert T command and the DS18B20 will
        /// respond by transmitting a 0 while the temperature conversion is in progress and a 1 when the conversion
        /// is done.In parasite power mode this notification technique cannot be used since the bus is pulled high by
        /// the strong pullup during the conversion.
        /// </summary>
        public void ConvertTempAll()
        {
            SkipROM();
            uart.WriteByte(RomCommands.ConvertTemp);
            // We do not know what are the sensors on the line, so we will wait maximum time
            Thread.Sleep(DefTempConversionTime);
        }

        /// <summary>
        /// CONVERT T [44h]
        /// This command initiates a single temperature conversion.Following the conversion, the resulting thermal
        /// data is stored in the 2-byte temperature register in the scratchpad memory and the DS18B20 returns to its
        /// low-power idle state.If the device is being used in parasite power mode, within 10µs(max) after this
        /// command is issued the master must enable a strong pullup on the 1-Wire bus for the duration of the
        /// conversion (tCONV) as described in the Powering the DS18B20 section.If the DS18B20 is powered by an
        /// external supply, the master can issue read time slots after the Convert T command and the DS18B20 will
        /// respond by transmitting a 0 while the temperature conversion is in progress and a 1 when the conversion
        /// is done.In parasite power mode this notification technique cannot be used since the bus is pulled high by
        /// the strong pullup during the conversion.
        /// </summary>
        public void ConvertTemp()
        {
            uart.WriteByte(RomCommands.ConvertTemp);
            Wait(TempConversionTime);
        }

        /// <summary>
        /// READ SCRATCHPAD [BEh]
        /// This command allows the master to read the contents of the scratchpad.The data transfer starts with the
        /// least significant bit of byte 0 and continues through the scratchpad until the 9th byte (byte 8 – CRC) is
        /// read.The master may issue a reset to terminate reading at any time if only part of the scratchpad data is
        /// needed.
        /// </summary>
        public byte[] ReadScratchpad()
        {
            uart.WriteByte(RomCommands.ReadScratchPad);
            byte[] scratchpad = uart.ReadBytes(8);
            byte crc = uart.ReadByte();
            if (CRC8(scratchpad) != crc)
            {
                throw new CRCError("CRC error while reading scratchpad");
            }

            return scratchpad;
        }

        /// <summary>
        /// WRITE SCRATCHPAD [4Eh]
        /// This command allows the master to write 3 bytes of data to the DS18B20’s scratchpad.The first data byte
        /// is written into the TH register (byte 2 of the scratchpad), the second byte is written into the TL register
        /// (byte 3), and the third byte is written into the configuration register(byte 4). Data must be transmitted
        /// least significant bit first.All three bytes MUST be written before the master issues a reset, or the data
        /// may be corrupted.
        /// </summary>
        public void WriteScratchpad(byte[] scratchpad)
        {
            uart.WriteByte(RomCommands.WriteScratchpad);
            uart.WriteBytes(scratchpad);
        }

        /// <summary>
        /// COPY SCRATCHPAD [48h]
        /// This command copies the contents of the scratchpad TH, TL and configuration registers(bytes 2, 3 and 4)
        /// to EEPROM.If the device is being used in parasite power mode, within 10µs (max) after this command is
        /// issued the master must enable a strong pullup on the 1-Wire bus for at least 10ms as described in the
        /// Powering the DS18B20 section.
        /// </summary>
        public void CopyScratchpad()
        {
            uart.WriteByte(RomCommands.CopyScratchPad);
            Wait(WriteTime);
        }

        /// <summary>
        /// RECALL E [B8h]
        /// This command recalls the alarm trigger values(TH and TL) and configuration data from EEPROM and
        /// places the data in bytes 2, 3, and 4, respectively, in the scratchpad memory.The master device can issue
        /// read time slots following the Recall E2 command and the DS18B20 will indicate the status of the recall by
        /// transmitting 0 while the recall is in progress and 1 when the recall is done.The recall operation happens
        /// automatically at power-up, so valid data is available in the scratchpad as soon as power is applied to the
        /// device.
        /// </summary>
        public void Recall()
        {
            if (!isParasiticPwr)
            {
                uart.WriteByte(RomCommands.Recall);
                Wait();
            }
        }

        /// <summary>
        /// READ POWER SUPPLY [B4h]
        /// The master device issues this command followed by a read time slot to determine if any DS18B20s on the
        /// bus are using parasite power.During the read time slot, parasite powered DS18B20s will pull the bus
        /// low, and externally powered DS18B20s will let the bus remain high.See the Powering the DS18B20
        /// section for usage information for this command.
        /// </summary>
        public bool IsPowerSupply()
        {
            uart.WriteByte(RomCommands.ReadPowerSupply);
            return uart.ReadBit() == 0x0;
        }

        public void Reset()
        {
            if (isSingleMode)
                SkipROM();
            else
                MatchROM(romCode);
        }

        protected void Wait(int millisecs = 750)
        {
            if (isParasiticPwr)
                Thread.Sleep(millisecs);
            else
                while (uart.ReadBit() == 0x0)
                { }
        }

        protected virtual float CalcTemperature(byte[] scratchpad)
        {
            throw new NotImplementedException();
        }
    }
}
