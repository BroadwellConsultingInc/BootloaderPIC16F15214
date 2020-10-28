using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;

namespace IntelHex
{
    class HexData
    {
        public Hashtable Memory;
        private String Warnings = "";

        private const int HexLengthOffset = 1;
        private const int HexAddressOffset = 3;
        private const int HexRecordTypeOffset = 7;
        private const int HexDataOffset = 9;

        private UInt32 HexToVal(string s)
        {
            UInt32 returnvalue = 0;
            s = s.ToUpper();
            for (int i = 0; i < s.Length; ++i)
            {
                if ((s[i] >= '0') && (s[i] <= '9'))
                {
                    returnvalue *= 16;
                    returnvalue += (UInt32)((byte)(s[i]) - (byte)'0');
                }
                else if ((s[i] >= 'A') && (s[i] <= 'F'))
                {
                    byte b = (byte)s[i];
                    b -= (byte)'A';
                    b += 10;
                    returnvalue *= 16;
                    returnvalue += b;
                }
                else
                {
                    throw new InvalidDataException("Invalid character " + s[i] + " in Hex conversion");
                }
            }
            return returnvalue;
        }


        public HexData()
        {
            Memory = new Hashtable();
        }

        public HexData(string filename, Boolean enforceChecksum) : this()
        {
            Load(filename, enforceChecksum);
        }

        public void Load(string Filename, Boolean EnforceChecksum)
        {
            using (StreamReader sr = new StreamReader(Filename))
            {
                String line;

                UInt32 extendedaddress = 0;
                // Read and display lines from the file until the end of 
                // the file is reached.
                while ((line = sr.ReadLine()) != null)
                {
                    //First remove any whitespace
                    line = Regex.Replace(line, "\\s*", "");

                    //Ignore lines that contain illegal characters
                    if (Regex.Match(line, "[^A-Fa-f0-9:]").Success)
                    {
                        continue;
                    }
                    //The following prevents out of range exceptions later
                    if (line.Length < 11)
                    {
                        continue;
                    }

                    // First character should be a ':'
                    if (line[0] != ':')
                    {
                        continue;
                    }

                    // Get the length.  There are 11 bytes per line in addition to
                    // the data.  Each byte of data takes 2 ascii characters to 
                    // display.  Check that the indicated length matches the 
                    // true length
                    UInt32 length = HexToVal(line.Substring(HexLengthOffset, 2));
                    if ((length * 2 + 11) != line.Length)
                    {
                        continue;
                    }
                    UInt32 lineaddress = HexToVal(line.Substring(HexAddressOffset, 4));

                    UInt32 indicatedchecksum = HexToVal(line.Substring(line.Length - 2, 2));
                    UInt32 calculatedchecksum = 0;

                    for (int i = HexLengthOffset; i < line.Length - 3; i += 2)
                    {
                        calculatedchecksum += HexToVal(line.Substring(i, 2));
                    }
                    calculatedchecksum %= 256;
                    calculatedchecksum = (256 - calculatedchecksum) % 256;

                    if ((calculatedchecksum != indicatedchecksum) && EnforceChecksum)
                    {
                        continue;
                    }

                    // At this point, the line has been validated for length, leading
                    // character, and checksum.
                    // Switch based on command character

                    UInt32 recordtype = HexToVal(line.Substring(HexRecordTypeOffset, 2));

                    switch (recordtype)
                    {
                        case 0:
                            // Data Record

                            for (UInt32 i = 0; i < length; ++i)
                            {
                                byte data = (byte)HexToVal(line.Substring((int)(HexDataOffset + 2 * i), 2));
                                UInt32 byteaddress = extendedaddress * 65536 + lineaddress + i;
                                if (Memory.ContainsKey((UInt32)(byteaddress)))
                                {
                                    Warnings += "Address " + String.Format("{0:X}", byteaddress) +
                                        "is defined multiple times";
                                }
                                Memory[byteaddress] = data;
                            }

                            break;

                        case 4:
                            //Extended Record

                            if (length != 2)
                            {
                                continue;
                            }
                            extendedaddress = HexToVal(line.Substring(HexDataOffset, 4));

                            break;

                        case 1:
                            // End of File

                            break;

                        default:
                            //unsupported line type

                            break;
                    }
                }
            }
        }

        public string TwoColumn()
        {
            string s = "";
            ArrayList keys = SortedKeys();
            foreach (UInt32 key in keys)
            {
                s += string.Format("{0:X}", key) + " " + string.Format("{0,2:X}", Memory[key]) + "\n";
            }
            return s;
        }

        private ArrayList SortedKeys()
        {
            ArrayList aKeys = new ArrayList(Memory.Keys);
            aKeys.Sort();
            return aKeys;
        }

        public UInt32 HighestAddress
        {
            get
            {
                ArrayList aKeys = SortedKeys();
                return ((UInt32)aKeys[aKeys.Count - 1]);
            }
        }

        public UInt32 LowestAddress
        {
            get
            {
                ArrayList aKeys = SortedKeys();
                return ((UInt32)aKeys[0]);
            }
        }

        public void Crop(UInt32 InclusiveStartAddress, UInt32 ExclusiveEndAddress)
        {
            ArrayList keys = SortedKeys();

            foreach (UInt32 key in keys)
            {
                if (key < InclusiveStartAddress || key >= ExclusiveEndAddress)
                {
                    Memory.Remove(key);
                }
            }
        }

        public void Fill(UInt32 InclusiveStartAddress, UInt32 ExclusiveEndAddress, byte value)
        {
            for (UInt32 address = InclusiveStartAddress; address < ExclusiveEndAddress; ++address)
            {
                if (!Memory.ContainsKey(address))
                {
                    Memory[address] = value;
                }
            }
        }

        public void Fill16(UInt32 InclusiveStartAddress, UInt32 ExclusiveEndAddress, UInt16 value)
        {
            for (UInt32 address = InclusiveStartAddress; address < ExclusiveEndAddress; address += 2)
            {
                if (!Memory.ContainsKey(address))
                {
                    Memory[address] = (byte)(value & 0xFF);
                    Memory[address + 1] = (byte)(value >> 8);
                }
            }
        }

        public void Binary(string Filename, UInt32 InclusiveStartAddress, UInt32 ExclusiveEndAddress, byte value)
        {
            FileStream fs = new FileStream(Filename, FileMode.Create);
            {
                for (UInt32 i = InclusiveStartAddress; i < ExclusiveEndAddress; ++i)
                {
                    if (Memory.ContainsKey(i))
                    {
                        fs.WriteByte((byte)Memory[i]);
                    }
                    else
                    {
                        fs.WriteByte(value);
                    }
                }
            }
            fs.Close();

        }

        public void Add(ref HexData x)
        {
            ArrayList aKeys = new ArrayList(x.Memory.Keys);
            foreach (UInt32 key in aKeys)
            {
                if (Memory.ContainsKey(key))
                {
                    Warnings += (string.Format("Warning, address 0x{0,X} is being overwritten in add.  Original value: 0x{1,X} New Value: 0x{2,X}", key, Memory[key], x.Memory[key]));
                }
                Memory[key] = x.Memory[key];
            }
        }

        public byte[] Subarray(uint start, uint length)
        {
            byte[] b = new byte[length];
            for (int i = 0; i < length; ++i)
            {
                b[i] = (byte)Memory[(UInt32)(i + start)];
            }
            return (b);
        }
    }
}
