using System;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.DocObjects; 
using Rhino.Geometry.Intersect;
using Rhino.UI;
using BitMiracle.LibTiff.Classic;


namespace MeshToTiff
{
    public class MeshToTiffCommand : Command
    {
        public MeshToTiffCommand()
        {
            Instance = this;
        }

        /// <summary>
        /// The only instance of this command.
        /// </summary>
        public static MeshToTiffCommand Instance
        {
            get; private set;
        }

        /// <returns>
        /// The command name as it appears on the Rhino command line.
        /// </returns>
        public override string EnglishName
        {
            get { return "MeshToTiff"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {



            ////////////////////////////////////////////////////////////////////////////////////
            ////////////////////////////////////////////////////////////////////////////////////
            //
            //                                  User input
            //
            ////////////////////////////////////////////////////////////////////////////////////
            ////////////////////////////////////////////////////////////////////////////////////

            Result re;

            re = RhinoGet.GetOneObject("Select reference surface", true, ObjectType.Surface, out ObjRef obj_ref);
            if (re != Result.Success)
                return re;
            Surface surface = obj_ref.Surface();

            re = RhinoGet.GetOneObject("Select mesh", true, ObjectType.Mesh, out ObjRef obj_ref_m);
            if (re != Result.Success)
                return re;
            Mesh mesh = obj_ref_m.Mesh();
     
            int nb_pix_U = 1000;
            re = RhinoGet.GetInteger("image size U (pixels)", true, ref nb_pix_U);
            if (re != Result.Success)
                return re;
            if (nb_pix_U < 10) nb_pix_U = 10;
            int nb_pix_V = nb_pix_U;

            re = RhinoGet.GetInteger("Image size V (pixels)", true, ref nb_pix_V);
            if (re != Result.Success)
                return re;
            if (nb_pix_V < 10) nb_pix_V = 10;

            double max_depth = 2.5;
            re = RhinoGet.GetNumber("Depth of black color (absolute value)", true, ref max_depth);
            if (max_depth < 0.001) max_depth = 0.001;
            if (re != Result.Success)
                return re;

            double default_depth = 0;
            re = RhinoGet.GetNumber("Default depth (0 to " + max_depth.ToString() + ")", true, ref default_depth);
            if (re != Result.Success)
                return re;
            if (default_depth > max_depth) default_depth = max_depth;
            if (default_depth < 0) default_depth = 0;
            ushort default_color = (ushort)((double)ushort.MaxValue * (1 - (default_depth / max_depth)));                        

            string str_use_zmin = "min";
            re = RhinoGet.GetString("Use Zmin or Zmax?", true, ref str_use_zmin);
            if (re != Result.Success)
                return re;
            str_use_zmin = str_use_zmin.ToUpper();
            bool use_zmin = false;
            if (str_use_zmin == "MAX") use_zmin = true;

            var save_file_dialog = new Eto.Forms.SaveFileDialog();
            save_file_dialog.Filters.Add(new Eto.Forms.FileFilter("Tiff files", ".tif"));
            if (save_file_dialog.ShowDialog("") != Eto.Forms.DialogResult.Ok)
                return Result.Failure;
            string filename = save_file_dialog.FileName;



            ////////////////////////////////////////////////////////////////////////////////////
            ////////////////////////////////////////////////////////////////////////////////////
            //
            //                              Initialize variables
            //
            ////////////////////////////////////////////////////////////////////////////////////
            ////////////////////////////////////////////////////////////////////////////////////  
            ///
            const double tolerance = 0;
            Interval udir = surface.Domain(0);
            Interval vdir = surface.Domain(1);
            double umax = udir.Max;
            double vmax = vdir.Max;
            double umin = udir.Min;
            double vmin = vdir.Min;
            double stepU = (umax - umin) / nb_pix_U;
            double stepV = (vmax - vmin) / nb_pix_V;
            double i, j;
            int pixels_number = nb_pix_U * nb_pix_V;
            uint pixels_processed = 0;
            double d_dbl = 0;
            double d;
            double max_depth_real = 0.0;
            Vector3d projection_dir;
            Point3d ptuv;
            ushort[] tiff_row = new ushort[nb_pix_U];
            long check_time = 0;
            DateTime dt_start = DateTime.Now;



            ////////////////////////////////////////////////////////////////////////////////////
            ////////////////////////////////////////////////////////////////////////////////////
            //
            //                      Initialize tiff image & main loop
            //
            ////////////////////////////////////////////////////////////////////////////////////
            ////////////////////////////////////////////////////////////////////////////////////           

            StatusBar.ShowProgressMeter(0, pixels_number, "Creating image...", true, true);

            using (Tiff output = Tiff.Open(filename, "w"))
            {
                output.SetField(TiffTag.IMAGEWIDTH, nb_pix_U);
                output.SetField(TiffTag.IMAGELENGTH, nb_pix_V);
                output.SetField(TiffTag.SAMPLESPERPIXEL, 1);
                output.SetField(TiffTag.BITSPERSAMPLE, 16);
                output.SetField(TiffTag.ROWSPERSTRIP, nb_pix_V);
                output.SetField(TiffTag.XRESOLUTION, 72.0);
                output.SetField(TiffTag.YRESOLUTION, 72.0);
                output.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.CENTIMETER);
                output.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
                output.SetField(TiffTag.COMPRESSION, Compression.NONE);
                output.SetField(TiffTag.FILLORDER, FillOrder.MSB2LSB);

                for (int v = 0; v < nb_pix_V; v++)
                {
                    if (DateTime.Now.Ticks >= check_time)
                    {
                        // updates progress bar every 5 seconds
                        StatusBar.UpdateProgressMeter((int)pixels_processed, true);
                        check_time = DateTime.Now.Ticks + 50000000;
                        RhinoApp.Wait();
                    }
                    
                    for (int u = 0; u < nb_pix_U; u++)
                    {
                        pixels_processed++;
                        i = umin + u * stepU;
                        j = vmax - v * stepV;

                        // evaluates the UV point and projects it onto the mesh
                        ptuv = surface.PointAt(i, j);
                        projection_dir = surface.NormalAt(i, j);
                        Point3d[] pom = Intersection.ProjectPointsToMeshes(new[] { mesh }, new[] { ptuv }, projection_dir, tolerance);

                        // check the numbers of intersections, if pom == null -> projection is failed
                        if (pom != null)
                        {
                            switch (pom.Length)
                            {
                                case 1:
                                    // single intersection found
                                    d = pom[0].DistanceTo(ptuv);                                        
                                    d_dbl = max_depth - d;
                                    if (d_dbl < 0) d_dbl = 0;  
                                    if (max_depth_real < d) max_depth_real = d;
                                    tiff_row[u] = (ushort)((double)ushort.MaxValue * (d_dbl / max_depth));
                                    break;

                                case 0:
                                    // if no intersection use default color
                                    tiff_row[u] = default_color;
                                    break;

                                default:
                                    // if multiple intersections calculate min & max distances
                                    double dmin = double.MaxValue;
                                    double dmax = 0;
                                    for (int x = 0; x < pom.Length; x++)
                                    {
                                        d = pom[x].DistanceTo(ptuv);
                                        if (d < dmin) dmin = d;
                                        if (d > dmax) dmax = d;
                                    }
                                    d = use_zmin ? dmin : dmax;
                                    d_dbl = max_depth - d;
                                    if (d_dbl < 0) d_dbl = 0; 
                                    if (max_depth_real < d) max_depth_real = d;
                                    tiff_row[u] = (ushort)((double)ushort.MaxValue * (d_dbl / max_depth));
                                    break;
                            }
                        }
                        else
                        {
                            // save white color if the projection is failed
                            tiff_row[u] = ushort.MaxValue;   
                        }
                    }

                    // save row to buffer & write line  
                    byte[] buffer = new byte[tiff_row.Length * sizeof(ushort)];
                    Buffer.BlockCopy(tiff_row, 0, buffer, 0, buffer.Length);
                    output.WriteScanline(buffer, v);
                }
               
            }



            ////////////////////////////////////////////////////////////////////////////////////
            ////////////////////////////////////////////////////////////////////////////////////
            //
            //                                  final output
            //
            ////////////////////////////////////////////////////////////////////////////////////
            ////////////////////////////////////////////////////////////////////////////////////

            DateTime dt_end = DateTime.Now;
            TimeSpan tspan_elapsed_time = dt_end - dt_start;
            StatusBar.HideProgressMeter();
            tspan_elapsed_time = dt_end - dt_start;
            RhinoApp.WriteLine("----------------- tiff file created -----------------");
            RhinoApp.WriteLine("Speed = {0} megapixels/sec.", (pixels_number * 0.000001 / tspan_elapsed_time.Seconds));
            RhinoApp.WriteLine("Total Time = {0}", tspan_elapsed_time);
            RhinoApp.WriteLine("Heightmap depth (depth of black color) = {0}", max_depth);
            RhinoApp.WriteLine("Maximum depth = {0}", max_depth_real);             
            return Result.Success;
        }
    }
}
