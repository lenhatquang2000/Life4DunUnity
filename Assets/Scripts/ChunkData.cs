using UnityEngine;
using System;

[Serializable]
public class TileObject
{
    public string objectType; // "tree", "rock", "flower", etc.
    public string spriteName;
    public float x;
    public float y;
}

[Serializable]
public class ChunkData
{
    public int chunkX;
    public int chunkY;
    
    // Ground tiles
    public string[] tileSpriteNames;
    public int[] tileX;
    public int[] tileY;
    
    // Objects on top of ground
    public TileObject[] objects;
    
    public ChunkData(int x, int y)
    {
        chunkX = x;
        chunkY = y;
        tileSpriteNames = new string[0];
        tileX = new int[0];
        tileY = new int[0];
        objects = new TileObject[0];
    }
    
    public void AddTile(string spriteName, int x, int y)
    {
        Array.Resize(ref tileSpriteNames, tileSpriteNames.Length + 1);
        Array.Resize(ref tileX, tileX.Length + 1);
        Array.Resize(ref tileY, tileY.Length + 1);
        
        tileSpriteNames[tileSpriteNames.Length - 1] = spriteName;
        tileX[tileX.Length - 1] = x;
        tileY[tileY.Length - 1] = y;
    }
    
    public void AddObject(string objectType, string spriteName, float x, float y)
    {
        Array.Resize(ref objects, objects.Length + 1);
        
        objects[objects.Length - 1] = new TileObject
        {
            objectType = objectType,
            spriteName = spriteName,
            x = x,
            y = y
        };
    }
}
