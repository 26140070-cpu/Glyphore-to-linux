namespace Glyphore;

internal enum PresenceContext
{
    Scene,
    AsciiTitleStudio,
    MaskEditing
}

internal enum PresenceOperation
{
    None,
    Export
}

internal enum DiscordRpcOpcode
{
    Handshake = 0,
    Frame = 1,
    Close = 2,
    Ping = 3,
    Pong = 4
}

internal readonly record struct DiscordPresenceDescriptor(
    string Details,
    string State,
    string LargeImage,
    string LargeText);






