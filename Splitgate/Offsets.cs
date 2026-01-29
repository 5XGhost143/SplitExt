namespace SplitExt
{
    public static class Offsets
    {
        public const string PROCESS_NAME = "PortalWars-Win64-Shipping";

        public const long GWorld                   = 0x589cb60;
        public const int  OwningGameInstance       = 0x180; // UGameInstance
        public const int  LocalPlayers             = 0x38; // TArray
        public const int  PlayerController         = 0x30; // APlayerController
        public const int  AcknowledgedPawn         = 0x2a0; // APawn
        public const int  RootComponent            = 0x130; // USceneComponent
        public const int  RelativeLocation         = 0x11C; // FVector
        public const int  GameState                = 0x120; // AGameStateBase
        public const int  PlayerArray              = 0x238; // TArray
        public const int  PawnPrivate              = 0x280; // APawn
        public const int  TeamNum                  = 0x338; // APortalWarsPlayerState
        public const int  Health                   = 0x4ec; // APortalWarsCharacter
        public const int  PlayerCameraManager      = 0x2b8; // APlayerCameraManager
        public const int  CameraCache              = 0x290; // FCameraCacheEntry
        public const int  CurrentAmmo              = 0x302; // AGun
        public const int  CurrentAmmoInClip              = 0x304; // AGun
        public const int  CurrentWeapon              = 0x800; // AGun
    }
}