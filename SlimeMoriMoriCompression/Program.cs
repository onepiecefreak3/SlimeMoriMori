using System;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using Komponent.IO;

namespace SlimeMoriMoriCompression
{
    class Program
    {
        private static short _unkValue1;
        private static short _unkValue2;
        private static short _unkValue3;
        private static short _unkValue4;
        private static short _unkValue5;
        private static short _unkValue6;
        private static short _unkValue7;
        private static byte _code1;
        private static byte _code2;
        private static byte _code3;
        private static byte _code4;
        private static byte _code5;
        private static byte _code6;
        private static byte _code7;

        private static MemoryStream _output;

        static void Main(string[] args)
        {
            _output = new MemoryStream();
            var file = File.Open(@"D:\Users\Kirito\Desktop\compressedBlob1.bin.decomp.comp", FileMode.Open);

            //var decoder = new SlimeMoriMoriDecoder();
            //decoder.Decode(file, _output);

            //var outuptFile2 = File.Create(@"D:\Users\Kirito\Desktop\compressedBlob3.bin.decomp");
            //_output.Position = 0;
            //_output.CopyTo(outuptFile2);
            //outuptFile2.Close();

            //return;

            using (var br = new BinaryReaderX(file))
            {
                var magicValue = br.ReadInt32();
                var significant = br.ReadBits<byte>(8);

                // Bit 3 and 4 declare if and how the huffman tree table is initialized
                byte[] table = null;
                Func<BinaryReaderX, byte> readValueFunc;
                var what = (significant >> 3) & 0x3;
                switch (what)
                {
                    case 1:
                        // Goto 8098B00
                        // store 8098C03 in r5
                        // store 4 in r4
                        // Goto 8098B0A
                        // Goto 8098B84
                        table = LoadTable(br, 4);  // Init table of size 16 * 4
                        readValueFunc = internalBr =>
                        {
                            var nibble1 = Fun_8098C1A(internalBr, table) << 4;
                            var nibble2 = Fun_8098C1A(internalBr, table);
                            return (byte)(nibble1 | nibble2);
                        };
                        // Goto 8098B0E
                        break;
                    case 2:
                        // Goto 8098B06
                        // store 8098C13 in r5  // Method for reading value from tree
                        // store 8 in r4
                        // Goto 8098B84
                        table = LoadTable(br, 8);  // Init table of size 256 * 4
                        readValueFunc = internalBr => Fun_8098C1A(internalBr, table);
                        // Goto 8098B0E
                        break;
                    default:
                        // store 8098BFF in r5
                        // r4 should be 0 (or 1) here
                        readValueFunc = internalBr => internalBr.ReadBits<byte>(8);
                        // Goto 8098B0E
                        break;
                }

                // Label 8098B0E
                // store r5 in r9; That effectively works as a delegate now, which describes how to read a value later on
                ReadData(br, table, significant, magicValue >> 8, readValueFunc);
            }

            var outuptFile = File.Create(@"D:\Users\Kirito\Desktop\compressedBlob1.bin.decomp.comp.decomp");
            _output.Position = 0;
            _output.CopyTo(outuptFile);
            outuptFile.Close();
        }

        static byte[] LoadTable(BinaryReaderX br, int huffmanBits)
        {
            // Label 8098B84

            var table = new byte[(1 << huffmanBits) * 4];
            for (var i = 0; i < (1 << huffmanBits) * 4; i++)
                table[i] = 0xFF;

            var tableIndex2 = 0;
            var counter3 = 0;
            var counter2 = 0;
            var counter = 16;
            do
            {
                counter3 <<= 1;
                var value = br.ReadBits<byte>(8);
                var valueBk = value;

                if (value != 0)
                {
                    do
                    {
                        var tableIndex = 0;
                        var internalCounter = counter2;

                        while (internalCounter != 0)
                        {
                            var newTableIndex = ((counter3 >> internalCounter) & 0x1) * 2 + tableIndex;
                            tableIndex = (short)(table[newTableIndex] | (table[newTableIndex + 1] << 8));
                            if (tableIndex < 0)
                            {
                                tableIndex = tableIndex2 + 4;

                                // Reference to another node
                                table[newTableIndex] = (byte)tableIndex;
                                table[newTableIndex + 1] = (byte)(tableIndex >> 8);
                                tableIndex2 = tableIndex;
                            }

                            internalCounter--;
                        }

                        value = br.ReadBits<byte>(huffmanBits);

                        // Value in tree
                        table[(counter3 & 0x1) * 2 + tableIndex] = (byte)~value;
                        table[(counter3 & 0x1) * 2 + tableIndex + 1] = (byte)((~value) >> 8);

                        counter3++;
                        valueBk--;
                    } while (valueBk != 0);
                }

                counter2++;
                counter--;
            } while (counter != 0);

            return table;
        }

        static void ReadData(BinaryReaderX br, byte[] table, byte significant, int uncompressedLength, Func<BinaryReaderX, byte> readValueFunc)
        {
            var type = significant & 0x7;
            switch (type)
            {
                case 1:
                    // 8098B2C
                    // -> 8098D02
                    Fun_8098D02(br, uncompressedLength, readValueFunc);
                    break;
                case 2:
                    // 8098B32
                    // -> 8098D54
                    Fun_8098D54(br, uncompressedLength, readValueFunc);
                    break;
                case 3:
                    // 8098B38
                    // -> 8098E04
                    Fun_8098E04(br, uncompressedLength, readValueFunc);
                    break;
                case 4:
                    // 8098B3E
                    // -> 8098C6C
                    Fun_8098C6C(br, uncompressedLength, readValueFunc);
                    break;
                default:
                    // 8098C8C
                    Fun_8098C8C(br, uncompressedLength, readValueFunc);
                    break;
            }

            // Label 8098B42
            var upper3 = significant >> 5;
            switch (upper3)
            {
                case 1:
                    // B5C
                    // -> F80
                    br.BaseStream.Position = 0;
                    Fun_08098F80(br.BaseStream, 0);
                    break;
                case 2:
                    // B62
                    // -> FB0
                    br.BaseStream.Position = 0;
                    Fun_08098FB0(br.BaseStream, 0);
                    break;
                case 3:
                    // B68
                    // -> FCC
                    br.BaseStream.Position = 0;
                    Fun_08098FCC(br.BaseStream, 0);
                    break;
                case 4:
                    // B6E
                    // -> FDC
                    br.BaseStream.Position = 0;
                    Fun_08098FDC(br.BaseStream, 0, 0);
                    break;
            }
        }

        // Deobfuscate mode 4
        static void Fun_08098FDC(Stream output, int seed, int seed2)
        {
            while (output.Position < output.Length)
            {
                var byte2 = output.ReadByte();
                var byte1 = output.ReadByte();

                var byte2New = seed = (byte2 + seed) & 0xFF;
                var byte1New = seed2 = (byte1 + seed2) & 0xFF;

                output.Position -= 2;
                output.WriteByte((byte)byte2New);
                output.WriteByte((byte)byte1New);
            }
        }

        // Deobfuscate mode 3
        static void Fun_08098FCC(Stream output, int seed)
        {
            while (output.Position < output.Length)
            {
                var short1 = (output.ReadByte() << 8) | output.ReadByte();

                var short1New = short1 + seed;
                seed = short1;

                output.Position -= 2;
                output.WriteByte((byte)(short1New >> 8));
                output.WriteByte((byte)short1New);
            }
        }

        // Deobfuscate mode 2
        static void Fun_08098FB0(Stream output, int seed)
        {
            while (output.Position < output.Length)
            {
                var byte2 = output.ReadByte();
                var byte1 = output.ReadByte();

                var byte1New = byte2 + seed;
                var byte2New = seed = byte1 + byte1New;

                output.Position -= 2;
                output.WriteByte((byte)byte1New);
                output.WriteByte((byte)byte2New);
            }
        }

        // Deobfuscate mode 1
        static void Fun_08098F80(Stream output, int seed)
        {
            while (output.Position < output.Length)
            {
                var byte2 = output.ReadByte();
                var byte1 = output.ReadByte();

                var nibble2 = (seed + (byte2 >> 4)) & 0xF;
                var nibble1 = (nibble2 + (byte2 & 0xF)) & 0xF;
                var byte2New = (nibble2 << 4) | nibble1;

                var nibble4 = (nibble1 + (byte1 >> 4)) & 0xF;
                var nibble3 = seed = (nibble4 + (byte1 & 0xF)) & 0xF;
                var byte1New = (nibble4 << 4) | nibble3;

                output.Position -= 2;
                output.WriteByte((byte)byte2New);
                output.WriteByte((byte)byte1New);
            }
        }

        static void ObfuscateMode4(Stream output, int seed, int seed2)
        {
            while (output.Position < output.Length)
            {
                var byte2 = output.ReadByte();
                var byte1 = output.ReadByte();

                var byte1New = byte1 - seed;
                var byte2New = byte2 - seed2;
                seed = byte1;
                seed2 = byte2;

                output.Position -= 2;
                output.WriteByte((byte)byte2New);
                output.WriteByte((byte)byte1New);
            }
        }

        static void ObfuscateMode3(Stream output, int seed)
        {
            while (output.Position < output.Length)
            {
                var short1 = (output.ReadByte() << 8) | output.ReadByte();

                var short1New = short1 - seed;
                seed = short1;

                output.Position -= 2;
                output.WriteByte((byte)(short1New >> 8));
                output.WriteByte((byte)short1New);
            }
        }

        static void ObfuscateMode2(Stream output, int seed)
        {
            while (output.Position < output.Length)
            {
                var byte2 = output.ReadByte();
                var byte1 = output.ReadByte();

                var byte1New = byte1 - byte2;
                var byte2New = byte2 - seed;
                seed = byte1;

                output.Position -= 2;
                output.WriteByte((byte)byte2New);
                output.WriteByte((byte)byte1New);
            }
        }

        static void ObfuscateMode1(Stream output, int seed)
        {
            while (output.Position < output.Length)
            {
                var byte2 = output.ReadByte();
                var byte1 = output.ReadByte();

                var nibble4 = ((byte1 >> 4) - (byte2 & 0xF)) & 0xF;
                var nibble3 = ((byte1 & 0xF) - (byte1 >> 4)) & 0xF;
                var byte2New = (nibble4 << 4) | nibble3;

                var nibble2 = ((byte2 >> 4) - seed) & 0xF;
                var nibble1 = ((byte2 & 0xF) - (byte2 >> 4)) & 0xF;
                var byte1New = (nibble2 << 4) | nibble1;

                seed = byte1 & 0xF;

                output.Position -= 2;
                output.WriteByte((byte)byte2New);
                output.WriteByte((byte)byte1New);
            }
        }

        static void Fun_8098D02(BinaryReaderX br, int uncompressedLength, Func<BinaryReaderX, byte> readValueFunc)
        {
            // Read 4 codes
            _unkValue1 = 1;
            _code1 = (byte)(br.ReadBits<byte>(4) + 1);
            _unkValue2 = (short)((1 << _code1) + _unkValue1);
            _code2 = (byte)(br.ReadBits<byte>(4) + 1);
            _unkValue3 = (short)((1 << _code2) + _unkValue2);
            _code3 = (byte)(br.ReadBits<byte>(4) + 1);
            _unkValue4 = (short)((1 << _code3) + _unkValue3);
            _code4 = (byte)(br.ReadBits<byte>(4) + 1);

            while (_output.Length < uncompressedLength)
            {
                if (br.ReadBit())
                {
                    // D16
                    var displacement = GetDisplacement(br, br.ReadBits<byte>(2));
                    var matchLength = br.ReadBits<byte>(4) + 3;
                    // Goto EC4

                    // EC4
                    for (var i = 0; i < matchLength; i++)
                    {
                        var position = _output.Position;

                        _output.Position = position - displacement;
                        var matchValue = (byte)_output.ReadByte();

                        _output.Position = position;
                        _output.WriteByte(matchValue);
                    }
                }
                else
                {
                    // D34
                    _output.WriteByte(readValueFunc(br));
                }
            }
        }

        static void Fun_8098C8C(BinaryReaderX br, int uncompressedLength, Func<BinaryReaderX, byte> readValueFunc)
        {
            // Read 2 codes
            _unkValue1 = 1;
            _code1 = (byte)(br.ReadBits<byte>(4) + 1);
            _unkValue2 = (short)((1 << _code1) + _unkValue1);
            _code2 = (byte)(br.ReadBits<byte>(4) + 1);

            while (_output.Length < uncompressedLength)
            {
                var matchLength = 0;
                var displacement = 0;

                var value0 = br.ReadBits<byte>(2);
                if (value0 < 2) // cmp value0, #2 -> bcc
                {
                    // CC4
                    displacement = GetDisplacement(br, value0);
                    matchLength = br.ReadBits<byte>(6) + 3;

                    // Goto EC4
                }
                else
                {
                    if (value0 == 2)
                    {
                        // CDC
                        matchLength = br.ReadBits<byte>(6) + 1;
                        for (var i = 0; i < matchLength; i++)
                            _output.WriteByte(readValueFunc(br));

                        continue;
                    }
                    else  // value0 == 3
                    {
                        // CA4
                        matchLength = br.ReadBits<byte>(6) + 1;
                        displacement = 1;
                        _output.WriteByte(br.ReadBits<byte>(8));// read static 8 bit; we don't read a huffman value here

                        // Goto EC4
                    }
                }

                // EC4
                for (var i = 0; i < matchLength; i++)
                {
                    var position = _output.Position;

                    _output.Position = position - displacement;
                    var matchValue = (byte)_output.ReadByte();

                    _output.Position = position;
                    _output.WriteByte(matchValue);
                }
            }
        }

        static void Fun_8098C6C(BinaryReaderX br, int uncompressedLength, Func<BinaryReaderX, byte> readValueFunc)
        {
            // Read values n times
            while (_output.Length < uncompressedLength)
                _output.WriteByte(readValueFunc(br));
        }

        static void Fun_8098D54(BinaryReaderX br, int uncompressedLength, Func<BinaryReaderX, byte> readValueFunc)
        {
            // Read 7 codes
            _unkValue1 = 1;
            _code1 = (byte)(br.ReadBits<byte>(4) + 1);
            _unkValue2 = (short)((1 << _code1) + _unkValue1);
            _code2 = (byte)(br.ReadBits<byte>(4) + 1);
            _unkValue3 = (short)((1 << _code2) + _unkValue2);
            _code3 = (byte)(br.ReadBits<byte>(4) + 1);
            _unkValue4 = (short)((1 << _code3) + _unkValue3);
            _code4 = (byte)(br.ReadBits<byte>(4) + 1);
            _unkValue5 = (short)((1 << _code4) + _unkValue4);
            _code5 = (byte)(br.ReadBits<byte>(4) + 1);
            _unkValue6 = (short)((1 << _code5) + _unkValue5);
            _code6 = (byte)(br.ReadBits<byte>(4) + 1);
            _unkValue7 = (short)((1 << _code6) + _unkValue6);
            _code7 = (byte)(br.ReadBits<byte>(4) + 1);

            while (_output.Length < uncompressedLength)
            {
                // 8098D60
                if (br.ReadBit())
                {
                    var matchLength = 0;
                    int displacement;

                    // 8098D68
                    var value0 = br.ReadBits<byte>(3);
                    if (value0 == 7)
                    {
                        // 8098D8A
                        // Add 3 bit values as long as read values' LSB is set
                        // Seems to be a variable length value
                        byte readValue;
                        do
                        {
                            readValue = br.ReadBits<byte>(4);
                            matchLength = (matchLength << 3) | (readValue >> 1);
                        } while ((readValue & 0x1) == 1);

                        if (br.ReadBit())
                        {
                            // 8098DA2
                            displacement = GetDisplacement(br, br.ReadBits<byte>(3));

                            matchLength = ((matchLength << 4) | br.ReadBits<byte>(4)) + 3;
                            // Goto 8098EC4
                        }
                        else
                        {
                            // 8098DC4
                            matchLength += 1;
                            for (var i = 0; i < matchLength; i++)
                            {
                                // Read 1 byte
                                _output.WriteByte(readValueFunc(br));
                            }

                            continue;
                        }
                    }
                    else
                    {
                        // 8098D72
                        displacement = GetDisplacement(br, value0);

                        matchLength = br.ReadBits<byte>(4) + 3;
                        // Goto 8098EC4
                    }

                    // Label 8098EC4
                    for (var i = 0; i < matchLength; i++)
                    {
                        var position = _output.Position;

                        _output.Position = position - displacement;
                        var matchValue = (byte)_output.ReadByte();

                        _output.Position = position;
                        _output.WriteByte(matchValue);
                    }
                }
                else
                {
                    // 8098DDE
                    _output.WriteByte(readValueFunc(br));
                }
            }
        }

        //static void Fun_8098D02(BinaryReaderX br, byte[] table)
        //{
        //    // Read 4 codes
        //    _unkValue1 = 1;
        //    _code1 = br.ReadBits<byte>(4) + 1;
        //    _unkValue2 = (1 << _code1) + _unkValue1;
        //    _code2 = br.ReadBits<byte>(4) + 1;
        //    _unkValue3 = (1 << _code2) + _unkValue2;
        //    _code3 = br.ReadBits<byte>(4) + 1;
        //    _unkValue4 = 1 << _code3 + _unkValue3;
        //    _code4 = br.ReadBits<byte>(4) + 1;

        //    if (br.ReadBit())
        //    {
        //        // 8098D16
        //        var value0 = br.ReadBits<byte>(2);
        //        var displacement=GetDisplacement(br, value0);
        //        var value1 = br.ReadBits<byte>(4)+3;

        //        // 8098EC4
        //        while (displacement != 1)
        //        {

        //        }
        //    }
        //    else
        //    {

        //    }
        //}

        static void Fun_8098E04(BinaryReaderX br, int uncompressedLength, Func<BinaryReaderX, byte> readValueFunc)
        {
            // Read 3 codes
            _unkValue1 = 1;
            _code1 = (byte)(br.ReadBits<byte>(4) + 1);
            _unkValue2 = (short)((1 << _code1) + _unkValue1);
            _code2 = (byte)(br.ReadBits<byte>(4) + 1);
            _unkValue3 = (short)((1 << _code2) + _unkValue2);
            _code3 = (byte)(br.ReadBits<byte>(4) + 1);

            while (_output.Length < uncompressedLength)
            {
                if (br.ReadBit())
                {
                    var matchLength = 0;
                    int displacement;

                    var value0 = br.ReadBits<byte>(2);
                    if (value0 == 3)
                    {
                        // 8098E4C
                        byte readValue;
                        // Add 2 bit values as long as read values' LSB is set
                        // Seems to be a variable length value
                        do
                        {
                            readValue = br.ReadBits<byte>(3);
                            matchLength = (matchLength << 2) | (readValue >> 1);
                        } while ((readValue & 0x1) == 1);

                        if (br.ReadBit())
                        {
                            // 8098E64
                            displacement = GetDisplacement(br, br.ReadBits<byte>(2)) << 1;

                            matchLength = ((matchLength << 3) | br.ReadBits<byte>(3)) + 2;
                            // Goto 8098EA2
                        }
                        else
                        {
                            // 8098E88
                            matchLength += 1;
                            for (var i = 0; i < matchLength; i++)
                            {
                                // Read 2 bytes
                                _output.WriteByte(readValueFunc(br));
                                _output.WriteByte(readValueFunc(br));
                            }

                            continue;
                        }
                    }
                    else
                    {
                        // 8098E32
                        displacement = GetDisplacement(br, value0) << 1;

                        matchLength = br.ReadBits<byte>(3) + 2;
                        // Goto 8098EA2
                    }

                    // Label 8098EA2
                    for (var i = 0; i < matchLength; i++)
                    {
                        var position = _output.Position;

                        _output.Position = position - displacement;
                        var matchValue = (byte)_output.ReadByte();
                        var matchValue2 = (byte)_output.ReadByte();

                        _output.Position = position;
                        _output.WriteByte(matchValue);
                        _output.WriteByte(matchValue2);
                    }
                }
                else
                {
                    // 8098E14
                    // Read 2 bytes
                    _output.WriteByte(readValueFunc(br));
                    _output.WriteByte(readValueFunc(br));
                }
            }

            // 8098EB4
        }

        /// <summary>
        /// Gets value from tree.
        /// </summary>
        /// <param name="br">Reader for file.</param>
        /// <param name="table">Table to step through.</param>
        /// <returns>Value from tree.</returns>
        static byte Fun_8098C1A(BinaryReaderX br, byte[] table)
        {
            short tableValue = 0;

            do
            {
                var tableIndex = (br.ReadBits<byte>(1) << 1) + tableValue;
                tableValue = (short)(table[tableIndex] | (table[tableIndex + 1] << 8));
            } while (tableValue >= 0);

            return (byte)~tableValue;
        }

        static int GetDisplacement(BinaryReaderX br, byte type)
        {
            var global = -1;
            switch (type)
            {
                case 0:
                    global = br.ReadBits<int>(_code1) + _unkValue1;
                    break;
                case 1:
                    global = br.ReadBits<int>(_code2) + _unkValue2;
                    break;
                case 2:
                    global = br.ReadBits<int>(_code3) + _unkValue3;
                    break;
                case 3:
                    global = br.ReadBits<int>(_code4) + _unkValue4;
                    break;
                case 4:
                    global = br.ReadBits<int>(_code5) + _unkValue5;
                    break;
                case 5:
                    global = br.ReadBits<int>(_code6) + _unkValue6;
                    break;
                case 6:
                    global = br.ReadBits<int>(_code7) + _unkValue7;
                    break;
            }

            return global;
        }
    }
}
