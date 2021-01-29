using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace SoftEtherVPN_AutoMacro
{
    class ExternalProcess
    {
        IntPtr process_handle;

        enum ProcessFlags
        {
            PROCESS_VM_OPERATION = 0x0008,
            PROCESS_VM_READ = 0x0010,
            PROCESS_VM_WRITE = 0x0020,
            PROCESS_QUERY_INFORMATION = 0x0400
        }

        enum MemoryFlags
        {
            PAGE_READWRITE = 0x04,
            MEM_COMMIT = 0x1000,
            MEM_RELEASE = 0x8000
        }

        public ExternalProcess(uint process_id)
        {
            process_handle = OpenProcess((uint)ProcessFlags.PROCESS_VM_OPERATION | (uint)ProcessFlags.PROCESS_VM_READ |
                (uint)ProcessFlags.PROCESS_VM_WRITE | (uint)ProcessFlags.PROCESS_QUERY_INFORMATION, false, process_id);

            if (process_handle == IntPtr.Zero)
            {
                Console.WriteLine("Could not open process " + process_id);
            }
        }

        ~ExternalProcess()
        {
            CloseHandle(process_handle);
        }

        public static ExternalProcess FromWindow(Window window)
        {
            uint process_id;
            GetWindowThreadProcessId(window.Handle, out process_id);

            return new ExternalProcess(process_id);
        }

        public static ExternalProcess Create(string command)
        {
            PROCESS_INFORMATION process_info = new PROCESS_INFORMATION();
            STARTUPINFO startup_info = new STARTUPINFO();

            startup_info.cb = Marshal.SizeOf(startup_info);
            startup_info.dwFlags = STARTF_USESTDHANDLES;

            SECURITY_ATTRIBUTES pSec = new SECURITY_ATTRIBUTES();
            SECURITY_ATTRIBUTES tSec = new SECURITY_ATTRIBUTES();

            bool result = CreateProcess(null, command, ref pSec, ref tSec, true,
                DETACHED_PROCESS, IntPtr.Zero, null, ref startup_info, out process_info);

            int process_id = process_info.dwProcessId;

            if (!result)
            {
                Console.Error.WriteLine("CreateProcess failed");
                return null;
            }

            return new ExternalProcess((uint)process_id);
        }

        public IntPtr AllocateMemory(int size)
        {
            IntPtr pointer = VirtualAllocEx(process_handle, IntPtr.Zero, (uint)size,
                (uint)MemoryFlags.MEM_COMMIT, (uint)MemoryFlags.PAGE_READWRITE);

            if (pointer == IntPtr.Zero) Console.WriteLine("Could not allocate " + size + " bytes of memory in process " + process_handle);

            return pointer;
        }

        public void FreeMemory(IntPtr pointer)
        {
            VirtualFreeEx(process_handle, pointer, UIntPtr.Zero, (uint)MemoryFlags.MEM_RELEASE);
        }

        public void Write(byte[] local_source, IntPtr external_dest, int size)
        {
            //WriteProcessMemory(party_process, dest, source, size, NULL);

            if (!WriteProcessMemory(process_handle, external_dest, local_source, (uint)size, IntPtr.Zero))
            {
                Console.WriteLine("Failed to write to process " + process_handle);
            }
        }

        public void Write(IntPtr local_source, IntPtr external_dest, int size)
        {
            if (!WriteProcessMemory(process_handle, external_dest, local_source, (uint)size, IntPtr.Zero))
            {
                Console.WriteLine("Failed to write to process " + process_handle);
            }
        }

        public void Read(IntPtr external_source, byte[] local_dest, int size)
        {
            //ReadProcessMemory(party_process, source, dest, size, NULL);
            IntPtr numRead = (IntPtr)0;

            ReadProcessMemory(process_handle, external_source, local_dest, (uint)size, ref numRead);
        }


        private byte[] ReadMemory(UInt64 address, UInt32 length)
        {
            // Copy the bytes from this heap
            byte[] buffer = new byte[length];
            IntPtr numRead = (IntPtr)0;
            bool result = ReadProcessMemory(process_handle, (IntPtr)address, buffer, (uint)length,ref numRead);

            // Check that all the data was read correctly
            if ((UInt32)numRead != length)
            {
                return null;
            }

            if (!result)
            {
                return null;
            }

            return buffer;
        }

        private object RawDataToObject(ref byte[] rawData, Type overlayType)
        {
            object result = null;

            GCHandle pinnedRawData = GCHandle.Alloc(rawData,
                GCHandleType.Pinned);
            try
            {

                // Get the address of the data array
                IntPtr pinnedRawDataPtr =
                    pinnedRawData.AddrOfPinnedObject();

                // overlay the data type on top of the raw data
                result = Marshal.PtrToStructure(
                    pinnedRawDataPtr,
                    overlayType);
            }
            finally
            {
                // must explicitly release
                pinnedRawData.Free();
            }

            return result;
        }

        public bool IsProcessX86()
        {
            IMAGE_DOS_HEADER dosHeader;
            IMAGE_NT_HEADERS64 ntHeader;

            // Copy the bytes from this heap
            byte [] dosHeaderData = ReadMemory(0, (uint)Marshal.SizeOf(typeof(IMAGE_DOS_HEADER)));
            dosHeader = (IMAGE_DOS_HEADER)RawDataToObject(ref dosHeaderData, typeof(IMAGE_DOS_HEADER));
            if (!dosHeader.isValid)
            {
                throw new System.ApplicationException("Library does not appear to be a valid DOS file!");
            }


            byte[] ntHeaderData = ReadMemory((ulong)dosHeader.e_lfanew, (uint)Marshal.SizeOf(typeof(IMAGE_NT_HEADERS64)));
            ntHeader = (IMAGE_NT_HEADERS64)RawDataToObject(ref dosHeaderData, typeof(IMAGE_NT_HEADERS64));
            if (!ntHeader.isValid)
            {
                throw new System.ApplicationException("Library NT Header does not appear valid!");
            }

            if (ntHeader.FileHeader.Machine == (ushort)MachineType.I386)
            {
                return true;
            }

            return false;
        }

        public bool IsProcessX64()
        {
            /*
            Boolean result = true;
            int ret = IsWow64Process(process_handle, out result);
            if( ret ==0 )
            {
                int err = GetLastError();
                if( err !=0 )
                {

                }
            }

            return result;*/

            IMAGE_DOS_HEADER dosHeader;
            IMAGE_NT_HEADERS64 ntHeader;

            // Copy the bytes from this heap
            byte[] dosHeaderData = ReadMemory(0, (uint)Marshal.SizeOf(typeof(IMAGE_DOS_HEADER)));
            dosHeader = (IMAGE_DOS_HEADER)RawDataToObject(ref dosHeaderData, typeof(IMAGE_DOS_HEADER));
            if (!dosHeader.isValid)
            {
                throw new System.ApplicationException("Library does not appear to be a valid DOS file!");
            }


            byte[] ntHeaderData = ReadMemory((ulong)dosHeader.e_lfanew, (uint)Marshal.SizeOf(typeof(IMAGE_NT_HEADERS64)));
            ntHeader = (IMAGE_NT_HEADERS64)RawDataToObject(ref dosHeaderData, typeof(IMAGE_NT_HEADERS64));
            if (!ntHeader.isValid)
            {
                throw new System.ApplicationException("Library NT Header does not appear valid!");
            }

            if (ntHeader.FileHeader.Machine == (ushort)MachineType.x64)
            {
                return true;
            }

            return false;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);


        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress,
           uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress,
           UIntPtr dwSize, uint dwFreeType);

        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
           byte[] lpBuffer, uint size, IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
          IntPtr lpBuffer, uint size, IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] buffer, UInt32 size, ref IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32")]
        public static extern Int32 IsWow64Process(IntPtr hProcess, out Boolean bWow64Process);

        [DllImport("kernel32")]
        public static extern Int32 GetLastError();



    const Int32 STARTF_USESTDHANDLES = 0x00000100;

        const Int32 DETACHED_PROCESS = 0x00000008;

        [StructLayout(LayoutKind.Sequential)]
        struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [DllImport("kernel32.dll")]
        static extern bool CreateProcess(string lpApplicationName,
              string lpCommandLine, ref SECURITY_ATTRIBUTES lpProcessAttributes,
              ref SECURITY_ATTRIBUTES lpThreadAttributes, bool bInheritHandles,
              uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
              [In] ref STARTUPINFO lpStartupInfo,
              out PROCESS_INFORMATION lpProcessInformation);


        [StructLayout(LayoutKind.Sequential)]
        struct IMAGE_DOS_HEADER
        { // DOS .EXE header
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public char[] e_magic;       // Magic number
            public UInt16 e_cblp;    // Bytes on last page of file
            public UInt16 e_cp;      // Pages in file
            public UInt16 e_crlc;    // Relocations
            public UInt16 e_cparhdr;     // Size of header in paragraphs
            public UInt16 e_minalloc;    // Minimum extra paragraphs needed
            public UInt16 e_maxalloc;    // Maximum extra paragraphs needed
            public UInt16 e_ss;      // Initial (relative) SS value
            public UInt16 e_sp;      // Initial SP value
            public UInt16 e_csum;    // Checksum
            public UInt16 e_ip;      // Initial IP value
            public UInt16 e_cs;      // Initial (relative) CS value
            public UInt16 e_lfarlc;      // File address of relocation table
            public UInt16 e_ovno;    // Overlay number
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public UInt16[] e_res1;    // Reserved words
            public UInt16 e_oemid;       // OEM identifier (for e_oeminfo)
            public UInt16 e_oeminfo;     // OEM information; e_oemid specific
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public UInt16[] e_res2;    // Reserved words
            public Int32 e_lfanew;      // File address of new exe header

            private string _e_magic
            {
                get { return new string(e_magic); }
            }

            public bool isValid
            {
                get { return _e_magic == "MZ"; }
            }
        }

        public enum MachineType : ushort
        {
            Native = 0,
            I386 = 0x014c,
            Itanium = 0x0200,
            x64 = 0x8664
        }
        public enum MagicType : ushort
        {
            IMAGE_NT_OPTIONAL_HDR32_MAGIC = 0x10b,
            IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x20b
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct IMAGE_NT_HEADERS32
        {
            [FieldOffset(0)]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public char[] Signature;

            [FieldOffset(4)]
            public IMAGE_FILE_HEADER FileHeader;

            [FieldOffset(24)]
            public IMAGE_OPTIONAL_HEADER32 OptionalHeader;

            private string _Signature
            {
                get { return new string(Signature); }
            }

            public bool isValid
            {
                get { return _Signature == "PE\0\0" && OptionalHeader.Magic == (ushort)MagicType.IMAGE_NT_OPTIONAL_HDR32_MAGIC; }
            }
        }
        [StructLayout(LayoutKind.Explicit)]
        public struct IMAGE_NT_HEADERS64
        {
            [FieldOffset(0)]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public char[] Signature;

            [FieldOffset(4)]
            public IMAGE_FILE_HEADER FileHeader;

            [FieldOffset(24)]
            public IMAGE_OPTIONAL_HEADER64 OptionalHeader;

            private string _Signature
            {
                get { return new string(Signature); }
            }

            public bool isValid
            {
                get { return _Signature == "PE\0\0" && OptionalHeader.Magic == (ushort)MagicType.IMAGE_NT_OPTIONAL_HDR64_MAGIC; }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_FILE_HEADER
        {
            public UInt16 Machine;
            public UInt16 NumberOfSections;
            public UInt32 TimeDateStamp;
            public UInt32 PointerToSymbolTable;
            public UInt32 NumberOfSymbols;
            public UInt16 SizeOfOptionalHeader;
            public UInt16 Characteristics;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_DATA_DIRECTORY
        {
            public UInt32 VirtualAddress;
            public UInt32 Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_OPTIONAL_HEADER32
        {
            public UInt16 Magic;
            public Byte MajorLinkerVersion;
            public Byte MinorLinkerVersion;
            public UInt32 SizeOfCode;
            public UInt32 SizeOfInitializedData;
            public UInt32 SizeOfUninitializedData;
            public UInt32 AddressOfEntryPoint;
            public UInt32 BaseOfCode;
            public UInt32 BaseOfData;
            public UInt32 ImageBase;
            public UInt32 SectionAlignment;
            public UInt32 FileAlignment;
            public UInt16 MajorOperatingSystemVersion;
            public UInt16 MinorOperatingSystemVersion;
            public UInt16 MajorImageVersion;
            public UInt16 MinorImageVersion;
            public UInt16 MajorSubsystemVersion;
            public UInt16 MinorSubsystemVersion;
            public UInt32 Win32VersionValue;
            public UInt32 SizeOfImage;
            public UInt32 SizeOfHeaders;
            public UInt32 CheckSum;
            public UInt16 Subsystem;
            public UInt16 DllCharacteristics;
            public UInt32 SizeOfStackReserve;
            public UInt32 SizeOfStackCommit;
            public UInt32 SizeOfHeapReserve;
            public UInt32 SizeOfHeapCommit;
            public UInt32 LoaderFlags;
            public UInt32 NumberOfRvaAndSizes;
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_OPTIONAL_HEADER64
        {
            public UInt16 Magic;
            public Byte MajorLinkerVersion;
            public Byte MinorLinkerVersion;
            public UInt32 SizeOfCode;
            public UInt32 SizeOfInitializedData;
            public UInt32 SizeOfUninitializedData;
            public UInt32 AddressOfEntryPoint;
            public UInt32 BaseOfCode;
            public UInt64 ImageBase;
            public UInt32 SectionAlignment;
            public UInt32 FileAlignment;
            public UInt16 MajorOperatingSystemVersion;
            public UInt16 MinorOperatingSystemVersion;
            public UInt16 MajorImageVersion;
            public UInt16 MinorImageVersion;
            public UInt16 MajorSubsystemVersion;
            public UInt16 MinorSubsystemVersion;
            public UInt32 Win32VersionValue;
            public UInt32 SizeOfImage;
            public UInt32 SizeOfHeaders;
            public UInt32 CheckSum;
            public UInt16 Subsystem;
            public UInt16 DllCharacteristics;
            public UInt64 SizeOfStackReserve;
            public UInt64 SizeOfStackCommit;
            public UInt64 SizeOfHeapReserve;
            public UInt64 SizeOfHeapCommit;
            public UInt32 LoaderFlags;
            public UInt32 NumberOfRvaAndSizes;
        }

    }
}

