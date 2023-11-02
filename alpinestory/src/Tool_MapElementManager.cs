using SkiaSharp;
using System;
using System.Linq;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
public class MapElementManager
{
    internal ICoreServerAPI api;
    internal int min_height_custom, max_height_custom;
    internal UtilTool uTool;
    internal SKBitmap[] height_maps;
    internal int chunksize;
    public MapElementManager(ICoreServerAPI api, UtilTool uTool, int chunkX, int chunkZ, int min_height_custom, int max_height_custom, SKBitmap[] height_maps){
        this.api = api;
        this.uTool = uTool;
        this.height_maps = height_maps;

        this.min_height_custom = min_height_custom;
        this.max_height_custom = max_height_custom;

        chunksize = api.WorldManager.ChunkSize;
    }
    int getSeed(int x, int z){
        return x + (int)(z*z) + api.WorldManager.Seed;
    }
    MapElement elementFactory(int X, int Z, SKBitmap[] dataMaps){
        Random rand = new Random(getSeed(X, Z));

        return new MapElement(api, 
                                    uTool, 
                                    X*chunksize, 
                                    Z*chunksize, 
                                    (float)(rand.NextDouble() * Math.PI * 2),
                                    new int[0], 
                                    dataMaps[0].Width, 
                                    1, 
                                    rand.Next((int)(0.5*(max_height_custom - min_height_custom)), max_height_custom - min_height_custom), 
                                    dataMaps[rand.Next(dataMaps.Length - 1)]);
    }
    public MapElement[] getLocalMapElements(int interMountainChunkCount, int chunkX, int chunkZ){
        int lowX = chunkX - uTool.mod(chunkX, interMountainChunkCount); 
        int highX = chunkX - uTool.mod(chunkX, interMountainChunkCount) + interMountainChunkCount; 
        int lowZ = chunkZ - uTool.mod(chunkZ, interMountainChunkCount); 
        int highZ = chunkZ - uTool.mod(chunkZ, interMountainChunkCount) + interMountainChunkCount; 

        MapElement SWElement = elementFactory(lowX, lowZ, height_maps);
       
        MapElement SEElement = elementFactory(highX, lowZ, height_maps);

        MapElement NWElement = elementFactory(lowX, highZ, height_maps);

        MapElement NEElement = elementFactory(highX, highZ, height_maps);

        return new MapElement[4] {SWElement, SEElement, NWElement, NEElement};
    }
    (int, int) getHighestValue(MapElement[] mapElements, int interMountainChunkCount, int chunkX, int chunkZ, int x, int z, int chunkS){
        int lowX = chunkX - uTool.mod(chunkX, interMountainChunkCount); 
        int highX = chunkX - uTool.mod(chunkX, interMountainChunkCount) + interMountainChunkCount; 
        int lowZ = chunkZ - uTool.mod(chunkZ, interMountainChunkCount); 
        int highZ = chunkZ - uTool.mod(chunkZ, interMountainChunkCount) + interMountainChunkCount; 

        int worldx = chunkX*chunkS + x;
        int worldz = chunkZ*chunkS + z;

        float h00 = mapElements[0].getPosHeight(lowX*chunkS - worldx, lowZ*chunkS - worldz);
        float h01 = mapElements[1].getPosHeight(highX*chunkS - worldx, lowZ*chunkS - worldz);
        float h10 = mapElements[2].getPosHeight(lowX*chunkS - worldx, highZ*chunkS - worldz);
        float h11 = mapElements[3].getPosHeight(highX*chunkS - worldx, highZ*chunkS - worldz);

        float maxVal = (new float[]{h00, h10, h01, h11}).Max();
        int mountainIndicator = 0;

        if (maxVal == h00)
            mountainIndicator=lowX+ 10*lowZ;
        else if (maxVal == h10)
            mountainIndicator=highX+ 10*lowZ;
        else if (maxVal == h01)
            mountainIndicator=lowX+ 10*highZ;
        else if (maxVal == h11)
            mountainIndicator=highX+ 10*highZ;
        
        return (min_height_custom + (int)maxVal, mountainIndicator);
    }
    public (int[], int[]) generateHeightMap(MapElement[] mapElements, int interMountainChunkCount, int chunkX, int chunkZ){
        int[] heightMap = new int[chunksize*chunksize];
        int[] elementMap = new int[chunksize*chunksize];

        for(int x = 0; x < chunksize; x++){
            for(int z = 0; z < chunksize; z++){
                int i = uTool.ChunkIndex2d(x, z, chunksize);

                (heightMap[i], elementMap[i]) = getHighestValue(mapElements, interMountainChunkCount, chunkX, chunkZ, x, z, chunksize);
            }
        }

        return (heightMap, elementMap);
    }
    public IntDataMap2D generateRegionMap(int interMountainChunkCount, IntDataMap2D referenceMap, int chunkX, int chunkZ, int globalRegionSize, float ratio){
        MapElement[] mapElements;
        int regionSize = referenceMap.Size;
        int innerRegionSize = referenceMap.InnerSize;
        int regionOffset = referenceMap.TopLeftPadding;

        float h00, h10, h01, h11;
        int fakeChunkX, fakeChunkZ, lowX, lowZ, highX, highZ;

        for(int i = 0; i < regionSize; i++){
            for(int j = 0; j < regionSize; j++){
                fakeChunkX = (int)(chunkX - uTool.mod(chunkX, innerRegionSize) - regionOffset*ratio + i*ratio);
                fakeChunkZ = (int)(chunkZ - uTool.mod(chunkZ, innerRegionSize) - regionOffset*ratio + j*ratio);

                lowX = fakeChunkX - uTool.mod(fakeChunkX, interMountainChunkCount); 
                highX = fakeChunkX - uTool.mod(fakeChunkX, interMountainChunkCount) + interMountainChunkCount; 
                lowZ = fakeChunkZ - uTool.mod(fakeChunkZ, interMountainChunkCount); 
                highZ = fakeChunkZ - uTool.mod(fakeChunkZ, interMountainChunkCount) + interMountainChunkCount; 

                mapElements = getLocalMapElements(interMountainChunkCount, fakeChunkX, fakeChunkZ);

                h00 = mapElements[0].getPosHeight(lowX - fakeChunkX, lowZ - fakeChunkZ);
                h01 = mapElements[1].getPosHeight(highX - fakeChunkX, lowZ - fakeChunkZ);
                h10 = mapElements[2].getPosHeight(lowX - fakeChunkX, highZ - fakeChunkZ);
                h11 = mapElements[3].getPosHeight(highX - fakeChunkX, highZ - fakeChunkZ);

                referenceMap.SetInt(i, j, min_height_custom + (int)(new float[]{h00, h10, h01, h11}).Max());
            }
        }
        // Array.Reverse(referenceMap.Data);
        return referenceMap;
    }
    public int[] generateRegionMap_2(MapElement[] mapElements, int interMountainChunkCount, IntDataMap2D referenceMap, int chunkX, int chunkZ){
        int regionSize = referenceMap.Size;
        
        int[] miniRegionMap = new int[regionSize*regionSize];

        int lowX = chunkX - uTool.mod(chunkX, interMountainChunkCount); 
        int highX = chunkX - uTool.mod(chunkX, interMountainChunkCount) + interMountainChunkCount; 
        int lowZ = chunkZ - uTool.mod(chunkZ, interMountainChunkCount); 
        int highZ = chunkZ - uTool.mod(chunkZ, interMountainChunkCount) + interMountainChunkCount; 

        float h00, h10, h01, h11;

        for(int x = 0; x < regionSize; x++){
            for(int z = 0; z < regionSize; z++){
                int i = uTool.ChunkIndex2d(x, z, regionSize);

                h00 = mapElements[0].getPosHeight(lowX - chunkX, lowZ - chunkZ);
                h01 = mapElements[1].getPosHeight(highX - chunkX, lowZ - chunkZ);
                h10 = mapElements[2].getPosHeight(lowX - chunkX, highZ - chunkZ);
                h11 = mapElements[3].getPosHeight(highX - chunkX, highZ - chunkZ);

                miniRegionMap[i] = min_height_custom + (int)(new float[]{h00, h10, h01, h11}).Max();
            }
        }
        Array.Reverse(miniRegionMap);
        return miniRegionMap;
    }
}