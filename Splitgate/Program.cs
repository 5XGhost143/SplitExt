using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Numerics;
using ImGuiNET;
using ClickableTransparentOverlay;
using System.Collections.Generic;

namespace SplitExt
{
    public struct PlayerInfo
    {
        public Vector3 Position;
        public bool IsEnemy;
        public byte TeamId;
        public float Distance;
        public float Health;
    }

    class External_Main : Overlay
    {
        private const string PROCESS_NAME = "PortalWars-Win64-Shipping";
        
        // Offsets
        private const long OFFSET_GWORLD = 0x589cb60;
        private const int OFFSET_OwningGameInstance = 0x180;
        private const int OFFSET_LocalPlayers = 0x38;
        private const int OFFSET_PlayerController = 0x30;
        private const int OFFSET_AcknowledgedPawn = 0x2a0;
        private const int OFFSET_RootComponent = 0x130;
        private const int OFFSET_RelativeLocation = 0x11C;
        private const int OFFSET_GameState = 0x120;
        private const int OFFSET_PlayerArray = 0x238;
        private const int OFFSET_PawnPrivate = 0x280;
        private const int OFFSET_TeamNum = 0x338;
        private const int OFFSET_Health = 0x4ec;
        private const int OFFSET_PlayerCameraManager = 0x2b8;
        private const int OFFSET_CameraCache = 0x290;
        
        private static int foundCameraOffset = -1;

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private IntPtr processHandle;
        private IntPtr baseAddress;
        private int cachedLocalTeamId = -1;
        private int screenWidth = 1920;
        private int screenHeight = 1080;

        private List<PlayerInfo> players = new List<PlayerInfo>();
        private Vector3 localPosition;
        private float localHealth = 0f;
        private Vector3 cameraRotation;
        private Vector3 cameraPosition;
        private float cameraFov = 90f;

        private bool showMenu = true;
        private bool insertKeyPressed = false;
        private bool enableTraceLines = true;
        private bool enableBoxes = false;

        public External_Main() : base()
        {
            if (!AttachToProcess())
            {
                Console.WriteLine("[-] Failed to attach to game.");
                Environment.Exit(1);
            }
            Console.Clear();
            Console.WriteLine("[+] Debug Console Active");
        }

        static void Main(string[] args) => new External_Main().Start().Wait();

        protected override void Render()
        {
            HandleInput();
            var io = ImGui.GetIO();
            screenWidth = (int)io.DisplaySize.X;
            screenHeight = (int)io.DisplaySize.Y;

            UpdateGameData();
            PrintConsoleDebug();

            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.SetNextWindowSize(new Vector2(screenWidth, screenHeight));
            ImGui.Begin("##Overlay", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs);
            
            var drawList = ImGui.GetWindowDrawList();
            
            Vector2 center = new Vector2(screenWidth / 2, screenHeight / 2);
            drawList.AddCircle(center, 3f, 0xFF00FF00, 12, 2f);

            if (enableTraceLines || enableBoxes)
            {
                foreach (var player in players)
                {
                    if (player.Health <= 0) continue;
                    
                    if (player.IsEnemy && WorldToScreen(player.Position, out Vector2 screenPos))
                    {
                        if (screenPos.X >= 0 && screenPos.X <= screenWidth && 
                            screenPos.Y >= 0 && screenPos.Y <= screenHeight)
                        {
                            if (enableTraceLines)
                            {
                                Vector2 bottomCenter = new Vector2(screenWidth / 2, screenHeight);
                                drawList.AddLine(bottomCenter, screenPos, 0xFF0000FF, 2.0f);
                            }

                            if (enableBoxes)
                            {
                                Vector3 headPos = player.Position;
                                headPos.Z += 70f;
                                
                                if (WorldToScreen(headPos, out Vector2 headScreen))
                                {
                                    float height = Math.Abs(headScreen.Y - screenPos.Y);
                                    float width = height * 0.4f;

                                    Vector2 topLeft = new Vector2(screenPos.X - width / 2, headScreen.Y);
                                    Vector2 bottomRight = new Vector2(screenPos.X + width / 2, screenPos.Y);

                                    drawList.AddRect(topLeft, bottomRight, 0xFF0000FF, 0f, ImDrawFlags.None, 1.5f);
                                    
                                    string distText = $"{player.Distance:F0}m";
                                    Vector2 textPos = new Vector2(screenPos.X - 15, topLeft.Y - 15);
                                    drawList.AddText(textPos, 0xFFFFFFFF, distText);
                                }
                            }
                        }
                    }
                }
            }
            ImGui.End();

            if (showMenu)
            {
                ImGui.Begin("SplitExt", ref showMenu, ImGuiWindowFlags.AlwaysAutoResize);
                ImGui.Text($"Camera FOV: {cameraFov:F1}");
                ImGui.Text($"Local Team: {cachedLocalTeamId}");
                ImGui.Text($"Local Health: {localHealth:F1}");
                ImGui.Separator();
                ImGui.Checkbox("Enemy Trace Lines", ref enableTraceLines);
                ImGui.Checkbox("Enemy Boxes", ref enableBoxes);
                ImGui.Separator();
                ImGui.Text("GitHub: github.com/5XGhost143/SplitExt");
                ImGui.End();
            }
        }

        private void PrintConsoleDebug()
        {
            if (DateTime.Now.Millisecond % 500 > 10) return; 

            Console.SetCursorPosition(0, 0);
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine($"LOCAL POS: X:{localPosition.X,8:F0} Y:{localPosition.Y,8:F0} Z:{localPosition.Z,8:F0}");
            Console.WriteLine($"CAMERA   : X:{cameraPosition.X,8:F0} Y:{cameraPosition.Y,8:F0} Z:{cameraPosition.Z,8:F0}");
            Console.WriteLine($"ROTATION : P:{cameraRotation.X,7:F1}° Y:{cameraRotation.Y,7:F1}° R:{cameraRotation.Z,7:F1}°");
            Console.WriteLine($"FOV      : {cameraFov:F1}° | Offset: {(foundCameraOffset == -1 ? "Scanning..." : $"0x{foundCameraOffset:X}")}");
            Console.WriteLine($"TEAM ID  : {(cachedLocalTeamId == -1 ? "Scanning..." : cachedLocalTeamId.ToString())}");
            Console.WriteLine("═══════════════════════════════════════");
            
            int enemies = 0;
            int teammates = 0;
            foreach (var p in players)
            {
                if (p.IsEnemy)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ENEMY] Dist: {p.Distance,5:F0}m | Team: {p.TeamId} | Pos: ({p.Position.X:F0}, {p.Position.Y:F0}, {p.Position.Z:F0})");
                    enemies++;
                }
                else if (p.TeamId == cachedLocalTeamId)
                {
                    teammates++;
                }
            }
            Console.ResetColor();
            Console.WriteLine($"\nTracking {enemies} enemies, {teammates} teammates        ");
            Console.WriteLine("                                        ");
        }

        private void UpdateGameData()
        {
            players.Clear();
            try
            {
                long gworld = ReadInt64((long)baseAddress + OFFSET_GWORLD);
                if (gworld == 0) return;

                long gameInstance = ReadInt64(gworld + OFFSET_OwningGameInstance);
                if (gameInstance == 0) return;

                long localPlayer = ReadInt64(ReadInt64(gameInstance + OFFSET_LocalPlayers));
                if (localPlayer == 0) return;

                long playerController = ReadInt64(localPlayer + OFFSET_PlayerController);
                if (playerController == 0) return;

                long localPawn = ReadInt64(playerController + OFFSET_AcknowledgedPawn);

                long camMgr = ReadInt64(playerController + OFFSET_PlayerCameraManager);
                if (camMgr != 0)
                {
                    bool foundValidCamera = false;
                    
                    if (foundCameraOffset == -1)
                    {
                        for (int offset = 0; offset < 0x2000; offset += 0x4)
                        {
                            Vector3 testPos = ReadVector3(camMgr + offset);
                            
                            if (!float.IsNaN(testPos.X) && !float.IsNaN(testPos.Y) && !float.IsNaN(testPos.Z))
                            {
                                float distToPlayer = Vector3.Distance(testPos, localPosition);
                                
                                if (distToPlayer < 200 && distToPlayer > 0.1f)
                                {
                                    Vector3 testRot = ReadVector3(camMgr + offset + 0xC);
                                    
                                    if (!float.IsNaN(testRot.X) && !float.IsNaN(testRot.Y) &&
                                        Math.Abs(testRot.X) < 90 && Math.Abs(testRot.Y) < 360)
                                    {
                                        for (int fovOff = 0; fovOff < 32; fovOff += 4)
                                        {
                                            float testFov = ReadFloat(camMgr + offset + 0xC + fovOff);
                                            
                                            if (testFov > 70 && testFov < 130)
                                            {
                                                cameraPosition = testPos;
                                                cameraRotation = testRot;
                                                cameraFov = testFov;
                                                foundCameraOffset = offset;
                                                foundValidCamera = true;
                                                Console.WriteLine($"[+] Found camera at offset 0x{offset:X}, rot at 0x{(offset + 0xC):X}, fov at 0x{(offset + 0xC + fovOff):X}");
                                                break;
                                            }
                                        }
                                        
                                        if (foundValidCamera) break;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        cameraPosition = ReadVector3(camMgr + foundCameraOffset);
                        cameraRotation = ReadVector3(camMgr + foundCameraOffset + 0xC);
                        cameraFov = ReadFloat(camMgr + foundCameraOffset + 0x18);
                        
                        if (!float.IsNaN(cameraPosition.X) && !float.IsNaN(cameraRotation.X) &&
                            cameraFov > 60 && cameraFov < 150 && Math.Abs(cameraRotation.X) < 90)
                        {
                            foundValidCamera = true;
                        }
                        else
                        {
                            foundCameraOffset = -1;
                            Console.WriteLine("[-] Camera offset became invalid, rescanning...");
                        }
                    }
                    
                    if (!foundValidCamera && localPawn != 0)
                    {
                        cameraPosition = localPosition;
                        cameraPosition.Z += 60f;
                        
                        Vector3 controlRot = ReadVector3(playerController + 0x290);
                        if (!float.IsNaN(controlRot.X) && Math.Abs(controlRot.X) < 90)
                        {
                            cameraRotation = controlRot;
                            cameraFov = 103f;
                        }
                        else
                        {
                            cameraRotation = Vector3.Zero;
                            cameraFov = 103f;
                        }
                    }
                }

                if (localPawn != 0)
                {
                    long root = ReadInt64(localPawn + OFFSET_RootComponent);
                    if (root != 0)
                    {
                        localPosition = ReadVector3(root + OFFSET_RelativeLocation);
                    }
                    
                    // local health idk
                    localHealth = ReadFloat(localPawn + OFFSET_Health);
                }

                long gameState = ReadInt64(gworld + OFFSET_GameState);
                if (gameState == 0) return;

                long playerArray = ReadInt64(gameState + OFFSET_PlayerArray);
                if (playerArray == 0) return;

                int count = ReadInt32(gameState + OFFSET_PlayerArray + 0x8);

                for (int i = 0; i < Math.Min(count, 64); i++)
                {
                    long playerState = ReadInt64(playerArray + (i * 8));
                    if (playerState == 0) continue;

                    long pawn = ReadInt64(playerState + OFFSET_PawnPrivate);
                    byte teamId = ReadByte(playerState + OFFSET_TeamNum);

                    if (pawn == localPawn) 
                    { 
                        cachedLocalTeamId = teamId; 
                        continue; 
                    }
                    
                    if (pawn == 0) continue;

                    long root = ReadInt64(pawn + OFFSET_RootComponent);
                    if (root == 0) continue;

                    // read the fucking health of the pawn
                    float health = ReadFloat(pawn + OFFSET_Health);
                    
                    // skip 
                    if (health <= 0) continue;

                    Vector3 pos = ReadVector3(root + OFFSET_RelativeLocation);
                    
                    // claude told me to add these checks
                    if (float.IsNaN(pos.X) || float.IsNaN(pos.Y) || float.IsNaN(pos.Z)) continue;
                    if (Math.Abs(pos.X) > 1000000 || Math.Abs(pos.Y) > 1000000 || Math.Abs(pos.Z) > 1000000) continue;

                    float dist = Vector3.Distance(localPosition, pos);

                    players.Add(new PlayerInfo {
                        Position = pos,
                        TeamId = teamId,
                        Distance = dist,
                        Health = health,
                        IsEnemy = (cachedLocalTeamId != -1 && teamId != 255 && teamId != cachedLocalTeamId)
                    });
                }
            }
            catch (Exception ex)
            {
                // claude said do it idk what this is actually...
            }
        }

        // world to fucking screen (I hate it)
        private bool WorldToScreen(Vector3 worldPos, out Vector2 screenPos)
        {
            screenPos = Vector2.Zero;
            
            try
            {
                const float UCONST_Pi = 3.1415926f;
                const float URotationToRadians = UCONST_Pi / 180.0f;
                
                Vector3 AxisX, AxisY, AxisZ;
                GetAxes(cameraRotation, out AxisX, out AxisY, out AxisZ);
                
                Vector3 Delta = new Vector3(
                    worldPos.X - cameraPosition.X,
                    worldPos.Y - cameraPosition.Y,
                    worldPos.Z - cameraPosition.Z
                );
                
                Vector3 Transformed;
                Transformed.X = Vector3.Dot(Delta, AxisY);
                Transformed.Y = Vector3.Dot(Delta, AxisZ);
                Transformed.Z = Vector3.Dot(Delta, AxisX);
                
                if (Transformed.Z < 1.0f)
                    Transformed.Z = 1.0f;
                
                float centerX = screenWidth / 2.0f;
                float centerY = screenHeight / 2.0f;
                
                screenPos.X = centerX + Transformed.X * (centerX / MathF.Tan(cameraFov * UCONST_Pi / 360.0f)) / Transformed.Z;
                screenPos.Y = centerY + -Transformed.Y * (centerX / MathF.Tan(cameraFov * UCONST_Pi / 360.0f)) / Transformed.Z;
                
                return Transformed.Z >= 1.0f;
            }
            catch
            {
                return false;
            }
        }
        
        private Vector3 RotationToVector(Vector3 rotation)
        {
            const float URotationToRadians = 3.1415926f / 180.0f;
            
            float fYaw = rotation.Y * URotationToRadians;
            float fPitch = rotation.X * URotationToRadians;
            float CosPitch = MathF.Cos(fPitch);
            
            return new Vector3(
                MathF.Cos(fYaw) * CosPitch,
                MathF.Sin(fYaw) * CosPitch,
                MathF.Sin(fPitch)
            );
        }
        
        private void GetAxes(Vector3 rotation, out Vector3 X, out Vector3 Y, out Vector3 Z)
        {
            X = RotationToVector(rotation);
            X = Vector3.Normalize(X);
            
            Vector3 R = rotation;
            R.Y += 89.8f;
            Vector3 R2 = R;
            R2.X = 0.0f;
            Y = RotationToVector(R2);
            Y = Vector3.Normalize(Y);
            Y.Z = 0.0f;
            
            R.Y -= 89.8f;
            R.X += 89.8f;
            Z = RotationToVector(R);
            Z = Vector3.Normalize(Z);
        }

        private void HandleInput()
        {
            if ((GetAsyncKeyState(0x2D) & 0x8000) != 0) // Insert key bru
            {
                if (!insertKeyPressed) 
                { 
                    showMenu = !showMenu; 
                    insertKeyPressed = true; 
                }
            }
            else insertKeyPressed = false;
        }

        private bool AttachToProcess()
        {
            var procs = Process.GetProcessesByName(PROCESS_NAME);
            if (procs.Length == 0) return false;
            processHandle = OpenProcess(0x0010 | 0x0400, false, procs[0].Id);
            if (processHandle == IntPtr.Zero) return false;
            baseAddress = procs[0].MainModule.BaseAddress;
            return true;
        }


        // some memory functions (copied from google bc im lazy)

        private long ReadInt64(long addr) 
        { 
            if (addr == 0) return 0;
            byte[] b = new byte[8]; 
            if (!ReadProcessMemory(processHandle, (IntPtr)addr, b, 8, out _)) return 0;
            return BitConverter.ToInt64(b, 0); 
        }

        private int ReadInt32(long addr) 
        { 
            if (addr == 0) return 0;
            byte[] b = new byte[4]; 
            if (!ReadProcessMemory(processHandle, (IntPtr)addr, b, 4, out _)) return 0;
            return BitConverter.ToInt32(b, 0); 
        }

        private float ReadFloat(long addr) 
        { 
            if (addr == 0) return 0f;
            byte[] b = new byte[4]; 
            if (!ReadProcessMemory(processHandle, (IntPtr)addr, b, 4, out _)) return 0f;
            return BitConverter.ToSingle(b, 0); 
        }

        private byte ReadByte(long addr) 
        { 
            if (addr == 0) return 0;
            byte[] b = new byte[1]; 
            if (!ReadProcessMemory(processHandle, (IntPtr)addr, b, 1, out _)) return 0;
            return b[0]; 
        }

        private Vector3 ReadVector3(long addr) 
        {
            if (addr == 0) return Vector3.Zero;
            return new Vector3(ReadFloat(addr), ReadFloat(addr + 4), ReadFloat(addr + 8));
        }
    }
}