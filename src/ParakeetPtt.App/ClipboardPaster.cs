using ParakeetPtt.Core;
using System.Runtime.InteropServices;

namespace ParakeetPtt.App;

internal sealed class ClipboardPaster : IClipboardPaster
{
    public async Task PasteAsync(string text, CancellationToken cancellationToken)
    {
        var previous = Clipboard.GetDataObject();
        try
        {
            Clipboard.SetText(text);
            SendKeys.SendWait("^v");
            await Task.Delay(750, cancellationToken);
        }
        finally
        {
            TryRestore(text, previous);
        }
    }

    private static void TryRestore(string pastedText, IDataObject? previous)
    {
        if (previous is null)
        {
            return;
        }

        try
        {
            if (Clipboard.ContainsText() && Clipboard.GetText() == pastedText)
            {
                Clipboard.SetDataObject(previous, copy: true);
            }
        }
        catch (ExternalException)
        {
        }
    }
}
