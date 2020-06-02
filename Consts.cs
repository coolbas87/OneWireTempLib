
namespace OneWireTempLib
{
    public static class RomCommands
    {
        public const byte SearchRom = 0xF0;
        public const byte ReadRom = 0x33;
        public const byte MatchRom = 0x55;
        public const byte SkipRom = 0xCC;
        public const byte AlarmSearch = 0xEC;
        public const byte ConvertTemp = 0x44;
        public const byte WriteScratchpad = 0x4E;
        public const byte ReadScratchPad = 0xBE;
        public const byte CopyScratchPad = 0x48;
        public const byte Recall = 0xB8;
        public const byte ReadPowerSupply = 0xB4;
        public const byte NoOp = 0xFF;
    }
}
