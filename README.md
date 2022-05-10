# MeshToTiff

## description

This Rhinoceros V6/V7 plugin creates a grayscale 16 bit tiff file with the heightmap representation of the selected mesh using a single surface as reference for depth calculation. The projection is perpendicular to the surface. It use the libtiff library by BitMiracle: https://github.com/BitMiracle/libtiff.net

## usage

### MeshToTiff 
launch plugin with the MeshToTiff command

### Select surface
select the reference surface

### Select mesh
select the mesh to calculate

### image size U (pixels), Image size V (pixels)
set tiff image size in pixels

### Depth of black color 
set the depth of the black color (anything over this value will be truncated to black)

### Default depth
set the default depth (depth of the pixels where the mesh faces are not present)

### Use Zmin or Zmax?
if the projection creates multiple points with this option you can choose if you want to use the nearest or the furthest one for the grayscale level calculation.


![immagine](https://user-images.githubusercontent.com/75561495/167575513-010c5865-15ec-4b68-84eb-1783c1a73692.png)




![immagine](https://user-images.githubusercontent.com/75561495/167576300-1748d2fd-aafd-4ee5-b6a6-cdb078e81199.png)
