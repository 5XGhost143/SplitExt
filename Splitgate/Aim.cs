using System;
using System.Numerics;
using System.Collections.Generic;

namespace SplitExt
{
    public class Aim
    {
        private int screenWidth;
        private int screenHeight;
        private Vector3 cameraPosition;
        private Vector3 cameraRotation;
        private float cameraFov;

        public bool EnableAimbot { get; set; }
        public float AimbotFOV { get; set; }
        public float AimbotSmoothing { get; set; }

        public Aim(int width, int height)
        {
            screenWidth = width;
            screenHeight = height;
            EnableAimbot = false;
            AimbotFOV = 150f;
            AimbotSmoothing = 5f;
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

        public void AimAtNearestEnemy(List<UPlayerInfo> players)
        {
            if (players.Count == 0 || !EnableAimbot) return;

            Vector2 screenCenter = new Vector2(screenWidth / 2, screenHeight / 2);
            
            float closestDistance = float.MaxValue;
            
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

                    if (distanceToCenter < AimbotFOV && distanceToCenter < closestDistance)
                    {
                        closestDistance = distanceToCenter;
                    }
                }
            }

            if (closestDistance == float.MaxValue) return;

            UPlayerInfo? targetPlayer = null;
            float lowestHealth = float.MaxValue;
            Vector2 targetScreenPos = Vector2.Zero;

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

                    if (Math.Abs(distanceToCenter - closestDistance) < 0.1f)
                    {
                        if (player.Health < lowestHealth)
                        {
                            lowestHealth = player.Health;
                            targetPlayer = player;
                            targetScreenPos = screenPos;
                        }
                    }
                }
            }

            if (targetPlayer.HasValue)
            {
                Vector2 delta = targetScreenPos - screenCenter;

                int moveX = (int)(delta.X / AimbotSmoothing);
                int moveY = (int)(delta.Y / AimbotSmoothing);

                if (Math.Abs(moveX) > 0 || Math.Abs(moveY) > 0)
                {
                    Win32.mouse_event(Win32.MOUSEEVENTF_MOVE, moveX, moveY, 0, 0);
                }
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