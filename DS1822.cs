using System;
using System.Collections.Generic;
using System.Text;

namespace OneWireTempLib
{
    public class DS1822 : DS18B20
    {
        public override byte FamilyCode => 0x22;

        public DS1822(UART_Adapter UARTAdapter, byte[] rom = null) :
            base(UARTAdapter, rom)
        {

        }
    }
}
