using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Diagnostics;

namespace OneWireTempLib
{
    public class UART_Adapter : IDisposable
    {
        private SerialPort UART;

        public UART_Adapter(string portName)
        {
            UART = new SerialPort(portName)
            {
                DtrEnable = true
            };
        }

        public void Open()
        {
            if (!UART.IsOpen)
            {
                UART.DtrEnable = true;
                UART.Open();
            }
        }

        public void Close()
        {
            if (UART.IsOpen)
            {
                UART.DtrEnable = false;
                UART.Close();
            }
        }

        public bool IsOpened { get { return UART.IsOpen; } }

        public void Clear()
        {
            UART.DiscardInBuffer();
            UART.DiscardOutBuffer();
        }

        public byte ReadByte()
        {
            Clear();
            byte[] buffer = new byte[] { RomCommands.NoOp, RomCommands.NoOp, RomCommands.NoOp, RomCommands.NoOp, RomCommands.NoOp, RomCommands.NoOp, RomCommands.NoOp, RomCommands.NoOp };
            UART.Write(buffer, 0, buffer.Length);
            Array.Clear(buffer, 0, buffer.Length);
            Stopwatch stopWatch = Stopwatch.StartNew();
            do
            {

            } while (UART.BytesToRead != 8 || stopWatch.Elapsed.TotalMilliseconds < 100);
            if (UART.Read(buffer, 0, 8) != 8)
                throw new ReadError();
            byte value = 0;
            foreach (byte b in buffer.Reverse<byte>())
            {
                value <<= 1;
                if (b == 0xFF)
                {
                    value += 1;
                }
            }
            return value;
        }

        public byte[] ReadBytes(int size = 1)
        {
            byte[] data = new byte[size];
            for (int i = 0; i < size; i++)
            {
                data[i] = ReadByte();
            }
            return data;
        }

        public void WriteByte(byte data)
        {
            byte[] buffer = new byte[8];
            for (int i = 0; i < 8; i++)
            {
                buffer[i] = data % 2 != 0 ? (byte)0xFF : (byte)0x00;
                data >>= 1;
            }
            Clear();
            UART.Write(buffer, 0, buffer.Length);
            byte[] backBuffer = new byte[8];
            Stopwatch stopWatch = Stopwatch.StartNew();
            do
            {

            } while (UART.BytesToRead != 8 || stopWatch.Elapsed.TotalMilliseconds < 100);
            if (UART.Read(backBuffer, 0, 8) != 8)
                throw new WriteError();
            if (!buffer.SequenceEqual<byte>(backBuffer))
                throw new SystemException("Noise on the line detected");
        }

        public void WriteBytes(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                WriteByte(data[i]);
            }
        }

        public byte ReadBit()
        {
            Clear();
            byte[] buffer = new byte[] { RomCommands.NoOp };
            UART.Write(buffer, 0, buffer.Length);
            if (UART.Read(buffer, 0, 1) != 1)
                throw new ReadError();
            if (buffer[0] == RomCommands.NoOp)
            {
                return 0x1;
            }
            else
            {
                return 0x0;
            }
        }

        public void WriteBit(byte bit)
        {
            byte[] buffer = new byte[] { (bit == 0x1) ? (byte)0xFF : (byte)0x00 };
            Clear();
            UART.Write(buffer, 0, buffer.Length);
            byte[] backBuffer = new byte[1];
            Stopwatch stopWatch = Stopwatch.StartNew();
            do
            {

            } while (UART.BytesToRead != 1 || stopWatch.Elapsed.TotalMilliseconds < 100);
            if (UART.Read(backBuffer, 0, 1) != 1)
                throw new WriteError();
            if (!buffer.SequenceEqual<byte>(backBuffer))
                throw new SystemException("Noise on the line detected");
        }

        /// <summary>
        /// Reset pulse and presence pulse.
        /// </summary>
        /// <returns></returns>
        public bool Reset()
        {
            Clear();
            UART.BaudRate = 9600;
            byte[] buffer = new byte[] { RomCommands.SearchRom };
            UART.Write(buffer, 0, buffer.Length);
            Stopwatch stopWatch = Stopwatch.StartNew();
            do
            {

            } while (UART.BytesToRead != 1 || stopWatch.Elapsed.TotalMilliseconds < 100);
            if (UART.Read(buffer, 0, 1) != 1)
                throw new UARTError();
            UART.BaudRate = 115200;
            byte data = buffer[0];

            if (data == RomCommands.SearchRom)
            {
                throw new UARTError("No 1-wire devices");
            }
            else if ((0xE0 >= data) && (data >= 0x10))
            {
                return true;
            }
            else
            {
                throw new UARTError(String.Format("Device error: {0,10:X}", data));
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    UART.Close();
                    UART.Dispose();
                }

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~UART_Adapter()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class ReadError : SystemException
    {
        public ReadError() : base("Read error")
        { }

        public ReadError(string message) : base(message)
        { }
    }

    public class WriteError : SystemException
    {
        public WriteError() : base("Write error")
        { }

        public WriteError(string message) : base(message)
        { }
    }

    public class UARTError : SystemException
    {
        public UARTError() : base("Read or Write error")
        { }

        public UARTError(string message) : base(message)
        { }
    }
}
