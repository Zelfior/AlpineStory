from os import path
from typing import Dict

import numpy as np
from PIL import Image

from gmalthgtparser import HgtParser

"""

    World height data can be found at that path:

    http://viewfinderpanoramas.org/dem3.html#andes

    You can access the altitude in meter for a given coordinate through the function get_coord_height. 
    In that function, x and y need to be given in latitude and longitude coordinates, for example Paris would be at :
        48° 51' 24'', 2° 21' 07'' -> (48.85, 2.35)

    The missing data will be interpolated based on their neighbor.

"""

data_folder = "data_folder_path"

#   How many pixels in width and height. The output file will have resolution*resolution pixels.
#   Start at a low resolution (~ 100 200), and increase when you are sure of the frame you want to map.
resolution = 100   

##  Mapped frame boundaries
min_x = 46.5    #   Minimum longitude
min_y = 10      #   Maximum longitude
max_x = 47      #   Minimum latitude
max_y = 10.5    #   Maximum latitude


"""

        Code starting here

"""


def get_coord_height(x: float, y: float, datas: Dict[str, HgtParser]):    
    x_ = int(x)
    y_ = int(y)

    with datas[str(x_)+"_"+str(y_)] as parser:
        return parser.get_elevation((x, y))[2]

def nan_helper(y: np.ndarray):
    return np.isnan(y), lambda z: z.nonzero()[0]

data = {}
for x_ in range(int(min_x), int(max_x) + 1):
    for y_ in range(int(min_y), int(max_y) + 1):
        if not path.isfile(path.join(data_folder, 'N'+"{0:0=2d}".format(x_)+'E'+"{0:0=3d}".format(y_)+'.hgt')):
            raise ValueError(f"The data for coordinates ({x_}, {y_}) are missing in the provided folder. We expect to find the file : "
                                +'N'+"{0:0=2d}".format(x_)+'E'+"{0:0=3d}".format(y_)+'.hgt')

        data[str(x_)+"_"+str(y_)] = HgtParser(path.join(data_folder, 'N'+"{0:0=2d}".format(x_)+'E'+"{0:0=3d}".format(y_)+'.hgt'))

elevations = np.zeros([resolution, resolution])

print("Spatial resolution :", str((max_x-min_x)/resolution*110000), "m")

for i in range(resolution):
    for j in range(resolution):
        elevations[i,j] = get_coord_height(i/resolution*(max_x-min_x)+min_x, j/resolution*(max_y-min_y)+min_y, data)

data_min = np.min(elevations)
data_max = np.max(elevations)

elevations = (elevations-data_min)/(data_max-data_min) 

image = elevations.flatten()
nans, x= nan_helper(image)
image[nans] = np.interp(x(nans), x(~nans), image[~nans])
image = np.reshape(image, elevations.shape)

im = Image.fromarray(image*255)
im = im.convert('RGB')
im.save("HeightMap.png")
