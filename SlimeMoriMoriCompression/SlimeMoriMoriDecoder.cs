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
                    break;
                case 2:
                    SetupDisplacementTable(7);
                    DecodeByMode(output, uncompressedSize, 1, 3, 4, 3);
                    break;
                case 3:
                    SetupDisplacementTable(3);
                    DecodeByMode(output, uncompressedSize, 2, 2, 3, 2);
                    break;
                case 4:
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

        private void DecodeByMode(Stream output, int uncompressedSize, int bytesToRead, int dispBitCount, int lengthBitCount, int minLength)
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
                        // Add 2 bit values as long as read values' LSB is set
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

                            matchLength = (matchLength << lengthBitCount) | (_br.ReadBits<byte>(lengthBitCount) + bytesToRead);
                            // Goto 8098EA2
                        }
                        else
                        {
                            // 8098E88
                            matchLength++;
                            for (var i = 0; i < matchLength; i++)
                                for (var j = 0; j < bytesToRead; j++)
                                    output.WriteByte(_readValueFunc());

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
                else
                {
                    // 8098E14
                    for (var j = 0; j < bytesToRead; j++)
                        output.WriteByte(_readValueFunc());
                }
            }
        }

        private void SetupDisplacementTable(int displacementTableCount)
        {
            _displacementTable = new DisplacementElement[displacementTableCount];
            for (int i = 0; i < displacementTableCount; i++)
            {
                if (i == 0)
                    _displacementTable[0] = new DisplacementElement(_br.ReadBits<byte>(4), 1);
                else
                {
                    var newDisplacementStart = (1 << _displacementTable[i - 1].ReadBits) + _displacementTable[i - 1].DisplacementStart;
                    _displacementTable[i] = new DisplacementElement(_br.ReadBits<byte>(4), (short)newDisplacementStart);
                }
            }
        }

        private int GetDisplacement(int dispIndex)
        {
            return _br.ReadBits<int>(_displacementTable[dispIndex].ReadBits) +
                    _displacementTable[dispIndex].DisplacementStart;
        }
    }
}
