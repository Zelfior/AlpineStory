using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;
using SkiaSharp;
using Vintagestory.API.Util;

public class AlpineRiver: ModStdWorldGen
{
    ICoreServerAPI api;
    internal SKBitmap height_map;
    internal float data_width_per_pixel;        
    internal int max_height_custom;    
    internal int min_height_custom; 
    internal UtilTool uTool;
    public AlpineRiver(){}
    public AlpineRiver(ICoreServerAPI api, SKBitmap height_map, float data_width_per_pixel, int min_height_custom, UtilTool uTool)
    {
        LoadGlobalConfig(api);
        
        this.api = api;
        this.height_map = height_map;
        
        this.min_height_custom = min_height_custom;
        this.max_height_custom = api.WorldManager.MapSizeY;

        this.data_width_per_pixel = data_width_per_pixel;

        this.uTool = uTool;
    }
    public void OnChunkColumnGen(IChunkColumnGenerateRequest request)
    {   
        generate(request.Chunks, request.ChunkX, request.ChunkZ, request.RequiresChunkBorderSmoothing);
    }

    public override double ExecuteOrder()
    {
        return 1.5;
    }
    private void generate(IServerChunk[] chunks, int chunkX, int chunkZ, bool requiresChunkBorderSmoothing)
    { 
        //  We reiterate the river and lake making here, to remove the plants generated underwater by the vanilla worldgen.
        int muddyGravelID = api.World.GetBlock(new AssetLocation("muddygravel")).Id ;        
        int waterID = api.World.GetBlock(new AssetLocation("water-still-7")).Id ;    
        int graniteGravelID = api.World.GetBlock(new AssetLocation("gravel-granite")).Id ;

        //  Clean river beds
        makeRiverBed(chunks, chunkX, chunkZ, waterID, muddyGravelID, graniteGravelID);
    }
    bool riverNeightbour(int[] chunkRiverMap, int lX, int lZ, int mapSize){
        for(int i = -1; i < 2; i += 2){
            for(int j = -1; j < 2; j += 2){
                if (lX + i < 0 || lX + i >= mapSize || lZ + j < 0 || lZ + j >= mapSize)
                    break;
                
                if (chunkRiverMap[uTool.ChunkIndex2d(lX+i, lZ+j, mapSize)] == 1)
                    return true;
            }
        }
        return false;
    }
    private int riverDepth(int defaultWidth, int altitude){
        return (int)((float)defaultWidth * (float)(max_height_custom - altitude) / (float)(max_height_custom - min_height_custom)) + 1;
    }
    public void makeRiverBed(IServerChunk[] chunks, int chunkX, int chunkZ, int waterID, int gravelID, int graniteGravelID){
        int altitude;
        int localRiverHeight;
        
        int[] chunkHeightMap = SerializerUtil.Deserialize<int[]>(chunks[0].MapChunk.MapRegion.GetModdata("Alpine_HeightMap_"+chunkX.ToString()+"_"+chunkZ.ToString()));
        int[] chunkRiverMap = SerializerUtil.Deserialize<int[]>(chunks[0].MapChunk.MapRegion.GetModdata("Alpine_RiverMap_"+chunkX.ToString()+"_"+chunkZ.ToString()));
        int[] chunkRiverHeightMap = SerializerUtil.Deserialize<int[]>(chunks[0].MapChunk.MapRegion.GetModdata("Alpine_RiverHeightMap_"+chunkX.ToString()+"_"+chunkZ.ToString()));
        
        for (int colId = 0; colId < chunksize*chunksize; colId++){
            if(chunkRiverMap[colId] == 1){
                altitude = chunkHeightMap[colId] - 1;
                localRiverHeight = chunkRiverHeightMap[colId] - 1;

                //  Checking if we are not removing a tree
                if(uTool.getBlockId(colId%chunksize, altitude+1, colId/chunksize, chunksize, chunks) == 0 ||
                    uTool.getBlockId(colId%chunksize, altitude+1, colId/chunksize, chunksize, chunks) != 
                        uTool.getBlockId(colId%chunksize, altitude+6, colId/chunksize, chunksize, chunks)){

                    int localRiverDepth = riverDepth(2, chunkHeightMap[colId]);

                    for(int i=0; i< localRiverDepth; i++){
                        uTool.SetBlockAir(colId%chunksize, localRiverHeight - i, colId/chunksize, chunksize, chunks);
                        uTool.setBlockId(colId%chunksize, localRiverHeight - i, colId/chunksize, chunksize, chunks, waterID, fluid:true);
                    }

                    for(int i=1; i< 10; i++){
                        uTool.SetBlockAir(colId%chunksize, localRiverHeight + i, colId/chunksize, chunksize, chunks);
                    }

                    uTool.setBlockId(colId%chunksize, localRiverHeight-localRiverDepth, colId/chunksize, chunksize, chunks, gravelID);
                    uTool.setBlockId(colId%chunksize, localRiverHeight-localRiverDepth-1, colId/chunksize, chunksize, chunks, gravelID);
                }
            }
            else if(riverNeightbour(chunkRiverMap, colId%chunksize, colId/chunksize, chunksize)){
                altitude = chunkHeightMap[colId] - 1;
                uTool.setBlockId(colId%chunksize, altitude, colId/chunksize, chunksize, chunks, graniteGravelID);
                
                if(uTool.getBlockId(colId%chunksize, altitude+1, colId/chunksize, chunksize, chunks) != 0 &&
                    uTool.getBlockId(colId%chunksize, altitude+1, colId/chunksize, chunksize, chunks) != 
                        uTool.getBlockId(colId%chunksize, altitude+6, colId/chunksize, chunksize, chunks)){
                    uTool.SetBlockAir(colId%chunksize, altitude+1, colId/chunksize, chunksize, chunks);
                    
                }
            }
        }
    }    
}