namespace Wartergen.Wc3Terrain;

public static class TerrainTranslator
{
    public static byte[] JsonToWar(TerrainModel terrain)
    {
        var writer = new LittleEndianWriter();

        /*
         * Header
         */
        writer.AddChars("W3E!"); // file id
        writer.AddInt(12); // file version

        writer.AddChar(terrain.Tileset[0]); // base tileset
        writer.AddInt(terrain.CustomTileset ? 1 : 0); // 1 = using custom tileset, 0 = not

        /*
         * Tiles
         */
        writer.AddInt(terrain.TilePalette.Length);
        foreach (string tile in terrain.TilePalette)
        {
            writer.AddChars(tile);
        }

        /*
         * Cliffs
         */
        writer.AddInt(terrain.CliffTilePalette.Length);
        foreach (string cliffTile in terrain.CliffTilePalette)
        {
            writer.AddChars(cliffTile);
        }

        /*
         * Map size data
         */
        writer.AddInt(terrain.Map.Width + 1);
        writer.AddInt(terrain.Map.Height + 1);

        /*
         * Map offset
         */
        writer.AddFloat(terrain.Map.Offset.X);
        writer.AddFloat(terrain.Map.Offset.Y);

        /*
         * Tile points
         */
        int rowSize = terrain.Map.Width + 1;

        // Partition the terrain masks into "chunks" (i.e. rows) of (width+1) length,
        // reverse that list of rows (due to vertical flipping), and then write the rows out
        var groundHeightRows = ChunkArray(terrain.GroundHeight, rowSize);
        var waterHeightRows = ChunkArray(terrain.WaterHeight, rowSize);
        var boundaryFlagRows = ChunkArray(terrain.BoundaryFlag, rowSize);
        var flagsRows = ChunkArray(terrain.Flags, rowSize);
        var groundTextureRows = ChunkArray(terrain.GroundTexture, rowSize);
        var groundVariationRows = ChunkArray(terrain.GroundVariation, rowSize);
        var cliffVariationRows = ChunkArray(terrain.CliffVariation, rowSize);
        var cliffTextureRows = ChunkArray(terrain.CliffTexture, rowSize);
        var layerHeightRows = ChunkArray(terrain.LayerHeight, rowSize);

        groundHeightRows.Reverse();
        waterHeightRows.Reverse();
        boundaryFlagRows.Reverse();
        flagsRows.Reverse();
        groundTextureRows.Reverse();
        groundVariationRows.Reverse();
        cliffVariationRows.Reverse();
        cliffTextureRows.Reverse();
        layerHeightRows.Reverse();

        for (int i = 0; i < groundHeightRows.Count; i++)
        {
            for (int j = 0; j < groundHeightRows[i].Length; j++)
            {
                int groundHeight = groundHeightRows[i][j];
                int waterHeight = waterHeightRows[i][j];
                bool boundaryFlag = boundaryFlagRows[i][j];
                int flags = flagsRows[i][j];
                int groundTexture = groundTextureRows[i][j];
                int groundVariation = groundVariationRows[i][j];
                int cliffVariation = cliffVariationRows[i][j];
                int cliffTexture = cliffTextureRows[i][j];
                int layerHeight = layerHeightRows[i][j];

                int hasBoundaryFlag = boundaryFlag ? 0x4000 : 0;

                writer.AddShort(groundHeight);
                writer.AddShort(waterHeight | hasBoundaryFlag);
                writer.AddShort(flags | groundTexture);
                writer.AddByte(groundVariation | cliffVariation);
                writer.AddByte(cliffTexture | layerHeight);
            }
        }

        return writer.GetBuffer();
    }

    public static TerrainModel WarToJson(byte[] buffer)
    {
        var reader = new LittleEndianReader(buffer);
        var result = new TerrainModel();

        /*
         * Header
         */
        reader.ReadChars(4); // w3eHeader: W3E!
        TerrainVersionMismatchException.ExpectVersion(12, reader.ReadInt()); // version: 0C 00 00 00

        string tileset = reader.ReadChars(1);
        bool customTileset = reader.ReadInt() == 1;

        result.Tileset = tileset;
        result.CustomTileset = customTileset;

        /*
         * Tiles
         */
        int numTilePalettes = reader.ReadInt();
        var tilePalettes = new string[numTilePalettes];
        for (int i = 0; i < numTilePalettes; i++)
        {
            tilePalettes[i] = reader.ReadChars(4);
        }
        result.TilePalette = tilePalettes;

        /*
         * Cliffs
         */
        int numCliffTilePalettes = reader.ReadInt();
        var cliffPalettes = new string[numCliffTilePalettes];
        for (int i = 0; i < numCliffTilePalettes; i++)
        {
            cliffPalettes[i] = reader.ReadChars(4);
        }
        result.CliffTilePalette = cliffPalettes;

        /*
         * Map dimensions
         */
        int width = reader.ReadInt() - 1;
        int height = reader.ReadInt() - 1;
        float offsetX = reader.ReadFloat();
        float offsetY = reader.ReadFloat();
        result.Map = new TerrainMap { Width = width, Height = height, Offset = new TerrainOffset { X = offsetX, Y = offsetY } };

        /*
         * Map tiles
         */
        var arrGroundHeight = new List<int>();
        var arrWaterHeight = new List<int>();
        var arrBoundaryFlag = new List<bool>();
        var arrFlags = new List<int>();
        var arrGroundTexture = new List<int>();
        var arrGroundVariation = new List<int>();
        var arrCliffVariation = new List<int>();
        var arrCliffTexture = new List<int>();
        var arrLayerHeight = new List<int>();

        while (!reader.IsExhausted())
        {
            int groundHeight = reader.ReadShort();

            int waterHeightAndBoundary = reader.ReadShort();
            int waterHeight = waterHeightAndBoundary & 32767;
            bool boundaryFlag = (waterHeightAndBoundary & 0x4000) == 0x4000;

            int flagsAndGroundTexture = reader.ReadShort();
            int flags = flagsAndGroundTexture & 0b1111_1111_1100_0000; // upper 10 bits
            int groundTexture = flagsAndGroundTexture & 0b0000_0000_0011_1111; // lower 6 bits

            int groundAndCliffVariation = reader.ReadByte();
            int groundVariation = groundAndCliffVariation & 0b11111000; // upper 5 bits
            int cliffVariation = groundAndCliffVariation & 0b00000111; // lower 3 bits

            int cliffTextureAndLayerHeight = reader.ReadByte();
            int cliffTexture = cliffTextureAndLayerHeight & 0b11110000; // upper 4 bits
            int layerHeight = cliffTextureAndLayerHeight & 0b00001111; // lower 4 bits

            arrGroundHeight.Add(groundHeight);
            arrWaterHeight.Add(waterHeight);
            arrBoundaryFlag.Add(boundaryFlag);
            arrFlags.Add(flags);
            arrGroundTexture.Add(groundTexture);
            arrGroundVariation.Add(groundVariation);
            arrCliffVariation.Add(cliffVariation);
            arrCliffTexture.Add(cliffTexture);
            arrLayerHeight.Add(layerHeight);
        }

        // The map was read in "backwards" because wc3 maps have origin (0,0)
        // at the bottom left instead of top left as we desire. Flip the rows
        // vertically to fix this.
        int rowSize = result.Map.Width + 1;

        result.GroundHeight = FlattenReversed(arrGroundHeight, rowSize);
        result.WaterHeight = FlattenReversed(arrWaterHeight, rowSize);
        result.BoundaryFlag = FlattenReversed(arrBoundaryFlag, rowSize);
        result.Flags = FlattenReversed(arrFlags, rowSize);
        result.GroundTexture = FlattenReversed(arrGroundTexture, rowSize);
        result.GroundVariation = FlattenReversed(arrGroundVariation, rowSize);
        result.CliffVariation = FlattenReversed(arrCliffVariation, rowSize);
        result.CliffTexture = FlattenReversed(arrCliffTexture, rowSize);
        result.LayerHeight = FlattenReversed(arrLayerHeight, rowSize);

        return result;
    }

    private static List<T[]> ChunkArray<T>(IReadOnlyList<T> array, int size)
    {
        var rows = new List<T[]>();
        for (int i = 0; i < array.Count; i += size)
        {
            int count = Math.Min(size, array.Count - i);
            var row = new T[count];
            for (int j = 0; j < count; j++)
            {
                row[j] = array[i + j];
            }
            rows.Add(row);
        }
        return rows;
    }

    private static T[] FlattenReversed<T>(IReadOnlyList<T> array, int rowSize)
    {
        var rows = ChunkArray(array, rowSize);
        rows.Reverse();

        var flat = new T[array.Count];
        int index = 0;
        foreach (T[] row in rows)
        {
            foreach (T item in row)
            {
                flat[index++] = item;
            }
        }
        return flat;
    }
}
