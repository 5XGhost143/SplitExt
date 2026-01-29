using System;
using System.Numerics;
using ImGuiNET;
using System.Collections.Generic;

namespace SplitExt
{
    public class ESP
    {
        private int screenWidth;
        private int screenHeight;
        private Vector3 cameraPosition;
        private Vector3 cameraRotation;
        private float cameraFov;

        public bool EnableTraceLines { get; set; }
        public bool EnableBoxes { get; set; }

        public ESP(int width, int height)
        {
            screenWidth = width;
            screenHeight = height;
            EnableTraceLines = false;
            EnableBoxes = false;
        }

        public void UpdateScreenSize(int width, int height)
        {
            screenWidth = width;
            screenHeight = height;
        }

        public void UpdateCamera(Vector3 position, Vector3 rotation, float fov)
        {
            cameraPosition = position;
            cameraRotation = rotation;
            cameraFov = fov;
        }

        public void RenderESP(List<UPlayerInfo> players, float rainbowHue)
        {
            if (!EnableTraceLines && !EnableBoxes) return;

            var drawList = ImGui.GetWindowDrawList();

            foreach (var player in players)
            {
                if (player.Health <= 0) continue;

                if (player.IsEnemy && WorldToScreen(player.Position, out Vector2 screenPos))
                {
                    if (EnableTraceLines)
                    {
                        Vector2 bottomCenter = new Vector2(screenWidth / 2, screenHeight);
                        drawList.AddLine(bottomCenter, screenPos, 0xFF0000FF, 2.0f);
                    }

                    if (EnableBoxes && screenPos.X >= 0 && screenPos.X <= screenWidth &&
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

                            string hpText = $"{player.Health:F0} HP";
                            Vector2 textSize = ImGui.CalcTextSize(hpText);
                            Vector2 textPos = new Vector2(feetScreen.X - textSize.X / 2, topLeft.Y - 15);
                            
                            RenderRainbowTextAtPosition(hpText, textPos, rainbowHue, drawList);
                        }
                    }
                }
            }
        }

        private void RenderRainbowTextAtPosition(string text, Vector2 position, float startHue, ImDrawListPtr drawList)
        {
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
    }
}