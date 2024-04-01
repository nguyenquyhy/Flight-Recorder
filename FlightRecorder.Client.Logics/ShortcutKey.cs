using System.Text;

namespace FlightRecorder.Client.Logic;

public record ShortcutKey(
    bool Ctrl,
    bool Alt,
    bool Shift,
    string Key,
    uint VirtualKey
)
{
    public override string ToString()
    {
        var stringBuilder = new StringBuilder();
        if (Ctrl) stringBuilder.Append("Ctrl + ");
        if (Shift) stringBuilder.Append("Shift + ");
        if (Alt) stringBuilder.Append("Alt + ");
        stringBuilder.Append(Key);
        return stringBuilder.ToString();
    }
}
