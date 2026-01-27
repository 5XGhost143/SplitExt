using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace SplitExt
{
    class External_Main
    {
        private const string PROCESS_NAME = "PortalWars-Win64-Shipping";
        
        private const long OFFSET_GNAMES = 0x571af40;
        private const long OFFSET_GOBJECTS = 0x57574f0;
        private const long OFFSET_GWORLD = 0x589cb60;
        private const int OFFSET_OwningGameInstance = 0x180;
        private const int OFFSET_LocalPlayers = 0x38;
        private const int OFFSET_PlayerController = 0x30;
        private const int OFFSET_AcknowledgedPawn = 0x2a0;
        private const int OFFSET_PlayerStateOffset = 0x240;
        private const int OFFSET_RootComponent = 0x130;
        private const int OFFSET_RelativeLocation = 0x11C;
        private const int OFFSET_GameState = 0x120;
        private const int OFFSET_PlayerArray = 0x238;
        private const int OFFSET_PawnPrivate = 0x280;
        private const int OFFSET_TeamNum = 0x338;
        private const int OFFSET_ControlRotation = 0x288;
        private const int OFFSET_PlayerCameraManager = 0x2b8;
        private const int OFFSET_CameraCache = 0x290;

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private const int PROCESS_VM_READ = 0x0010;
        private const int PROCESS_QUERY_INFORMATION = 0x0400;

        private IntPtr processHandle;
        private IntPtr baseAddress;
        private int cachedLocalTeamId = -1;

        static void Main(string[] args)
        {
            var ext = new External_Main();
            ext.Run();
        }

        private void Run()
        {

            if (!AttachToProcess())
            {
                Console.WriteLine($"[-] Failed to attach to {PROCESS_NAME}.exe");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"[+] Process: {PROCESS_NAME}.exe");
            Console.WriteLine($"[+] Base Address: 0x{baseAddress:X}");
            Console.WriteLine();
            PrintOffsets();
            Console.WriteLine("\n[INFO] Press ESC to exit\n");
            Thread.Sleep(2000);

            try
            {
                while (true)
                {
                    UpdateAndDisplay();
                    Thread.Sleep(800);

                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                    {
                        break;
                    }
                }
            }
            finally
            {
                CloseHandle(processHandle);
                Console.WriteLine("\n[-] Closed.");
            }
        }

        private bool AttachToProcess()
        {
            var processes = Process.GetProcessesByName(PROCESS_NAME);
            if (processes.Length == 0)
                return false;

            var process = processes[0];
            processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, process.Id);
            
            if (processHandle == IntPtr.Zero)
                return false;

            baseAddress = process.MainModule?.BaseAddress ?? IntPtr.Zero;
            return baseAddress != IntPtr.Zero;
        }

        private void PrintOffsets()
        {
            Console.WriteLine("[+] Offsets:");
            Console.WriteLine($"[OFFSET] OFFSET_GNAMES              = 0x{OFFSET_GNAMES:X}");
            Console.WriteLine($"[OFFSET] OFFSET_GOBJECTS            = 0x{OFFSET_GOBJECTS:X}");
            Console.WriteLine($"[OFFSET] OFFSET_GWORLD              = 0x{OFFSET_GWORLD:X}");
            Console.WriteLine($"[OFFSET] OwningGameInstance         = 0x{OFFSET_OwningGameInstance:X}");
            Console.WriteLine($"[OFFSET] LocalPlayers               = 0x{OFFSET_LocalPlayers:X}");
            Console.WriteLine($"[OFFSET] PlayerController           = 0x{OFFSET_PlayerController:X}");
            Console.WriteLine($"[OFFSET] AcknowledgedPawn           = 0x{OFFSET_AcknowledgedPawn:X}");
            Console.WriteLine($"[OFFSET] PlayerStateOffset          = 0x{OFFSET_PlayerStateOffset:X}");
            Console.WriteLine($"[OFFSET] RootComponent              = 0x{OFFSET_RootComponent:X}");
            Console.WriteLine($"[OFFSET] RelativeLocation           = 0x{OFFSET_RelativeLocation:X}");
            Console.WriteLine($"[OFFSET] GameState                  = 0x{OFFSET_GameState:X}");
            Console.WriteLine($"[OFFSET] PlayerArray                = 0x{OFFSET_PlayerArray:X}");
            Console.WriteLine($"[OFFSET] PawnPrivate                = 0x{OFFSET_PawnPrivate:X}");
            Console.WriteLine($"[OFFSET] TeamNum                    = 0x{OFFSET_TeamNum:X}");
            Console.WriteLine($"[OFFSET] ControlRotation            = 0x{OFFSET_ControlRotation:X}");
            Console.WriteLine($"[OFFSET] PlayerCameraManager        = 0x{OFFSET_PlayerCameraManager:X}");
            Console.WriteLine($"[OFFSET] CameraCache                = 0x{OFFSET_CameraCache:X}");
        }

        private void UpdateAndDisplay()
        {
            try
            {
                long gworld = ReadInt64(baseAddress + OFFSET_GWORLD);
                if (gworld == 0) return;

                long gameInstance = ReadInt64(gworld + OFFSET_OwningGameInstance);
                if (gameInstance == 0) return;

                long localPlayers = ReadInt64(gameInstance + OFFSET_LocalPlayers);
                if (localPlayers == 0) return;

                long localPlayer = ReadInt64(localPlayers);
                if (localPlayer == 0) return;

                long playerController = ReadInt64(localPlayer + OFFSET_PlayerController);
                if (playerController == 0) return;

                long localPawn = ReadInt64(playerController + OFFSET_AcknowledgedPawn);
                if (localPawn == 0)
                {
                    // Reset cached team when pawn becomes 0 (lobby/respawn)
                    cachedLocalTeamId = -1;
                    return;
                }

                long localRoot = ReadInt64(localPawn + OFFSET_RootComponent);
                if (localRoot == 0) return;

                var localPos = ReadVector3(localRoot + OFFSET_RelativeLocation);

                // Only determine team if not already cached
                if (cachedLocalTeamId == -1)
                {
                    long gameState = ReadInt64(gworld + OFFSET_GameState);
                    if (gameState != 0)
                    {
                        long playerArrayAddr = gameState + OFFSET_PlayerArray;
                        long dataPtr = ReadInt64(playerArrayAddr);
                        int count = ReadInt32(playerArrayAddr + 0x8);

                        if (dataPtr != 0 && count > 0 && count <= 100)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                try
                                {
                                    long playerState = ReadInt64(dataPtr + i * 8);
                                    if (playerState == 0) continue;

                                    long pawn = ReadInt64(playerState + OFFSET_PawnPrivate);
                                    if (pawn == 0) continue;

                                    long root = ReadInt64(pawn + OFFSET_RootComponent);
                                    if (root == 0) continue;

                                    var pos = ReadVector3(root + OFFSET_RelativeLocation);

                                    if (Math.Abs(pos.X - localPos.X) < 1f &&
                                        Math.Abs(pos.Y - localPos.Y) < 1f &&
                                        Math.Abs(pos.Z - localPos.Z) < 1f)
                                    {
                                        byte teamId = ReadByte(playerState + OFFSET_TeamNum);
                                        if (teamId != 255)
                                        {
                                            cachedLocalTeamId = teamId;
                                            break;
                                        }
                                    }
                                }
                                catch { continue; }
                            }
                        }
                    }
                }

                Console.Clear();
                Console.WriteLine("═══════════════════════════════════════");
                Console.WriteLine("LOCAL PLAYER");
                Console.WriteLine($"  Position: X:{localPos.X,9:F1} Y:{localPos.Y,9:F1} Z:{localPos.Z,9:F1}");
                Console.WriteLine($"  Team: {(cachedLocalTeamId != -1 ? cachedLocalTeamId.ToString() : "Unknown")}");
                Console.WriteLine("═══════════════════════════════════════\n");

                long gameState2 = ReadInt64(gworld + OFFSET_GameState);
                if (gameState2 == 0) return;

                long playerArrayAddr2 = gameState2 + OFFSET_PlayerArray;
                long dataPtr2 = ReadInt64(playerArrayAddr2);
                int count2 = ReadInt32(playerArrayAddr2 + 0x8);

                if (dataPtr2 == 0 || count2 <= 0 || count2 > 100) return;

                int enemiesFound = 0;
                int teammatesFound = 0;
                
                for (int i = 0; i < count2; i++)
                {
                    try
                    {
                        long playerState = ReadInt64(dataPtr2 + i * 8);
                        if (playerState == 0) continue;

                        long pawn = ReadInt64(playerState + OFFSET_PawnPrivate);
                        if (pawn == 0 || pawn == localPawn) continue;

                        byte enemyTeamId = ReadByte(playerState + OFFSET_TeamNum);
                        bool isEnemy = (cachedLocalTeamId != -1 && enemyTeamId != 255 && enemyTeamId != cachedLocalTeamId);

                        long root = ReadInt64(pawn + OFFSET_RootComponent);
                        if (root == 0) continue;

                        var enemyPos = ReadVector3(root + OFFSET_RelativeLocation);

                        if (enemyPos.X == 0 && enemyPos.Y == 0 && enemyPos.Z == 0) continue;

                        float dist = Distance(localPos, enemyPos);

                        if (isEnemy)
                        {
                            ConsoleColor color = dist < 2000 ? ConsoleColor.Red :
                                               dist < 5000 ? ConsoleColor.Green :
                                               ConsoleColor.Yellow;

                            Console.ForegroundColor = color;
                            Console.WriteLine($"[ENEMY] Team {enemyTeamId} | Distance: {dist,6:F0} | X:{enemyPos.X,9:F1} Y:{enemyPos.Y,9:F1} Z:{enemyPos.Z,9:F1}");
                            Console.ResetColor();
                            enemiesFound++;
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"[ALLY]  Team {enemyTeamId} | Distance: {dist,6:F0} | X:{enemyPos.X,9:F1} Y:{enemyPos.Y,9:F1} Z:{enemyPos.Z,9:F1}");
                            Console.ResetColor();
                            teammatesFound++;
                        }
                    }
                    catch { continue; }
                }

                Console.WriteLine($"\n{enemiesFound} enemies | {teammatesFound} allies");
            }
            catch { }
        }

        private long ReadInt64(long address)
        {
            byte[] buffer = new byte[8];
            ReadProcessMemory(processHandle, (IntPtr)address, buffer, 8, out _);
            return BitConverter.ToInt64(buffer, 0);
        }

        private int ReadInt32(long address)
        {
            byte[] buffer = new byte[4];
            ReadProcessMemory(processHandle, (IntPtr)address, buffer, 4, out _);
            return BitConverter.ToInt32(buffer, 0);
        }

        private byte ReadByte(long address)
        {
            byte[] buffer = new byte[1];
            ReadProcessMemory(processHandle, (IntPtr)address, buffer, 1, out _);
            return buffer[0];
        }

        private float ReadFloat(long address)
        {
            byte[] buffer = new byte[4];
            ReadProcessMemory(processHandle, (IntPtr)address, buffer, 4, out _);
            return BitConverter.ToSingle(buffer, 0);
        }

        private Vector3 ReadVector3(long address)
        {
            return new Vector3
            {
                X = ReadFloat(address + 0x0),
                Y = ReadFloat(address + 0x4),
                Z = ReadFloat(address + 0x8)
            };
        }

        private float Distance(Vector3 a, Vector3 b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float dz = b.Z - a.Z;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private struct Vector3
        {
            public float X;
            public float Y;
            public float Z;
        }
    }
}