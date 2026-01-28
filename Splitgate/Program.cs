using System;
using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using ClickableTransparentOverlay;
using System.Collections.Generic;

namespace SplitExt
{
    class Program : Overlay
    {
        private static int foundCameraOffset = -1;

        private int cachedLocalTeamId = -1;
        private int screenWidth = 1920;
        private int screenHeight = 1080;

        private List<UPlayerInfo> players = new List<UPlayerInfo>();
        private Vector3 localPosition;
        private float localHealth = 0f;
        private Vector3 cameraRotation;
        private Vector3 cameraPosition;
        private float cameraFov = 90f;

        private bool showMenu = true;
        private bool insertKeyPressed = false;
        private bool enableTraceLines = false;
        private bool enableBoxes = false;
        private bool enableAimbot = false;
        private bool rightMousePressed = false;
        private float rainbowHue = 0f;

        private float aimbotFOV = 150f;
        private float aimbotSmoothing = 5f;

        public Program() : base()
        {
            if (!Win32.AttachToProcess(Offsets.PROCESS_NAME, out _, out _))
            {
                Environment.Exit(1);
            }

            Console.WriteLine($"[OFFSET] GWorld: 0x{Offsets.GWorld:X}");
            Console.WriteLine($"[OFFSET] OwningGameInstance: 0x{Offsets.OwningGameInstance:X}");
            Console.WriteLine($"[OFFSET] LocalPlayers: 0x{Offsets.LocalPlayers:X}");
            Console.WriteLine($"[OFFSET] PlayerController: 0x{Offsets.PlayerController:X}");
            Console.WriteLine($"[OFFSET] AcknowledgedPawn: 0x{Offsets.AcknowledgedPawn:X}");
            Console.WriteLine($"[OFFSET] RootComponent: 0x{Offsets.RootComponent:X}");
            Console.WriteLine($"[OFFSET] RelativeLocation: 0x{Offsets.RelativeLocation:X}");
            Console.WriteLine($"[OFFSET] GameState: 0x{Offsets.GameState:X}");
            Console.WriteLine($"[OFFSET] PlayerArray: 0x{Offsets.PlayerArray:X}");
            Console.WriteLine($"[OFFSET] PawnPrivate: 0x{Offsets.PawnPrivate:X}");
            Console.WriteLine($"[OFFSET] TeamNum: 0x{Offsets.TeamNum:X}");
            Console.WriteLine($"[OFFSET] Health: 0x{Offsets.Health:X}");
            Console.WriteLine($"[OFFSET] PlayerCameraManager: 0x{Offsets.PlayerCameraManager:X}");
            Console.WriteLine($"[OFFSET] CameraCache: 0x{Offsets.CameraCache:X}");
        }

        static void Main(string[] args)
        {
            Console.WriteLine("[+] Loading Overlay");
            try
            {
                var app = new Program();
                Console.WriteLine("[+] Sucessfully Loaded Overlay");
                app.Start().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Failed to load Overlay: {ex.Message}");
                Environment.Exit(1);
            }
        }

        protected override void Render()
        {
            HandleInput();
            var io = ImGui.GetIO();
            screenWidth = (int)io.DisplaySize.X;
            screenHeight = (int)io.DisplaySize.Y;

            UpdateGameData();

            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.SetNextWindowSize(new Vector2(screenWidth, screenHeight));
            ImGui.Begin("Mozilla Firefox", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs);

            var drawList = ImGui.GetWindowDrawList();

            rainbowHue += 2.0f;
            if (rainbowHue > 360f) rainbowHue = 0f;

            string watermarkText = "SplitExt by GhOsT";
            Vector2 textSize = ImGui.CalcTextSize(watermarkText);
            float textX = screenWidth - textSize.X - 5f;
            float textY = 5f;
            RenderRainbowTextAtPosition(watermarkText, new Vector2(textX, textY), rainbowHue);

            if (enableTraceLines || enableBoxes)
            {
                foreach (var player in players)
                {
                    if (player.Health <= 0) continue;

                    if (player.IsEnemy && WorldToScreen(player.Position, out Vector2 screenPos))
                    {
                        if (enableTraceLines)
                        {
                            Vector2 bottomCenter = new Vector2(screenWidth / 2, screenHeight);
                            drawList.AddLine(bottomCenter, screenPos, 0xFF0000FF, 2.0f);
                        }

                        if (enableBoxes && screenPos.X >= 0 && screenPos.X <= screenWidth &&
                            screenPos.Y >= 0 && screenPos.Y <= screenHeight)
                        {
                            Vector3 headPos = player.Position;
                            headPos.Z += 95f;

                            Vector3 feetPos = player.Position;
                            feetPos.Z -= 10f;

                            if (WorldToScreen(headPos, out Vector2 headScreen) && WorldToScreen(feetPos, out Vector2 feetScreen))
                            {
                                float height = Math.Abs(headScreen.Y - feetScreen.Y);

                                float minHeight = 50f;
                                if (height < minHeight)
                                {
                                    height = minHeight;
                                }

                                float width = height * 0.4f;

                                float boxTop = headScreen.Y;
                                float boxBottom = feetScreen.Y;

                                if (Math.Abs(headScreen.Y - feetScreen.Y) < minHeight)
                                {
                                    float boxCenter = (headScreen.Y + feetScreen.Y) / 2;
                                    boxTop = boxCenter - height / 2;
                                    boxBottom = boxCenter + height / 2;
                                }

                                Vector2 topLeft = new Vector2(feetScreen.X - width / 2, boxTop);
                                Vector2 bottomRight = new Vector2(feetScreen.X + width / 2, boxBottom);

                                drawList.AddRect(topLeft, bottomRight, 0xFF0000FF, 0f, ImDrawFlags.None, 1.5f);

                                string distText = $"{player.Distance:F0}m";
                                Vector2 textPos = new Vector2(feetScreen.X - 15, topLeft.Y - 15);
                                drawList.AddText(textPos, 0xFFFFFFFF, distText);
                            }
                        }
                    }
                }
            }
            ImGui.End();

            if (showMenu)
            {
                var style = ImGui.GetStyle();

                float originalRounding = style.WindowRounding;
                float originalFrameRounding = style.FrameRounding;
                float originalTabRounding = style.TabRounding;
                Vector2 originalFramePadding = style.FramePadding;
                Vector2 originalItemSpacing = style.ItemSpacing;

                style.WindowRounding = 10f;
                style.FrameRounding = 6f;
                style.TabRounding = 6f;
                style.FramePadding = new Vector2(12f, 6f);
                style.ItemSpacing = new Vector2(12f, 8f);
                style.WindowPadding = new Vector2(15f, 15f);

                ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.12f, 0.12f, 0.15f, 0.95f));
                ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.15f, 0.15f, 0.20f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.20f, 0.20f, 0.28f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(0.18f, 0.18f, 0.25f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(0.28f, 0.28f, 0.40f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(0.25f, 0.25f, 0.35f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.18f, 0.18f, 0.25f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.25f, 0.25f, 0.35f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.30f, 0.30f, 0.42f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.25f, 0.35f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.28f, 0.35f, 0.48f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.35f, 0.42f, 0.58f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(0.45f, 0.68f, 0.95f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.SliderGrab, new Vector4(0.45f, 0.68f, 0.95f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, new Vector4(0.55f, 0.78f, 1.0f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.22f, 0.28f, 0.38f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.28f, 0.35f, 0.48f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.32f, 0.40f, 0.55f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.35f, 0.35f, 0.45f, 1.0f));

                ImGui.SetNextWindowSize(new Vector2(480, 0), ImGuiCond.FirstUseEver);
                ImGui.Begin("SplitExt", ref showMenu, ImGuiWindowFlags.NoCollapse);

                if (ImGui.BeginTabBar("MainTabs", ImGuiTabBarFlags.None))
                {
                    if (ImGui.BeginTabItem("Debug"))
                    {
                        ImGui.BulletText($"Local Team: {cachedLocalTeamId}");
                        ImGui.BulletText($"Local Health: {localHealth:F1}");
                        ImGui.BulletText($"Camera FOV: {cameraFov:F1}");
                        ImGui.BulletText($"Enemies Found: {players.Count}");

                        ImGui.Spacing();
                        ImGui.Separator();
                        ImGui.Spacing();
                        ImGui.Text("GitHub: ");
                        ImGui.SameLine();
                        RenderRainbowText("github.com/5XGhost143/SplitExt", rainbowHue);
                        ImGui.Spacing();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("ESP"))
                    {
                        ImGui.Checkbox("Enable ESP Boxes", ref enableBoxes);
                        ImGui.Checkbox("Enable ESP Trace Lines", ref enableTraceLines);

                        ImGui.Spacing();
                        ImGui.Separator();
                        ImGui.Spacing();
                        ImGui.Text("GitHub: ");
                        ImGui.SameLine();
                        RenderRainbowText("github.com/5XGhost143/SplitExt", rainbowHue);
                        ImGui.Spacing();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Aimbot"))
                    {
                        ImGui.Checkbox("Enable Aimbot", ref enableAimbot);

                        if (enableAimbot)
                        {
                            ImGui.Spacing();
                            ImGui.SliderFloat("FOV", ref aimbotFOV, 10f, 300f, "%.0f");
                            ImGui.SliderFloat("Smoothing", ref aimbotSmoothing, 1f, 20f, "%.1f");
                            ImGui.TextDisabled("(1 = instant, higher = smoother)");
                        }

                        ImGui.Spacing();
                        ImGui.Separator();
                        ImGui.Spacing();
                        ImGui.Text("GitHub: ");
                        ImGui.SameLine();
                        RenderRainbowText("github.com/5XGhost143/SplitExt", rainbowHue);
                        ImGui.Spacing();
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }

                ImGui.End();

                ImGui.PopStyleColor(19);
                style.WindowRounding = originalRounding;
                style.FrameRounding = originalFrameRounding;
                style.TabRounding = originalTabRounding;
                style.FramePadding = originalFramePadding;
                style.ItemSpacing = originalItemSpacing;
            }
        }

        private void UpdateGameData()
        {
            var procs = Process.GetProcessesByName(Offsets.PROCESS_NAME);
            if (procs.Length == 0)
            {
                Console.WriteLine($"[-] Process {Offsets.PROCESS_NAME}.exe stopped");
                Environment.Exit(0);
            }

            players.Clear();
            try
            {
                long gworld = Win32.ReadInt64((long)Win32.BaseAddress + Offsets.GWorld);
                if (gworld == 0) return;

                long gameInstance = Win32.ReadInt64(gworld + Offsets.OwningGameInstance);
                if (gameInstance == 0) return;

                long localPlayer = Win32.ReadInt64(Win32.ReadInt64(gameInstance + Offsets.LocalPlayers));
                if (localPlayer == 0) return;

                long playerController = Win32.ReadInt64(localPlayer + Offsets.PlayerController);
                if (playerController == 0) return;

                long localPawn = Win32.ReadInt64(playerController + Offsets.AcknowledgedPawn);

                long camMgr = Win32.ReadInt64(playerController + Offsets.PlayerCameraManager);
                if (camMgr != 0)
                {
                    bool foundValidCamera = false;

                    if (foundCameraOffset == -1 && !rightMousePressed)
                    {
                        for (int offset = 0; offset < 0x2000; offset += 0x4)
                        {
                            Vector3 testPos = Win32.ReadVector3(camMgr + offset);

                            if (!float.IsNaN(testPos.X) && !float.IsNaN(testPos.Y) && !float.IsNaN(testPos.Z))
                            {
                                float distToPlayer = Vector3.Distance(testPos, localPosition);

                                if (distToPlayer < 200 && distToPlayer > 0.1f)
                                {
                                    Vector3 testRot = Win32.ReadVector3(camMgr + offset + 0xC);

                                    if (!float.IsNaN(testRot.X) && !float.IsNaN(testRot.Y) &&
                                        Math.Abs(testRot.X) < 90 && Math.Abs(testRot.Y) < 360)
                                    {
                                        for (int fovOff = 0; fovOff < 32; fovOff += 4)
                                        {
                                            float testFov = Win32.ReadFloat(camMgr + offset + 0xC + fovOff);

                                            if (testFov > 70 && testFov < 130)
                                            {
                                                cameraPosition = testPos;
                                                cameraRotation = testRot;
                                                cameraFov = testFov;
                                                foundCameraOffset = offset;
                                                foundValidCamera = true;
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
                        cameraPosition = Win32.ReadVector3(camMgr + foundCameraOffset);
                        cameraRotation = Win32.ReadVector3(camMgr + foundCameraOffset + 0xC);
                        cameraFov = Win32.ReadFloat(camMgr + foundCameraOffset + 0x18);

                        if (!float.IsNaN(cameraPosition.X) && !float.IsNaN(cameraRotation.X) &&
                            cameraFov > 10 && cameraFov < 150 && Math.Abs(cameraRotation.X) < 90)
                        {
                            foundValidCamera = true;
                        }
                        else
                        {
                            if (!rightMousePressed)
                            {
                                foundCameraOffset = -1;
                            }
                        }
                    }

                    if (!foundValidCamera && localPawn != 0)
                    {
                        cameraPosition = localPosition;
                        cameraPosition.Z += 60f;

                        Vector3 controlRot = Win32.ReadVector3(playerController + 0x290);
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
                    long root = Win32.ReadInt64(localPawn + Offsets.RootComponent);
                    if (root != 0)
                    {
                        localPosition = Win32.ReadVector3(root + Offsets.RelativeLocation);
                    }

                    localHealth = Win32.ReadFloat(localPawn + Offsets.Health);
                }

                long gameState = Win32.ReadInt64(gworld + Offsets.GameState);
                if (gameState == 0) return;

                long playerArray = Win32.ReadInt64(gameState + Offsets.PlayerArray);
                if (playerArray == 0) return;

                int count = Win32.ReadInt32(gameState + Offsets.PlayerArray + 0x8);

                for (int i = 0; i < Math.Min(count, 64); i++)
                {
                    long playerState = Win32.ReadInt64(playerArray + (i * 8));
                    if (playerState == 0) continue;

                    long pawn = Win32.ReadInt64(playerState + Offsets.PawnPrivate);
                    byte teamId = Win32.ReadByte(playerState + Offsets.TeamNum);

                    if (pawn == localPawn)
                    {
                        if (cachedLocalTeamId == -1)
                        {
                            Console.WriteLine($"[OFFSET] Local Team ID: {teamId}");
                        }
                        cachedLocalTeamId = teamId;
                        continue;
                    }

                    if (pawn == 0) continue;

                    long root = Win32.ReadInt64(pawn + Offsets.RootComponent);
                    if (root == 0) continue;

                    float health = Win32.ReadFloat(pawn + Offsets.Health);

                    if (health <= 0) continue;

                    Vector3 pos = Win32.ReadVector3(root + Offsets.RelativeLocation);

                    if (float.IsNaN(pos.X) || float.IsNaN(pos.Y) || float.IsNaN(pos.Z)) continue;
                    if (Math.Abs(pos.X) > 1000000 || Math.Abs(pos.Y) > 1000000 || Math.Abs(pos.Z) > 1000000) continue;

                    float dist = Vector3.Distance(localPosition, pos);

                    players.Add(new UPlayerInfo
                    {
                        Position = pos,
                        TeamId = teamId,
                        Distance = dist,
                        Health = health,
                        IsEnemy = (cachedLocalTeamId == 255) ? true : (cachedLocalTeamId != -1 && teamId != 255 && teamId != cachedLocalTeamId)
                    });
                }
            }
            catch (Exception)
            {
            }
        }

        private bool WorldToScreen(Vector3 worldPos, out Vector2 screenPos)
        {
            screenPos = Vector2.Zero;

            try
            {
                const float UCONST_Pi = 3.1415926f;

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
            bool rightMouseCurrentlyPressed = (Win32.GetAsyncKeyState(0x02) & 0x8000) != 0;

            if (rightMouseCurrentlyPressed && enableAimbot)
            {
                AimAtNearestEnemy();
            }

            rightMousePressed = rightMouseCurrentlyPressed;

            if ((Win32.GetAsyncKeyState(0x2D) & 0x8000) != 0)
            {
                if (!insertKeyPressed)
                {
                    showMenu = !showMenu;
                    insertKeyPressed = true;
                }
            }
            else insertKeyPressed = false;
        }

        private void AimAtNearestEnemy()
        {
            if (players.Count == 0) return;

            Vector2 screenCenter = new Vector2(screenWidth / 2, screenHeight / 2);
            UPlayerInfo? closestPlayer = null;
            float closestDistance = float.MaxValue;
            Vector2 closestScreenPos = Vector2.Zero;

            foreach (var player in players)
            {
                if (!player.IsEnemy || player.Health <= 0) continue;

                Vector3 headPos = player.Position;
                headPos.Z += 80f;

                if (WorldToScreen(headPos, out Vector2 screenPos))
                {
                    if (screenPos.X < 0 || screenPos.X > screenWidth ||
                        screenPos.Y < 0 || screenPos.Y > screenHeight)
                        continue;

                    float distanceToCenter = Vector2.Distance(screenCenter, screenPos);

                    if (distanceToCenter < aimbotFOV && distanceToCenter < closestDistance)
                    {
                        closestDistance = distanceToCenter;
                        closestPlayer = player;
                        closestScreenPos = screenPos;
                    }
                }
            }

            if (closestPlayer.HasValue)
            {
                Vector2 delta = closestScreenPos - screenCenter;

                int moveX = (int)(delta.X / aimbotSmoothing);
                int moveY = (int)(delta.Y / aimbotSmoothing);

                if (Math.Abs(moveX) > 0 || Math.Abs(moveY) > 0)
                {
                    Win32.mouse_event(Win32.MOUSEEVENTF_MOVE, moveX, moveY, 0, 0);
                }
            }
        }

        private void RenderRainbowText(string text, float startHue)
        {
            var drawList = ImGui.GetWindowDrawList();
            Vector2 cursorPos = ImGui.GetCursorScreenPos();

            float charOffset = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                string character = text[i].ToString();
                Vector2 charSize = ImGui.CalcTextSize(character);

                float charHue = (startHue + (i * 15f)) % 360f;

                float h = charHue / 60f;
                int sector = (int)Math.Floor(h);
                float f = h - sector;

                float v = 1f;
                float s = 1f;

                float p = v * (1f - s);
                float q = v * (1f - s * f);
                float t = v * (1f - s * (1f - f));

                float r = 0, g = 0, b = 0;

                switch (sector % 6)
                {
                    case 0: r = v; g = t; b = p; break;
                    case 1: r = q; g = v; b = p; break;
                    case 2: r = p; g = v; b = t; break;
                    case 3: r = p; g = q; b = v; break;
                    case 4: r = t; g = p; b = v; break;
                    case 5: r = v; g = p; b = q; break;
                }

                uint color = 0xFF000000 |
                           ((uint)(b * 255) << 16) |
                           ((uint)(g * 255) << 8) |
                           (uint)(r * 255);

                drawList.AddText(new Vector2(cursorPos.X + charOffset, cursorPos.Y), color, character);
                charOffset += charSize.X;
            }

            ImGui.Dummy(new Vector2(charOffset, ImGui.GetTextLineHeight()));
        }

        private void RenderRainbowTextAtPosition(string text, Vector2 position, float startHue)
        {
            var drawList = ImGui.GetWindowDrawList();
            float charOffset = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                string character = text[i].ToString();
                Vector2 charSize = ImGui.CalcTextSize(character);

                float charHue = (startHue + (i * 15f)) % 360f;

                float h = charHue / 60f;
                int sector = (int)Math.Floor(h);
                float f = h - sector;

                float v = 1f;
                float s = 1f;

                float p = v * (1f - s);
                float q = v * (1f - s * f);
                float t = v * (1f - s * (1f - f));

                float r = 0, g = 0, b = 0;

                switch (sector % 6)
                {
                    case 0: r = v; g = t; b = p; break;
                    case 1: r = q; g = v; b = p; break;
                    case 2: r = p; g = v; b = t; break;
                    case 3: r = p; g = q; b = v; break;
                    case 4: r = t; g = p; b = v; break;
                    case 5: r = v; g = p; b = q; break;
                }

                uint color = 0xFF000000 |
                           ((uint)(b * 255) << 16) |
                           ((uint)(g * 255) << 8) |
                           (uint)(r * 255);

                drawList.AddText(new Vector2(position.X + charOffset, position.Y), color, character);
                charOffset += charSize.X;
            }
        }
    }
}