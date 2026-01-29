using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Numerics;

namespace SplitExt
{
    public static class Win32
    {
        public static IntPtr processHandle;
        public static IntPtr baseAddress;

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
        public const int MOUSEEVENTF_MOVE = 0x0001;

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

        public static bool AttachToProcess(string processName, out IntPtr procHandle, out IntPtr moduleBase)
        {
            procHandle = IntPtr.Zero;
            moduleBase = IntPtr.Zero;

            Process[] procs = Process.GetProcessesByName(processName);
            if (procs.Length == 0)
            {
                Console.WriteLine($"[-] Process {processName}.exe not found");
                return false;
            }

            procHandle = OpenProcess(0x0010 | 0x0020 | 0x0400, false, procs[0].Id);
            if (procHandle == IntPtr.Zero)
            {
                Console.WriteLine("[-] Failed to open process handle");
                return false;
            }

            moduleBase = procs[0].MainModule?.BaseAddress ?? IntPtr.Zero;
            if (moduleBase == IntPtr.Zero)
            {
                Console.WriteLine("[-] Failed to get base address");
                return false;
            }

            processHandle = procHandle;
            baseAddress   = moduleBase;

            Console.WriteLine($"[+] Attached to {processName}.exe");
            Console.WriteLine($"[OFFSET] Base Address: 0x{baseAddress.ToInt64():X}");

            return true;
        }

        public static long ReadInt64(long address)
        {
            if (address == 0) return 0;
            byte[] buffer = new byte[8];
            if (!ReadProcessMemory(processHandle, (IntPtr)address, buffer, 8, out _))
                return 0;
            return BitConverter.ToInt64(buffer, 0);
        }

        public static int ReadInt32(long address)
        {
            if (address == 0) return 0;
            byte[] buffer = new byte[4];
            if (!ReadProcessMemory(processHandle, (IntPtr)address, buffer, 4, out _))
                return 0;
            return BitConverter.ToInt32(buffer, 0);
        }

        public static float ReadFloat(long address)
        {
            if (address == 0) return 0f;
            byte[] buffer = new byte[4];
            if (!ReadProcessMemory(processHandle, (IntPtr)address, buffer, 4, out _))
                return 0f;
            return BitConverter.ToSingle(buffer, 0);
        }

        public static byte ReadByte(long address)
        {
            if (address == 0) return 0;
            byte[] buffer = new byte[1];
            if (!ReadProcessMemory(processHandle, (IntPtr)address, buffer, 1, out _))
                return 0;
            return buffer[0];
        }

        public static Vector3 ReadVector3(long address)
        {
            if (address == 0) return Vector3.Zero;
            return new Vector3(
                ReadFloat(address),
                ReadFloat(address + 4),
                ReadFloat(address + 8)
            );
        }

        public static bool WriteInt64(long address, long value)
        {
            if (address == 0) return false;
            byte[] buffer = BitConverter.GetBytes(value);
            return WriteProcessMemory(processHandle, (IntPtr)address, buffer, buffer.Length, out _);
        }

        public static bool WriteInt32(long address, int value)
        {
            if (address == 0) return false;
            byte[] buffer = BitConverter.GetBytes(value);
            return WriteProcessMemory(processHandle, (IntPtr)address, buffer, buffer.Length, out _);
        }

        public static bool WriteFloat(long address, float value)
        {
            if (address == 0) return false;
            byte[] buffer = BitConverter.GetBytes(value);
            return WriteProcessMemory(processHandle, (IntPtr)address, buffer, buffer.Length, out _);
        }

        public static bool WriteByte(long address, byte value)
        {
            if (address == 0) return false;
            byte[] buffer = new byte[] { value };
            return WriteProcessMemory(processHandle, (IntPtr)address, buffer, buffer.Length, out _);
        }

        public static IntPtr BaseAddress => baseAddress;
    }
}