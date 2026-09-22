namespace Glyphore;

internal sealed class GlyphoreActivityMessageFilter : IMessageFilter
{
    private readonly DiscordRichPresenceService _service;

    public GlyphoreActivityMessageFilter(DiscordRichPresenceService service) => _service = service;

    public bool PreFilterMessage(ref Message m)
    {
        
        
        switch (m.Msg)
        {
            case 0x0100: 
            case 0x0101: 
            case 0x0102: 
            case 0x0104: 
            case 0x0200: 
            case 0x0201: 
            case 0x0204: 
            case 0x0207: 
            case 0x020A: 
            case 0x020E: 
            case 0x00A0: 
            case 0x00A1: 
                _service.NotifyUserActivity();
                break;
        }
        return false;
    }
}
