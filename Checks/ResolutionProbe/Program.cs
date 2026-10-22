using Client.Resolution;
using System.Text.Json;

// The monitor may expose no compatible fullscreen modes (e.g. remote sessions).
bool fullscreenAvailable = DisplayResolutions.GetDisplayResolutions();
var fullscreenModes = DisplayResolutions.DisplaySupportedResolutions.ToArray();
if (!DisplayResolutions.GetDisplayResolutions(windowed: true) ||
    !DisplayResolutions.DisplaySupportedResolutions.Contains(eSupportedResolution.w1024h768))
    throw new Exception("Windowed 1024x768 must remain available independently of fullscreen modes.");
var first = DisplayResolutions.DisplaySupportedResolutions.ToArray();
DisplayResolutions.GetDisplayResolutions(windowed: true);
if (!first.SequenceEqual(DisplayResolutions.DisplaySupportedResolutions))
    throw new Exception("Repeated enumeration changed or duplicated the window sizes.");
if (!DisplayResolutions.IsSupported(1024) || DisplayResolutions.IsSupported(1234))
    throw new Exception("Configured resolution validation failed.");
Console.WriteLine(JsonSerializer.Serialize(new
{
    passed = true,
    fullscreenAvailable,
    fullscreenModes = fullscreenModes.Select(r => r.ToString()),
    windowedModes = first.Select(r => r.ToString())
}));
