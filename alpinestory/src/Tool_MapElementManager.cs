using SkiaSharp;
using System;
using System.Linq;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
public class MapElementManager
{
    internal ICoreServerAPI api;
    internal int chunkX, chunkZ, min_height_custom, max_height_custom;
    internal UtilTool uTool;
    internal SKBitmap[] height_maps;
    internal int chunksize;
    public MapElementManager(ICoreServerAPI api, UtilTool uTool, int chunkX, int chunkZ, int min_height_custom, int max_height_custom, SKBitmap[] height_maps){
        this.api = api;
        this.uTool = uTool;
        this.height_maps = height_maps;

        this.chunkX = chunkX;
        this.chunkZ = chunkZ;
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
    public MapElement[] getLocalMapElements(int interMountainChunkCount){
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
    public (int[], int[]) generateHeightMap(MapElement[] mapElements, int interMountainChunkCount){
        int[] heightMap = new int[chunksize*chunksize];
        int[] elementMap = new int[chunksize*chunksize];

        int lowX = chunkX - uTool.mod(chunkX, interMountainChunkCount); 
        int highX = chunkX - uTool.mod(chunkX, interMountainChunkCount) + interMountainChunkCount; 
        int lowZ = chunkZ - uTool.mod(chunkZ, interMountainChunkCount); 
        int highZ = chunkZ - uTool.mod(chunkZ, interMountainChunkCount) + interMountainChunkCount; 

        float h00, h10, h01, h11;
        int worldx, worldz;

        for(int x = 0; x < chunksize; x++){
            for(int z = 0; z < chunksize; z++){
                int i = uTool.ChunkIndex2d(x, z, chunksize);
                worldx = chunkX*chunksize + x;
                worldz = chunkZ*chunksize + z;

                h00 = mapElements[0].getPosHeight(lowX*chunksize - worldx, lowZ*chunksize - worldz);
                h01 = mapElements[1].getPosHeight(highX*chunksize - worldx, lowZ*chunksize - worldz);
                h10 = mapElements[2].getPosHeight(lowX*chunksize - worldx, highZ*chunksize - worldz);
                h11 = mapElements[3].getPosHeight(highX*chunksize - worldx, highZ*chunksize - worldz);

                heightMap[i] = min_height_custom + (int)(new float[]{h00, h10, h01, h11}).Max();
                elementMap[uTool.ChunkIndex2d(x, z, chunksize)] = Array.IndexOf(new float[]{h00, h10, h01, h11}, heightMap[i]);
            }
        }

        return (heightMap, elementMap);
    }
    int getMaxInChunk(MapElement[] mapElements, int interMountainChunkCount, int X, int Z){
        int current_chunkX = chunkX;
        int current_chunkZ = chunkZ;

        int[] heightMap;
        int[] elementMap ;

        chunkX = X;
        chunkZ = Z;
        (heightMap, elementMap) = generateHeightMap(mapElements, interMountainChunkCount);

        chunkX = current_chunkX;
        chunkZ = current_chunkZ;

        return heightMap.Max();
    }
    public int[] generateRegionMap(int interMountainChunkCount, IntDataMap2D referenceMap, int chunkX, int chunkZ, int globalRegionSize, float ratio){
        MapElement[] mapElements;
        int regionSize = referenceMap.Size;
        int regionOffset = referenceMap.TopLeftPadding;
        
        int[] miniRegionMap = new int[regionSize*regionSize];

        for(int i = 0; i < miniRegionMap.Length; i++){
            int current_chunkX = chunkX;
            int current_chunkZ = chunkZ;

            int fakeChunkX = (int)(chunkX + (- uTool.mod(chunkX, globalRegionSize) - regionOffset + i%regionSize)*ratio);
            int fakeChunkZ = (int)(chunkZ + (- uTool.mod(chunkZ, globalRegionSize) - regionOffset + i/regionSize)*ratio);
            
            mapElements = getLocalMapElements(interMountainChunkCount);
            miniRegionMap[i] = getMaxInChunk(mapElements, interMountainChunkCount, fakeChunkX, fakeChunkZ);

            chunkX = current_chunkX;
            chunkZ = current_chunkZ;
        }
        // Array.Reverse(miniRegionMap);
        return miniRegionMap;
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