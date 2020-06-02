using System;
using System.Collections.Generic;
using System.Text;

namespace OneWireTempLib
{
    public static class Utils
    {
        public static OneWireSensor CreateSensor(byte sensorType, UART_Adapter UARTAdapter, byte[] rom = null)
        {
            switch (sensorType)
            {
                case 0x01:
                    return new OneWireSensor(UARTAdapter, rom);
                case 0x10:
                    return new OneWireSensor(UARTAdapter, rom);
                case 0x22:
                    return new DS1822(UARTAdapter, rom);
                case 0x28:
                    return new DS18B20(UARTAdapter, rom);
                default:
                    return new OneWireSensor(UARTAdapter, rom);
            }
        }
    }
}
