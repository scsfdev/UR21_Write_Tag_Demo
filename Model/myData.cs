using GalaSoft.MvvmLight.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using static UR21_Write_Tag_Demo.Model.NativeMethods;

namespace UR21_Write_Tag_Demo.Model
{
    internal static class MyConst
    {
        internal const string ERROR = "<< ERROR >>";
        internal const string WARNING = "<< WARNING >>";
        internal const string INFO = "<< INFO >>";

        internal const string OK = "OK";
        internal const string CANCEL = "CANCEL";

        internal const string EXIT = "EXIT";

        internal const string CONNECT = "CONNECT";
        internal const string DISCONNECT = "DISCONNECT";

        internal const string TITLE = "UR21 WRITE DEMO APP";
    }

    public enum MsgType
    {
        MAIN_V,
        MAIN_V_CONFIRM,
        MAIN_VM,
        TERMINATOR
    }

    public enum ErrCode
    {
        Err_Null = 0
    }

    internal static class General
    {
        internal static string gReplyMsg(ErrCode errCode, string strMainmsg, bool? bErr = null)
        {
            string strTitle = "";
            string strErrCode = "";

            if (bErr == null)
                strTitle = MyConst.INFO;
            else if (bErr == true)
                strTitle = MyConst.ERROR;
            else
                strTitle = MyConst.WARNING;

            strErrCode = ((int)errCode).ToString();

            if (strErrCode == "0")
                return strTitle + Environment.NewLine + Environment.NewLine + strMainmsg;
            else if (bErr == true)
                return strTitle + Environment.NewLine + Environment.NewLine +
                       "Error Code: " + strErrCode + Environment.NewLine +
                       "Message: " + strMainmsg;
            else
                return strTitle + Environment.NewLine + Environment.NewLine +
                       "Message: " + strMainmsg;
        }

        internal static string gGetVersion()
        {
            System.Reflection.Assembly asm = System.Reflection.Assembly.GetEntryAssembly();
            return asm.GetName().Version.Major.ToString() + "." + asm.GetName().Version.Minor.ToString() + "." + asm.GetName().Version.Revision.ToString();
        }

        internal static string gGetExeLocation()
        {
            string strCompany = ((System.Reflection.AssemblyCompanyAttribute)Attribute.GetCustomAttribute(
                                System.Reflection.Assembly.GetExecutingAssembly(), typeof(System.Reflection.AssemblyCompanyAttribute), false)).Company;
            string strPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), strCompany);
            strPath = Path.Combine(strPath, System.Reflection.Assembly.GetEntryAssembly().GetName().Name);

            return strPath;
        }


        internal static byte[] gHexToByteArray(string strHex)
        {
            var index = 0;
            var result = new byte[strHex.Length / 2];
            for (var i = 0; i < strHex.Length - 1; i += 2)
            {
                var hex = Convert.ToInt32(strHex.Substring(i, 2), 16);
                result[index++] = Convert.ToByte(hex);
            }
            return result;
        }

        internal static string gBytesToHex(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }

        internal static string gHexToString(string strHex)
        {
            string strData = "";
            while (strHex.Length > 0)
            {
                strData += Convert.ToChar(Convert.ToInt32(strHex.Substring(0, 2), 16)).ToString();
                strHex = strHex.Substring(2, strHex.Length - 2);
            }
            return strData;
        }

        internal static string gStrToHex(string strData)
        {
            string strHex = "";
            foreach (char c in strData)
            {
                int iTmp = c;
                strHex += string.Format("{0:x2}", Convert.ToInt32(iTmp.ToString()));
            }
            return strHex;
        }
    }

    public class Tag
    {
        public string Uii { get; set; }
        public int No { get; set; }
        public int Qty { get; set; }

        public string ReadDate { get; set; }
        public string ReadTime { get; set; }

        public Tag() { }

        public Tag(Tag t)
        {
            Uii = t.Uii;
            No = t.No;
            Qty = t.Qty;
            ReadDate = t.ReadDate;
            ReadTime = t.ReadTime;
        }
    }


    // Create custom C# event >> https://www.codeproject.com/Articles/9355/Creating-advanced-C-custom-events
    public class TagArgs : EventArgs
    {
        public TagArgs()
        {
            Uii = null;
            Qty = 0;
        }

        //public string uii { get; set; }
        //public int qty { get; set; }
        public int qty;
        public int Qty
        {
            get { return qty; }
            set { qty = value; }
        }


        private string uii;
        public string Uii
        {
            get { return uii; }
            set { uii = value; }
        }

    }

    unsafe public class Ur21
    {
        public static UiiData ud = new UiiData();

        public delegate void TagHandler(object sender, TagArgs e);
        public static event TagHandler OnTagRead;

        Thread t1;
        byte bPort = 0;
        bool bTrue = false;

        IntPtr uiiBuf = Marshal.AllocHGlobal(sizeof(UiiData) * (int)500);


        public bool ConnectUR21(byte byPort)
        {
            bPort = byPort;
            try
            {
                uint iReturn;

                iReturn = UtsOpen(bPort);
                if (iReturn != 0)
                {
                    Messenger.Default.Send(MyConst.ERROR + Environment.NewLine + "Open port error: " + iReturn.ToString("X2"), MsgType.MAIN_VM);
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Messenger.Default.Send(MyConst.ERROR + Environment.NewLine + "An error occured while connecting to UR21!" +
                                        Environment.NewLine + Environment.NewLine + "Error Details: " + e.Message, MsgType.MAIN_VM);
                return false;
            }
        }

        internal bool DisconnectUR21()
        {
            try
            {
                uint iReturn;

                iReturn = UtsClose(bPort);
                if (iReturn != 0 && iReturn != 43266)
                {
                    Messenger.Default.Send(MyConst.ERROR + Environment.NewLine + "Close port error: " + iReturn.ToString("X2"), MsgType.MAIN_VM);
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Messenger.Default.Send(MyConst.ERROR + Environment.NewLine + "An error occured while disconnecting to UR21!" +
                                        Environment.NewLine + Environment.NewLine + "Error Details: " + e.Message, MsgType.MAIN_VM);
                return false;
            }
        }


        internal bool ReadOneTag(ref Tag tIn)
        {
            try
            {
                bTrue = true;
                uint iReturn;
                uint iReadCount;
                uint iRemainCount;
                uint iBufCount = 500;
                int i = 0;

                iReturn = UtsCheckAlive(bPort);
                if (iReturn != 0)
                {
                    Messenger.Default.Send(MyConst.ERROR + Environment.NewLine + "COM port checking error - " + iReturn.ToString("X2"), MsgType.MAIN_VM);
                    return false;
                }

                iReturn = UtsAbort(bPort);
                if (iReturn != 0)
                {
                    Messenger.Default.Send(MyConst.ERROR + Environment.NewLine + "Abort port error - " + iReturn.ToString("X2"), MsgType.MAIN_VM);
                    return false;
                }

                Tag tRead = new Tag();

                while (bTrue)
                {
                    iReturn = UtsReadUii(bPort);
                    if (iReturn != 0)        // There is an error, but keep going.
                        continue;

                    do
                    {
                        iReturn = UtsGetUii(bPort, (void*)uiiBuf, iBufCount, out iReadCount, out iRemainCount);

                        if (iReturn == 1)
                        {
                            iRemainCount = 1;
                            continue;
                        }

                        for (i = 0; i < iReadCount; i++)
                        {
                            ud = (UiiData)Marshal.PtrToStructure(uiiBuf + (sizeof(UiiData) * i), typeof(UiiData));

                            // If we do not use fixed keyword, it will throw CS1666 error.
                            fixed (UiiData* uf = &ud)
                            {
                                byte[] bUii = new byte[uf->length];

                                Marshal.Copy((IntPtr)uf->uii, bUii, 0, (int)uf->length);

                                tRead.Uii = BitConverter.ToString(bUii).Replace("-", "");
                            }

                            if (tRead.Uii != null)
                                break;
                        }

                        if (tRead.Uii != null)
                            bTrue = false;
                    }
                    while ((iRemainCount > 0) && bTrue);
                }

                tIn = tRead;
                return true;
            }
            catch (ThreadAbortException)
            {
                // If user close the form.
                UtsClose(bPort);
                Messenger.Default.Send(MyConst.ERROR + Environment.NewLine + "Action was aborted by user!", MsgType.MAIN_VM);
                return false;
            }
            catch (Exception e)
            {
                Messenger.Default.Send(MyConst.ERROR + Environment.NewLine + "An error occured while running UR21 Api!" +
                                        Environment.NewLine + Environment.NewLine + "Error Details: " + e.Message, MsgType.MAIN_VM);
                return false;
            }
        }

        internal bool WriteOneTag(string strOrig, string strWrite)
        {
            try
            {
                bTrue = true;
                uint iReturn;
                
                byte bTagCmd = 0x03;
                ushort uAntenna = 0x0001;

                WRITEPARAM wParam = new WRITEPARAM();
                wParam.bank = 0x01;             // UII bank.
                wParam.reserved = 0x00;         // Reserved data fixed. 
                wParam.size = 12;  // 12 byte = 96 bit. +2 for PC byte. // (ushort)Encoding.ASCII.GetByteCount(General.gStrToHex(strWrite));
                wParam.accesspwd[0] = 0x00;
                wParam.accesspwd[1] = 0x00;
                wParam.accesspwd[2] = 0x00;
                wParam.accesspwd[3] = 0x00;


                /*
                 * Example of EPC Memory Bank 01.
                 * 
                 * 00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F      <<-- 00 to 0F <<-- This is 16 bit which is 2 byte for CRC.
                 * 10 11 12 13 14 15 16 17 18 19 1A 1B 1C 1D 1E 1F      <<-- 10 to 1F <<-- This is 16 bit which is 2 byte for PC.
                 * 20 21 22 23 24 25 26 27 28 29 2A 2B 2C 2D 2E 2F      <<-- 20 to 7F <<-- Start from this till end is 96 bit for EPC assume this is 96 bit EPC tag.
                 * 30 31 32 33 34 35 36 37 38 39 3A 3B 3C 3D 3E 3F
                 * 40 41 42 43 44 45 46 47 48 49 4A 4B 4C 4D 4E 4F
                 * 50 51 52 53 54 55 56 57 58 59 5A 5B 5C 5D 5E 5F
                 * 60 61 62 63 64 65 66 67 68 69 6A 6B 6C 6D 6E 6F
                 * 70 71 72 73 74 75 76 77 78 79 7A 7B 7C 7D 7E 7F
                 */

                //float iData = 0;
                //string strHex = "";

                //if (float.TryParse(strWrite, out iData))
                //{
                //    // Numeric. Conver it to GTIN 14 for EPC
                //    strHex = strWrite.PadLeft(14, '0');
                //}
                //else
                //    strHex = General.gStrToHex(strWrite);

                //if (strHex.Length != 26)
                //{
                //    strHex = strHex.PadRight(28, '0');
                //}


                ////// Remaining (12 byte) 12*8 = 98 bit is for UII (EPC) data.
                ////for (int i = 0; i < 12; i++)
                ////{
                ////    wParam.writedata[i] = Convert.ToByte(strWrite.Substring(i * 2, 2), 16);
                ////}

                byte[] byteTemp = Enumerable.Range(0, strWrite.Length)
                                      .Where(x => x % 2 == 0)
                                      .Select(x => Convert.ToByte(strWrite.Substring(x, 2), 16))
                                      .ToArray();

                int i = 0;
                foreach (byte b in byteTemp)
                {
                    wParam.writedata[i] = b;
                    i++;
                }


                //for (int i = 0; i < (strWrite.Length / 2); i++)
                //{
                //    wParam.writedata[i + 2] = Convert.ToByte(strWrite.Substring(i * 2, 2), 16);
                //}

                byte[] bPC = new byte[PC_SIZE];     // PC is 2 byte (16 bit).
                UiiData ud = (UiiData)Marshal.PtrToStructure((IntPtr)((UInt64)uiiBuf), typeof(UiiData));


                // First 2 byte is PC (Protocol Control) data.
                bPC[0] = ud.pc[0];                  // Get PC 2 byte data from previous read Tag.
                bPC[1] = ud.pc[1];

                wParam.ptr = 4;                     // Start writing position. 0 and 1 is for 16 bit CRC which we do not touch. 2 onward is for PC and UII.
               // wParam.writedata[0] = bPC[0];       // Put it to writeData so that we can write the same PC to new Tag.
             //   wParam.writedata[1] = bPC[1];

                // Check still connect to UR21 or not.
                iReturn = UtsCheckAlive(bPort);
                if (iReturn != 0)
                {
                    Messenger.Default.Send(MyConst.ERROR + Environment.NewLine + "COM port checking error - " + iReturn.ToString("X2"), MsgType.MAIN_VM);
                    return false;
                }

                // Abort any current job on UR21.
                iReturn = UtsAbort(bPort);
                if (iReturn != 0)
                {
                    Messenger.Default.Send(MyConst.ERROR + Environment.NewLine + "Abort port error - " + iReturn.ToString("X2"), MsgType.MAIN_VM);
                    return false;
                }

                // Start writing data.
                // We cannot directly put wParam here due to it is not void* type.
                // And we also cannot directly cast WRITEPARAM struct to void*.
                // So, we have to get the address of WRITEPARAM and cast it to void*.
                // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/unsafe-code-pointers/how-to-obtain-the-address-of-a-variable
                // https://www.c-sharpcorner.com/article/pointers-and-unsafe-code-in-c-sharp-everything-you-need-to-know/
                iReturn = UtsStartTagComm(bPort, bTagCmd, uAntenna, (void*)&wParam, 0x01, 1, (void*)uiiBuf);
                if(iReturn != 0)
                {
                    Messenger.Default.Send(MyConst.ERROR + Environment.NewLine + "Write tag fail - " + iReturn.ToString("X2"), MsgType.MAIN_VM);
                    return false;
                }

                return true;
            }
            catch (ThreadAbortException)
            {
                // If user close the form.
                UtsClose(bPort);
                Messenger.Default.Send(MyConst.ERROR + Environment.NewLine + "Action was aborted by user!", MsgType.MAIN_VM);
                return false;
            }
            catch (Exception e)
            {
                Messenger.Default.Send(MyConst.ERROR + Environment.NewLine + "An error occured while running UR21 Api!" +
                                        Environment.NewLine + Environment.NewLine + "Error Details: " + e.Message, MsgType.MAIN_VM);
                return false;
            }
        }




        public void ReadTag()
        {
            try
            {
                // int iCounter = 0;
                bTrue = true;
                uint iReturn;
                uint iReadCount;
                uint iRemainCount;
                uint iBufCount = 500;
                int i = 0;

                // More info on Marshal class >> https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal?view=netframework-4.5.1
                // More info on IntPtr >> https://docs.microsoft.com/en-us/dotnet/api/system.intptr?view=netframework-4.5.1

                // More info on what is void* in C# >> https://stackoverflow.com/questions/15527985/what-is-void-in-c
                IntPtr uiiBuf = Marshal.AllocHGlobal(sizeof(UiiData) * (int)iBufCount);
                //IntPtr uiiBuf = Marshal.AllocCoTaskMem(sizeof(UiiData) * (int)iBufCount);

                iReturn = UtsOpen(bPort);
                if (iReturn != 0)
                    throw new Exception("Open port error-" + iReturn.ToString("X2"));

                iReturn = UtsAbort(bPort);
                if (iReturn != 0)
                    throw new Exception("Abort port error-" + iReturn.ToString("X2"));

                while (bTrue)
                {
                    iReturn = UtsReadUii(bPort);
                    if (iReturn != 0)        // There is an error, but keep going.
                        continue;

                    do
                    {
                        iReturn = UtsGetUii(bPort, (void*)uiiBuf, iBufCount, out iReadCount, out iRemainCount);

                        if (iReturn == 1)
                        {
                            iRemainCount = 1;
                            continue;
                        }

                        //Console.WriteLine("Read: " + iReadCount);

                        for (i = 0; i < iReadCount; i++)
                        {
                            TagArgs e = new TagArgs();

                            // IntPtr to Structure >> https://stackoverflow.com/a/27680642/770989
                            ud = (UiiData)Marshal.PtrToStructure(uiiBuf + (sizeof(UiiData) * i), typeof(UiiData));
                            //                            ud = (UiiData)Marshal.PtrToStructure((IntPtr)((uint)uiiBuf + (sizeof(UiiData) * i)), typeof(UiiData));

                            // More info on fixed >> https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/fixed-statement
                            // Reference from here >> https://docs.microsoft.com/en-us/dotnet/csharp/misc/cs1666
                            // If we do not use fixed, it will throw CS1666 error.
                            fixed (UiiData* uf = &ud)
                            {
                                byte[] bUii = new byte[uf->length];


                                // How to get IntPtr from byte[] >> https://stackoverflow.com/questions/537573/how-to-get-intptr-from-byte-in-c-sharp
                                // Another example >> https://stackoverflow.com/a/27680642/770989
                                Marshal.Copy((IntPtr)uf->uii, bUii, 0, (int)uf->length);

                                // Console.WriteLine(BitConverter.ToString(br));
                                e.Uii = BitConverter.ToString(bUii).Replace("-", "");
                            }

                            //byte[] bUii = new byte[ud.length];
                            //Marshal.Copy((IntPtr)ud.uii, bUii, 0, ud.length);

                            //Console.WriteLine("  Read: " + i);

                            if (!OnTagRead.Equals(null))
                                OnTagRead(this, e);
                            // Console.WriteLine(General.HexToString(BitConverter.ToString(br).Replace("-", "")));
                        }
                    }
                    while ((iRemainCount > 0) && bTrue);
                }

                iReturn = UtsClose(bPort);
                if (iReturn > 0)
                    throw new Exception("Close port error-" + iReturn.ToString("X2"));
            }
            catch (ThreadAbortException)
            {
                UtsClose(bPort);
            }
            catch (Exception e)
            {
                Messenger.Default.Send(MyConst.ERROR + Environment.NewLine + "An error occured while running UR21 Api!" +
                                        Environment.NewLine + Environment.NewLine + "Error Details: " + e.Message, MsgType.MAIN_VM);
            }
        }

    }

    unsafe public class NativeMethods
    {
        // Data size definition
        public const int PC_SIZE = 2;
        public const int UII_SIZE = 62;
        public const int PWD_SIZE = 4;
        public const int TAGDATA_SIZE = 256;

        // Structure definition
        // UII data structure
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe public struct UiiData
        {
            public uint length;                 // Effective data length of UII information stored in uii
            public fixed byte pc[PC_SIZE];      // PC information on RF tag obtained
            public fixed byte uii[UII_SIZE];    // UII information on RF tag obtained. Stored from the head, 0x00 stored beyond the length specified by length
        };


        // UII data structure
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe public struct UiiDataInfo
        {
            public uint length;                 // Effective data length of UII information stored in uii
            public fixed byte pc[PC_SIZE];      // PC information on RF tag obtained
            public fixed byte uii[UII_SIZE];    // UII information on RF tag obtained. Stored from the head, 0x00 stored beyond the length specified by length
            public short rssi;                  // Stores the electric field strength value that received the read tag
            public ushort antenna;              // Stores number of antenna that received the read tag
        };


        // Tag data structure
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe public struct RESULTDATA
        {
            public ushort result;                   // Result of communication with RF tag
            public fixed byte reserved[2];          // Reserved (0x00,0x00 fixed)
            public uint uiilength;                  // Effective data length of UII information stored in uii
            public uint datalength;                 // Effective data length of data from memory stored in data
            public fixed byte pc[PC_SIZE];          // PC information on RF tag obtained
            public fixed byte uii[UII_SIZE];        // UII information on RF tag obtained. Stored from the head, 0x00 stored beyond the length specified by uiilength
            public fixed byte data[TAGDATA_SIZE];   // Data from memory on read RF tag. Stored from the head, 0x00 stored beyond the length specified by datalength
        };


        // Tag data structure
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe public struct RESULTDATA2
        {
            public ushort result;                   // Result of communication with RF tag
            public fixed byte reserved[2];          // Reserved (0x00,0x00 fixed)
            public uint uiilength;                  // Effective data length of UII information stored in uii
            public uint datalength;                 // Effective data length of data from memory stored in data
            public fixed byte pc[PC_SIZE];          // PC information on RF tag obtained
            public fixed byte uii[UII_SIZE];        // UII information on RF tag obtained. Stored from the head, 0x00 stored beyond the length specified by uiilength
            public fixed byte data[TAGDATA_SIZE];   // Data from memory on read RF tag. Stored from the head, 0x00 stored beyond the length specified by datalength
            public short rssi;                      // Stores the electric field strength value that received the read tag
            public ushort antenna;                  // Stores number of antenna that received the read tag
        };


        //Tag communication Read from Memory structure
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe public struct READPARAM
        {
            public byte bank;                           // Bank area to be read
            public byte reserved;                       // Reserved (0x00 fixed)
            public ushort size;                         // Reading size(2-256, only even number acceptable)
            public ushort ptr;                          // Pointer to the beginning of reading (only even number acceptable)
            public fixed byte accesspwd[PWD_SIZE];      // Password for authentication of RF tag (ALL 0x00: RF tag not authenticated)
        };


        //Tag communication Write to Memory structure
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe public struct WRITEPARAM
        {
            public byte bank;                           // Bank area to be read
            public byte reserved;                       // Reserved (0x00 fixed)
            public ushort size;                         // Writing size(2-64, even number only)
            public ushort ptr;                          // Pointer to the beginning of reading (only even number acceptable)
            public fixed byte accesspwd[PWD_SIZE];      // Password for authentication of RF tag (ALL 0x00: RF tag not authenticated)
            public fixed byte writedata[TAGDATA_SIZE];       // Data written to RF tag. Stored from the head, set 0x00 stored beyond the length specified by size
        };


        // Tag communication Lock structure
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe public struct LOCKPARAM
        {
            public byte target;                         // To be locked
            public byte locktype;                       // Locked state after change
            public fixed byte accesspwd[PWD_SIZE];      // Password for authentication of RF tag (ALL 0x00: RF tag not authenticated)
        };


        // Tag communication Kill structure
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe public struct KILLPARAM
        {
            public fixed byte killpwd[PWD_SIZE];        // Password for killing RF tag (ALL 0x00: RF tag cannot be killed)
        };


        // TLV parameter structure
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe public struct TLVPARAM
        {
            public ushort tag;              // Parameter tag number
            public ushort length;           // Value length (4 bytes)
            public uint value;              // Parameter setting
        };


        // Device list structure
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe public struct DEVLIST
        {
            public uint IPaddress;          // IP address
            public ushort TcpPort;          // Connection destination Tcp port number
            public ushort DevNo;            // Terminal control number
            public uint Status;             // Status of terminal 0x00000000: not used (before opening) 0x00000001:in use (while open)
        };


        // Interface to UtsOpen
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsOpen(byte Port);


        // Interface to UtsClose
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsClose(byte Port);


        // Interface to UtsAbort
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsAbort(byte Port);


        // Interface to UtsReadUii
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsReadUii(byte Port);


        // Interface to UtsGetUii
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        unsafe internal static extern uint UtsGetUii(byte Port, void* UIIBUF, uint BufCount, out uint GetCount, out uint RestCount);


        // Interface to UtsGetUiiInfo
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        unsafe internal static extern uint UtsGetUiiInfo(byte Port, void* UIIBUFINFO, uint BufCount, out uint GetCount, out uint RestCount);


        // Interface to UtsStartContinuousRead
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsStartContinuousRead(byte Port);


        // Interface to UtsStartContinuousReadEx
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsStartContinuousReadEx(byte Port);


        // Interface to UtsStopContinuousRead
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsStopContinuousRead(byte Port);


        // Interface to UtsGetContinuousReadResult
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        unsafe internal static extern uint UtsGetContinuousReadResult(byte Port, void* UIIBUF, uint BufCount, out uint GetCount);


        // Interface to UtsGetContinuousReadResultInfo
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        unsafe internal static extern uint UtsGetContinuousReadResultInfo(byte Port, void* UIIBUFINFO, uint BufCount, out uint GetCount);


        // Interface to UtsStartTagComm
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        unsafe internal static extern uint UtsStartTagComm(byte Port, byte TagCmd, ushort Antenna, void* Param, byte ListEnable, ushort ListNum, void* UIIBUF);


        // Interface to UtsGetTagCommResult
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        unsafe internal static extern uint UtsGetTagCommResult(byte Port, void* RESULTBUF, uint BufCount, out uint GetCount, out uint RestCount);

        // Interface to UtsGetTagCommResultInfo
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        unsafe internal static extern uint UtsGetTagCommResultInfo(byte Port, void* RESULTBUFINFO, uint BufCount, out uint GetCount, out uint RestCount);


        // Interface to UtsSetTagList
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern uint UtsSetTagList(byte Port, byte Type, ushort ListNum, void* UIIBUF);


        // Interface to UtsGetVersions
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsGetVersions(byte Port, out byte OSVer, out byte MainVer, out byte RFVer, out byte ChipVer, out byte OEMver);


        // Interface to UtsGetProductNo
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsGetProductNo(byte Port, out byte MainNo, out byte RFNo);


        // Interface to UtsGetParameter
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsGetParameter(byte Port, ushort Tag, out TLVPARAM TLVDATA);


        // Interface to UtsSetParameter
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsSetParameter(byte Port, TLVPARAM TLVDATA);


        // Interface to UtsLoadParameter
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsLoadParameter(byte Port, ref byte FilePath);


        // Interface to UtsUpdateDevice
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        unsafe static extern uint UtsUpdateDevice(byte Port, ref byte FilePath);


        // Interface to UtsInitialReset
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsInitialReset(byte Port);


        // Interface to UtsCheckAlive
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsCheckAlive(byte Port);


        // Interface to UtsGetNetworkConfig
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsGetNetworkConfig(byte Port, out uint IPaddress, out uint SubnetMask, out uint Gateway);


        // Interface to UtsSetNetworkConfig
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsSetNetworkConfig(byte Port, uint IPaddress, uint SubnetMask, uint Gateway);


        // Interface to UtsCreateLanDevice
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsCreateLanDevice(uint IPaddress, ushort TcpPort, out ushort DevNo);

        // Interface to UtsDeleteLanDevice
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsDeleteLanDevice(ushort DevNo);


        // Interface to UtsSetCurrentLanDevice
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsSetCurrentLanDevice(ushort DevNo);


        // Interface to UtsGetCurrentLanDevice
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsGetCurrentLanDevice(out ushort DevNo);


        // Interface to UtsGetLanDeviceInfo
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint UtsGetLanDeviceInfo(ushort DevNo, out uint IPaddress, out ushort TcpPort, out uint Status);


        // Interface to UtsListLanDevice
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        unsafe internal static extern uint UtsListLanDevice(out ushort DevCount, void* DEVICELIST);


        // Interface to UtsSetLanDevice
        [DllImport("RfidTs.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        unsafe internal static extern uint UtsSetLanDevice(ushort DevCount, void* DEVICELIST);
    }

}
