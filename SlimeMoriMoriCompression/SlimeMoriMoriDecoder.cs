using System;
using System.IO;
using Komponent.IO;

namespace SlimeMoriMoriCompression
{
    class SlimeMoriMoriDecoder
    {
        private BinaryReaderX _br;
        private byte _identByte;

        private DisplacementElement[] _displacementTable;

        private Func<byte> _readValueFunc;

        public void Decode(Stream input, Stream output)
        {
            _br = new BinaryReaderX(input);

            var uncompressedSize = _br.ReadInt32() >> 8;
            _identByte = _br.ReadBits<byte>(8);

            SetupHuffman();

            switch (_identByte & 0x7)
            {
                case 1:
                    SetupDisplacementTable(4);
                    DecodeMode1(output, uncompressedSize);
                    break;
                case 2: // LZ 1 byte
                    SetupDisplacementTable(7);
                    DecodeMode2Or3(output, uncompressedSize, 1, 3, 4, 3);
                    break;
                case 3: // LZ 2 byte
                    SetupDisplacementTable(3);
                    DecodeMode2Or3(output, uncompressedSize, 2, 2, 3, 2);
                    break;
                case 4: // No further compression
                    while (output.Length < uncompressedSize)
                        output.WriteByte(_readValueFunc());
                    break;
                default: // LZ+RLE
                    SetupDisplacementTable(2);
                    DecodeMode5(output, uncompressedSize);
                    break;
            }

            // Deobfuscation
            switch (_identByte >> 5)
            {
                case 1:
                    output.Position = 0;
                    DeobfuscateMode1(output);
                    break;
                case 2:
                    output.Position = 0;
                    DeobfuscateMode2(output);
                    break;
                case 3:
                    output.Position = 0;
                    DeobfuscateMode3(output);
                    break;
                case 4:
                    output.Position = 0;
                    DeobfuscateMode4(output);
                    break;
            }
        }

        private void SetupHuffman()
        {
            HuffmanTree tree;
            switch ((_identByte >> 3) & 0x3)
            {
                case 1:
                    tree = new HuffmanTree(4);
                    tree.Build(_br);
                    _readValueFunc = () =>
                      {
                          var nibble1 = tree.GetValue(_br) << 4;
                          var nibble2 = tree.GetValue(_br);
                          return (byte)(nibble1 | nibble2);
                      };
                    break;
                case 2:
                    tree = new HuffmanTree(8);
                    tree.Build(_br);
                    _readValueFunc = () => tree.GetValue(_br);
                    break;
                default:
                    _readValueFunc = () => _br.ReadBits<byte>(8);
                    break;
            }
        }

        private void DecodeMode1(Stream output, int uncompressedSize)
        {
            while (output.Length < uncompressedSize)
            {
                if (_br.ReadBit())
                {
                    var displacement = GetDisplacement(_br.ReadBits<byte>(2));
                    var matchLength = _br.ReadBits<byte>(4) + 3;

                    ReadDisplacement(output, displacement, matchLength, 1);
                }
                else
                {
                    output.WriteByte(_readValueFunc());
                }
            }
        }

        private void DecodeMode2Or3(Stream output, int uncompressedSize, int bytesToRead, int dispBitCount, int lengthBitCount, int minLength)
        {
            while (output.Length < uncompressedSize)
            {
                if (_br.ReadBit())
                {
                    var matchLength = 0;
                    int displacement;

                    var dispIndex = _br.ReadBits<byte>(dispBitCount);
                    if (dispIndex == (1 << dispBitCount) - 1)
                    {
                        // 8098E4C
                        byte readValue;
                        // Add lengthBitCount bit values as long as read values' LSB is set
                        // Seems to be a variable length value
                        do
                        {
                            readValue = _br.ReadBits<byte>(lengthBitCount);
                            matchLength = (matchLength << (lengthBitCount - 1)) | (readValue >> 1);
                        } while ((readValue & 0x1) == 1);

                        if (_br.ReadBit())
                        {
                            // 8098E64
                            dispIndex = _br.ReadBits<byte>(dispBitCount);
                            displacement = GetDisplacement(dispIndex) * bytesToRead;

                            matchLength = ((matchLength << lengthBitCount) | _br.ReadBits<byte>(lengthBitCount)) + minLength;
                            // Goto 8098EA2
                        }
                        else
                        {
                            // 8098E88
                            matchLength++;
                            ReadHuffmanValues(output, matchLength, bytesToRead);

                            continue;
                        }
                    }
                    else
                    {
                        // 8098E32
                        displacement = GetDisplacement(dispIndex) * bytesToRead;

                        matchLength = _br.ReadBits<byte>(lengthBitCount) + minLength;
                        // Goto 8098EA2
                    }

                    // Label 8098EA2
                    ReadDisplacement(output, displacement, matchLength, bytesToRead);
                }
                else
                {
                    // 8098E14
                    ReadHuffmanValues(output, 1, bytesToRead);
                }
            }
        }

        private void DecodeMode5(Stream output, int uncompressedSize)
        {
            while (output.Length < uncompressedSize)
            {
                int matchLength;
                int displacement;

                var value0 = _br.ReadBits<byte>(2);
                if (value0 < 2) // cmp value0, #2 -> bcc
                {
                    // CC4
                    displacement = GetDisplacement(value0);
                    matchLength = _br.ReadBits<byte>(6) + 3;

                    // Goto EC4
                }
                else
                {
                    if (value0 == 2)
                    {
                        // CDC
                        matchLength = _br.ReadBits<byte>(6) + 1;
                        ReadHuffmanValues(output, matchLength, 1);

                        continue;
                    }

                    // value0 == 3
                    // CA4
                    matchLength = _br.ReadBits<byte>(6) + 1;
                    displacement = 1;
                    output.WriteByte(_br.ReadBits<byte>(8));// read static 8 bit; we don't read a huffman value here

                    // Goto EC4
                }

                // EC4
                ReadDisplacement(output, displacement, matchLength, 1);
            }
        }

        private void ReadHuffmanValues(Stream output, int count, int bytesToRead)
        {
            for (var i = 0; i < count; i++)
                for (var j = 0; j < bytesToRead; j++)
                    output.WriteByte(_readValueFunc());
        }

        private void ReadDisplacement(Stream output, int displacement, int matchLength, int bytesToRead)
        {
            for (var i = 0; i < matchLength; i++)
            {
                var position = output.Position;
                for (var j = 0; j < bytesToRead; j++)
                {
                    output.Position = position - displacement;
                    var matchValue = (byte)output.ReadByte();

                    output.Position = position;
                    output.WriteByte(matchValue);

                    position++;
                }
            }
        }

        private void SetupDisplacementTable(int displacementTableCount)
        {
            _displacementTable = new DisplacementElement[displacementTableCount];
            for (var i = 0; i < displacementTableCount; i++)
            {
                if (i == 0)
                    _displacementTable[0] = new DisplacementElement((byte)(_br.ReadBits<int>(4) + 1), 1);
                else
                {
                    var newDisplacementStart = (1 << _displacementTable[i - 1].ReadBits) + _displacementTable[i - 1].DisplacementStart;
                    _displacementTable[i] = new DisplacementElement((byte)(_br.ReadBits<int>(4) + 1), (short)newDisplacementStart);
                }
            }
        }

        private int GetDisplacement(int dispIndex)
        {
            return _br.ReadBits<int>(_displacementTable[dispIndex].ReadBits) +
                    _displacementTable[dispIndex].DisplacementStart;
        }

        private void DeobfuscateMode1(Stream output)
        {
            var seed = 0;
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

        private void DeobfuscateMode2(Stream output)
        {
            var seed = 0;
            while (output.Position < output.Length)
            {
                var byte2 = output.ReadByte();
                var byte1 = output.ReadByte();

                var byte2New = byte2 + seed;
                var byte1New = seed = byte1 + byte2New;

                output.Position -= 2;
                output.WriteByte((byte)byte2New);
                output.WriteByte((byte)byte1New);
            }
        }

        private void DeobfuscateMode3(Stream output)
        {
            var seed = 0;
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

        private void DeobfuscateMode4(Stream output)
        {
            var seed = 0;
            var seed2 = 0;
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
    }
}
