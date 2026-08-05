using System.Windows.Media;

namespace Wartergen.App.Models;

// One reference row for the cliff-color legend: the two swatches (bright = cliffTexture 16,
// dark = cliffTexture 0) for a given layerHeight, plus their hex codes for typing directly into
// an image editor's color picker.
public sealed record CliffColorLegendEntry(int Height, Brush BrightBrush, Brush DarkBrush, string BrightHex, string DarkHex);
