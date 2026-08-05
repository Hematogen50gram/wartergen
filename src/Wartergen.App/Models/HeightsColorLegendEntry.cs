using System.Windows.Media;

namespace Wartergen.App.Models;

// One reference row for the heights legend: a single grayscale swatch (unlike cliffs, heights
// have no bright/dark texture pairing) labeled with the raw groundHeight it decodes to.
public sealed record HeightsColorLegendEntry(string Label, byte Gray, int Height, Brush Brush, string Hex);
