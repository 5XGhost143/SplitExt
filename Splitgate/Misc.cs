using System.Threading;

namespace SplitExt
{
    public class Misc
    {
        private bool enableGod = false;
        public bool EnableGod => enableGod;
        
        private bool enableAmmo = false;
        public bool EnableAmmo => enableAmmo;

        private long currentLocalPawn = 0;
        private Thread godThread;
        private Thread ammoThread;
        private bool running = true;

        public Misc()
        {
            godThread = new Thread(GodLoop);
            godThread.IsBackground = true;
            godThread.Start();

            ammoThread = new Thread(AmmoLoop);
            ammoThread.IsBackground = true;
            ammoThread.Start();
        }

        public void SetLocalPawn(long localPawn)
        {
            currentLocalPawn = localPawn;
        }

        public void ToggleGod(bool enabled)  { enableGod = enabled; }
        public void ToggleAmmo(bool enabled) { enableAmmo = enabled; }

        private void GodLoop()
        {
            while (running)
            {
                if (enableGod && currentLocalPawn != 0)
                {
                    float currentHealth = Win32.ReadFloat(currentLocalPawn + Offsets.Health);
                    
                    if (currentHealth < 100f)
                    {
                        Win32.WriteFloat(currentLocalPawn + Offsets.Health, 100f);
                    }
                }

                Thread.Sleep(10);
            }
        }

        private void AmmoLoop()
        {
            while (running)
            {
                if (!enableAmmo || currentLocalPawn == 0)
                {
                    Thread.Sleep(50);
                    continue;
                }

                long weapon = Win32.ReadInt64(currentLocalPawn + Offsets.CurrentWeapon);
                if (weapon == 0)
                {
                    Thread.Sleep(50);
                    continue;
                }

                Win32.WriteInt32(weapon + Offsets.CurrentAmmo, 70);
                Win32.WriteInt32(weapon + Offsets.CurrentAmmoInClip, 30);

                Thread.Sleep(35);
            }
        }

        public void Stop()
        {
            running = false;
        }
    }
}