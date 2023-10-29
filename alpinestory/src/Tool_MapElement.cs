using SkiaSharp;
using System;
using Vintagestory.API.Server;
public class MapElement
{
    internal ICoreServerAPI api;
    internal int centerX, centerZ, halfSize;
    internal float rotation, pixelWidth;
    internal int[] mapData;
    internal UtilTool uTool;
    internal SKBitmap imageData;
    public MapElement(ICoreServerAPI api, UtilTool uTool, int centerX, int centerZ, float rotation, int[] mapData, int size, float pixelWidth, SKBitmap imageData){
        this.api = api;
        this.uTool = uTool;
        this.centerX = centerX;
        this.centerZ = centerZ;
        this.rotation = rotation;
        this.mapData = mapData;
        halfSize = size;
        this.pixelWidth = pixelWidth;
        this.imageData = imageData;
    }
    public float getPosHeight(int relativeX, int relativeY){
        double R = Math.Sqrt(relativeX*relativeX + relativeY*relativeY);
        double theta = Math.Atan2(relativeY, relativeX);

        int newX = (int)(R*Math.Cos(theta+rotation));
        int newY = (int)(R*Math.Sin(theta+rotation));

        if(Math.Abs(newX)*pixelWidth > halfSize || Math.Abs(newY)*pixelWidth > halfSize)
            return 0;

        return uTool.LerpPosHeight(newX+halfSize, newY+halfSize, 0, pixelWidth, imageData);
    }
}