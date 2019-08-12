using System;
using System.Collections.Generic;
using System.Text;

namespace SlimeMoriMoriCompression
{
    class DisplacementElement
    {
        public byte ReadBits { get; }
        public short DisplacementStart { get; }

        public DisplacementElement(byte readBits, short dispalcementStart)
        {
            ReadBits = readBits;
            DisplacementStart = DisplacementStart;
        }
    }
}
