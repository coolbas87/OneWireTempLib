using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneWireTempLib
{
    public class OneWireDevice
    {
        protected Dictionary<byte, string> TermometerType = new Dictionary<byte, string>()
        {
            [0x01] = "DS2401 - Silicon Serial Number",
            [0x10] = "DS18S20 - High-Precision 1-Wire Digital Thermometer",
            [0x22] = "DS1822 - Econo 1-Wire Digital Thermometer",
            [0x28] = "DS18B20 - Programmable Resolution 1-Wire Digital Thermometer",
        };

        protected UART_Adapter uart;

        public OneWireDevice(UART_Adapter UARTAdapter)
        {
            uart = UARTAdapter;
        }

        public virtual string DeviceName(byte DeviceFamilyCode)
        {
            string name;
            if (TermometerType.TryGetValue(DeviceFamilyCode, out name))
            {
                return name;
            }
            else
            {
                throw new UARTError("Unknown device");
            }
        }

        /// <summary>
        /// READ ROM [33h]
        /// This command can only be used when there is one slave on the bus. 
        /// It allows the bus master to read the slave’s 64-bit ROM code without using the Search ROM procedure. 
        /// If this command is used when there is  more  than  one  slave  present  on the  bus,  
        /// a  data  collision  will  occur  when  all  the  slaves  attempt to respond at the same time. 
        /// </summary>
        public byte[] ReadROM()
        {
            uart.Reset();
            uart.WriteByte(RomCommands.ReadRom);
            byte[] romCode = uart.ReadBytes(8);
            //crc = crc8(rom_code[0:7])
            //if crc != iord(rom_code, 7):
            //    raise CRCError('read_ROM CRC error')
            return romCode;
        }

        /// <summary>
        /// MATCH ROM [55h]
        /// The match ROM command followed by a 64-bit ROM code sequence allows the bus master to address a 
        /// specific slave device on a multidrop or single-drop bus. 
        /// Only the slave that exactly matches the 64-bit ROM code sequence will respond to the function command 
        /// issued by the master; all other slaves on the bus will wait for a reset pulse
        /// </summary>
        public void MatchROM(byte[] romCode)
        {
            uart.Reset();
            uart.WriteByte(RomCommands.MatchRom);
            uart.WriteBytes(romCode);
        }

        /// <summary>
        /// SKIP ROM [CCh]
        /// The master can use this command to address all devices on the bus simultaneously without sending out any  
        /// ROM  code  information. For  example,  the  master  can  make  all  DS18B20s  on  the  bus  
        /// perform simultaneous temperature conversions by issuing a Skip ROM command followed by a 
        /// Convert T [44h] command. 
        /// </summary>
        public void SkipROM()
        {
            uart.Reset();
            uart.WriteByte(RomCommands.SkipRom);
        }

        private byte[] bitsToRom(byte[] bits)
        {
            if (bits.Length != 64)
                throw new ArgumentException("Bits array length must be 64");

            List<byte> bytes = new List<byte>();

            for (int i = 0; i < 64; i += 8)
            {
                byte value = 0;
                byte[] reversed = new byte[8];
                Array.Copy(bits, i, reversed, 0, 8);
                reversed = reversed.Reverse().ToArray();
                for (int b = 0; b < reversed.Length; b++)
                {
                    value <<= 1;
                    if (reversed[b] > 0)
                        value += 1;
                }
                bytes.Add(value);
            }

            return bytes.ToArray();
        }

        protected byte[] romToBits(byte[] bits)
        {
            if (bits.Length != 8)
                throw new ArgumentException("Bits array length must be 8");

            List<byte> bytes = new List<byte>();

            for (int i = 0; i < bits.Length; i++)
            {
                byte b = bits[i];

                for (int j = 0; j < 8; j++)
                {
                    bytes.Add((byte)(b % 2));
                    b >>= 1;
                }
            }

            return bytes.ToArray();
        }

        private void Search(List<byte[]> completeRoms, List<byte[]> partialRoms, byte[] curROM = null, bool alarm = false)
        {
            List<byte> currentROM = new List<byte>();

            if (curROM != null && curROM.Length != 0)
            {
                currentROM = curROM.ToList();
            }
            uart.Reset();
            uart.WriteByte(alarm ? RomCommands.AlarmSearch : RomCommands.SearchRom);
            foreach (byte bit in currentROM)
            {
                uart.ReadBit();
                uart.ReadBit();
                uart.WriteBit(bit);
            }

            int maxCount = 64 - currentROM.Count;

            for (int i = 0; i < maxCount; i++)
            {
                byte b1 = uart.ReadBit();
                byte b2 = uart.ReadBit();
                if (b1 != b2)
                {
                    currentROM.Add(b1);
                    uart.WriteBit(b1);
                }
                else if ((b1 == 0x0) && (b2 == 0x0))
                {
                    List<byte> ROM = new List<byte>(currentROM);
                    ROM.Add(0x1);
                    partialRoms.Add(ROM.ToArray());
                    currentROM.Add(0x0);
                    uart.WriteBit(0x0);
                }
                else
                {
                    if (alarm)
                        return;
                    else
                        throw new OneWireException("Search command got wrong bits (two sequential 0b1)");
                }
            }
            completeRoms.Add(bitsToRom(currentROM.ToArray()));
        }

        /// <summary>
        /// SEARCH ROM [F0h]
        /// When a system is initially powered up, the master must identify the ROM codes of all slave devices on
        /// the bus, which allows the master to determine the number of slaves and their device types.The master
        /// learns the ROM codes through a process of elimination that requires the master to perform a Search ROM
        /// cycle(i.e., Search ROM command followed by data exchange) as many times as necessary to identify all
        /// of the slave devices.If there is only one slave on the bus, the simpler Read ROM command (see below)
        /// can be used in place of the Search ROM process. For a detailed explanation of the Search ROM
        /// procedure, refer to the iButton® Book of Standards at www.maxim-ic.com/ibuttonbook.After every
        /// Search ROM cycle, the bus master must return to Step 1 (Initialization) in the transaction sequence.
        /// 
        /// ALARM SEARCH [ECh]
        /// The operation of this command is identical to the operation of the Search ROM command except that
        /// only slaves with a set alarm flag will respond. This command allows the master device to determine if
        /// any DS18B20s experienced an alarm condition during the most recent temperature conversion. After
        /// every Alarm Search cycle (i.e., Alarm Search command followed by data exchange), the bus master must
        /// return to Step 1 (Initialization) in the transaction sequence.See the Operation—Alarm Signaling section
        /// for an explanation of alarm flag operation.
        /// </summary>
        public List<byte[]> SearchROM(bool alarm = false)
        {
            List<byte[]> completeRoms = new List<byte[]>();
            List<byte[]> partialRoms = new List<byte[]>();
            completeRoms.Clear();
            partialRoms.Clear();
            Search(completeRoms, partialRoms, null, alarm);
            for (int i = 0; i < partialRoms.Count(); i++)
            {
                Search(completeRoms, partialRoms, partialRoms[i], alarm);
                partialRoms.RemoveAt(i);
            }
            return completeRoms;
        }

        public bool IsConnected(byte[] romCode)
        {
            uart.Reset();
            uart.WriteByte(RomCommands.SearchRom);
            foreach (byte bit in romCode)
            {
                byte b1 = uart.ReadBit();
                byte b2 = uart.ReadBit();
                if ((b1 == 0x1) && (b2 == 0x1))
                    return false;
                uart.WriteBit(bit);
            }
            return true;
        }

        public List<byte[]> GetConnectedROMs()
        {
            return SearchROM();
        }

        public List<byte[]> AlaramSearch()
        {
            return SearchROM(true);
        }
    }

    public class OneWireException : SystemException
    {
        public OneWireException() : base("One Wire error")
        { }

        public OneWireException(string message) : base(message)
        { }
    }

    public class AdapterError : SystemException
    {
        public AdapterError() : base("Adapter error")
        { }

        public AdapterError(string message) : base(message)
        { }
    }

    public class DeviceError : SystemException
    {
        public DeviceError() : base("Device error")
        { }

        public DeviceError(string message) : base(message)
        { }
    }

    public class CRCError : SystemException
    {
        public CRCError() : base("CRC error")
        { }

        public CRCError(string message) : base(message)
        { }
    }
}
