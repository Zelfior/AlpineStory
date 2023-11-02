using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using System.Linq;

public class AlpineTerrain: ModStdWorldGen
{
    ICoreServerAPI api;
    int maxThreads;
    internal SKBitmap[] height_maps;
    internal float data_width_per_pixel;        
    internal int max_height_custom;
    internal int min_height_custom; 
    internal UtilTool uTool;
    public AlpineTerrain(){}
    public AlpineTerrain(ICoreServerAPI api, SKBitmap[] height_map, float data_width_per_pixel, int min_height_custom, UtilTool uTool)
    {
        LoadGlobalConfig(api);
        
        this.api = api;
        this.height_maps = height_map;

        //  The ColumnResult object will contain the data of the chunks to generate
        columnResults = new ColumnResult[chunksize * chunksize];
        for (int i = 0; i < chunksize * chunksize; i++) columnResults[i].ColumnBlockSolidities = new BitArray(api.WorldManager.MapSizeY);
        
        //  Initiating the number of threads to fasten the generation
        maxThreads = Math.Min(Environment.ProcessorCount, api.Server.Config.HostedMode ? 4 : 10);

        max_height_custom = api.WorldManager.MapSizeY;
        this.data_width_per_pixel = data_width_per_pixel;
        this.min_height_custom = min_height_custom;

        // Tools dedicated to this mod mainly to interpolate between pixels and pre-process the heightmap
        this.uTool = uTool;
    }
    public override double ExecuteOrder()
    {
        return 0.1;
    }
    ColumnResult[] columnResults;
    public void OnChunkColumnGen(IChunkColumnGenerateRequest request)
    {   
        generate(request.Chunks, request.ChunkX, request.ChunkZ, request.RequiresChunkBorderSmoothing);
        
    }
    
    private void generate(IServerChunk[] chunks, int chunkX, int chunkZ, bool requiresChunkBorderSmoothing)
    {
        int chunksize = this.chunksize;

        int rockID = api.World.GetBlock(new AssetLocation("rock-granite")).Id ;

        // // Store heightmap in the map chunk that can be used for ingame weather processing.
        ushort[] rainheightmap = chunks[0].MapChunk.RainHeightMap;
        ushort[] terrainheightmap = chunks[0].MapChunk.WorldGenTerrainHeightMap;

        //  Storing here the results for each X - Z coordinates (Y being the vertical) of the map pre-processing
        int[] chunkHeightMap;
        int[] chunkRiverMap = new int[chunksize*chunksize];
        int[] elementMap ;

        int interMountainChunkCount = 15;

        MapElementManager MEM = new MapElementManager(api, uTool, chunkX, chunkZ, min_height_custom, max_height_custom, height_maps);
        MapElement[] elements = MEM.getLocalMapElements(interMountainChunkCount, chunkX, chunkZ);
        (chunkHeightMap, elementMap) = MEM.generateHeightMap(elements, interMountainChunkCount, chunkX, chunkZ);


        for(int lX=0; lX < chunksize; lX++){
            for(int lZ=0; lZ < chunksize; lZ++){
                int[] neighbours = new int[4];

                if ((lX - 1 >= 0) && (lZ - 1 >= 0)){
                    neighbours[0] = elementMap[uTool.ChunkIndex2d(lX-1, lZ-1, chunksize)];
                }
                
                if ((lX - 1 >= 0) && (lZ + 1 < chunksize)){
                    neighbours[1] = elementMap[uTool.ChunkIndex2d(lX-1, lZ+1, chunksize)];
                }

                if ((lX + 1 < chunksize) && (lZ + 1 < chunksize)){
                    neighbours[2] = elementMap[uTool.ChunkIndex2d(lX+1, lZ+1, chunksize)];
                }

                if ((lX + 1 < chunksize) && (lZ - 1 >= 0)){
                    neighbours[3] = elementMap[uTool.ChunkIndex2d(lX+1, lZ-1, chunksize)];
                }

                for(int i=0; i<4; i++){
                    if(neighbours[i] == 0) neighbours[i] = elementMap[uTool.ChunkIndex2d(lX, lZ, chunksize)];
                }

                if (neighbours.Max() != neighbours.Min()){
                    chunkRiverMap[uTool.ChunkIndex2d(lX, lZ, chunksize)] = 1;
                }
            }
        }

        //  We find here all 2 high gap to increase the height there, it can prevent having 2 blocks wide steps, but is not necessary
        int[] to_increase = uTool.analyse_chunk(chunkHeightMap, chunkX, chunkZ, chunksize, min_height_custom, max_height_custom, data_width_per_pixel, 0);

        for (int lZ = 0; lZ < chunksize*chunksize; lZ++){
            if (to_increase[lZ] == 1){
                chunkHeightMap[lZ] += 1;
            }
            // if(chunkRiverMap[lZ] == 1)
            //     chunkHeightMap[lZ] -= 3;
        }
        
        //  For each X - Z coordinate of the chunk, storing the data in the column result. Multithreaded for faster process
        Parallel.For(0, chunksize * chunksize, new ParallelOptions() { MaxDegreeOfParallelism = maxThreads }, chunkIndex2d => {

            int current_thread = Thread.CurrentThread.ManagedThreadId;

            BitArray columnBlockSolidities = columnResults[chunkIndex2d].ColumnBlockSolidities;

            for (int posY = 1; posY < max_height_custom - 1; posY++)
            {
                //  The block solidity tells if the block will not be empty after the first pass.
                columnBlockSolidities[posY] = posY < chunkHeightMap[chunkIndex2d];
            }
        });

        //  Fills the chunk at height 0 of mantle blocks (indestructible block at the bottom of the map)
        chunks[0].Data.SetBlockBulk(0, chunksize, chunksize, GlobalConfig.mantleBlockId);

        /**
            Setting the blocks data here.

            The content of the chunks is stored in chunks[verticalChunkId].Data, which is an int array of size chunksize^3.

            The Id to provide can be given by the following function, "rock-granite" being the name of a block for example. 
                api.World.GetBlock(new AssetLocation("rock-granite")).Id ;
        */
        for (int posY = 1; posY < max_height_custom - 1; posY++)
        {
            for (int lZ = 0; lZ < chunksize; lZ++)
            {
                int worldZ = chunkZ * chunksize + lZ;
                for (int lX = 0; lX < chunksize; lX++)
                {
                    int worldX = chunkX * chunksize + lX;

                    int mapIndex = uTool.ChunkIndex2d(lX, lZ, chunksize);

                    ColumnResult columnResult = columnResults[mapIndex];
                    bool isSolid = columnResult.ColumnBlockSolidities[posY];

                    if (isSolid)
                    {
                        //  The rain maps help calculate where should it rain in the world
                        terrainheightmap[mapIndex] = (ushort)posY;
                        rainheightmap[mapIndex] = (ushort)posY;

                        //  A function of the UtilTool class sets the block
                        //  It is not as optimal as done in the vanilla worldgen, but more readable
                        uTool.setBlockId(lX, posY, lZ, chunksize, chunks, rockID);
                    }
                }
            }
        }

        /*
            Saving the height map for future uses
        */
        chunks[0].MapChunk.MapRegion.SetModdata("Alpine_HeightMap_"+chunkX.ToString()+"_"+chunkZ.ToString(), chunkHeightMap);
        chunks[0].MapChunk.MapRegion.SetModdata("Alpine_RiverMap_"+chunkX.ToString()+"_"+chunkZ.ToString(), chunkRiverMap);

        ushort ymax = 0;
        for (int i = 0; i < rainheightmap.Length; i++)
        {
            ymax = Math.Max(ymax, rainheightmap[i]);
        }

        chunks[0].MapChunk.YMax = ymax;
    }
}