#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.IO;
using ConcreteWallFraming.Core;
using ConcreteWallFraming.Core.Common;
using System.Windows.Media;
using PDF_Analyzer.Result;
using System.Reflection.Metadata;
using Document = Autodesk.Revit.DB.Document;
#endregion

namespace ConcreteWallFraming.Core.RVTProcessor
{
    public class RVT_ProcessingCore
    {
        public static void ProcessRVT(Document doc)
        {
            FilteredElementCollector fec = new FilteredElementCollector(doc);
            List<Wall> allWalls = fec.WhereElementIsNotElementType().OfClass(typeof(Wall)).Cast<Wall>().ToList();
            ElementClassFilter filter = new ElementClassFilter(typeof(Part));
            List<List<Part>> allParts = allWalls.Select(w => w.GetDependentElements(filter).Select(id => doc.GetElement(id) as Part).Where(p => doc.GetElement(p.GetSourceElementIds().ToList().First().HostElementId) is Part).ToList()).ToList();

            List<WallInfo> wallInfo = new List<WallInfo>();
            for (int i = 0; i < allWalls.Count; i++)
            {
                if (allParts[i].Any())
                {
                    foreach (Part p in allParts[i])
                    {
                        (double wallThickness, List<CurveLoop> curves, XYZ direction, XYZ origin, XYZ wallBasePoint) = GetWallBounds(p);
                        wallInfo.Add(new WallInfo($"P{wallInfo.Count+1}",p, allWalls[i].Id, wallThickness, curves, direction, origin, wallBasePoint));
                    }
                }
                else
                {//has no parts

                    (double wallThickness, List<CurveLoop> curves, XYZ direction, XYZ origin, XYZ wallBasePoint) = GetWallBounds(allWalls[i]);
                    wallInfo.Add(new WallInfo($"P{wallInfo.Count + 1}", allWalls[i], ElementId.InvalidElementId, wallThickness, curves, direction, origin, wallBasePoint));
                }
            }

            List<string> names = wallInfo.Where(wi => wi.IsValid && wi.Bounds.Any()).Select(wi => wi.Name).ToList();
            List<List<PDF_Analyzer.Geometry.Rectangle>> rectanglesLists = wallInfo.Where(wi => wi.IsValid && wi.Bounds.Any()).Select(wi => wi.Bounds).ToList();

            List<PDFSheetAssemblyData> PDFResult = PDF_Analyzer.Core.RectanglesAnalyze_Run(rectanglesLists, names, 0.2, false);
            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            List<AssemblyInstance> generatedAssemblies = new List<AssemblyInstance>();
            if (PDFResult.Any())
            {
                ////LoadFamilies
                Assembly runningAssembly = Assembly.GetExecutingAssembly();
                string appDirectory = runningAssembly.ManifestModule.FullyQualifiedName.Remove(runningAssembly.ManifestModule.FullyQualifiedName.Length - runningAssembly.ManifestModule.Name.Length);
                //string setPositionBatchPath = System.IO.Path.Combine(runningAssembly.ManifestModule.FullyQualifiedName.Remove(runningAssembly.ManifestModule.FullyQualifiedName.Length - runningAssembly.ManifestModule.Name.Length), "00. Panel Shops - Field Use.pdf");
                string f1 = Path.Combine(appDirectory, "Allied_ARCO_Concrete_Wall.rfa");
                string f2 = Path.Combine(appDirectory, "Allied_ARCO_Lumber.rfa");


                var wallSymbols = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilySymbol))
                            .Cast<FamilySymbol>()
                            .Where(x => x.FamilyName == "Allied_ARCO_Concrete_Wall").ToList();


                var lumberSymbols = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilySymbol))
                            .Cast<FamilySymbol>()
                            .Where(x => x.FamilyName == "Allied_ARCO_Lumber").ToList();


                Transaction tr = new Transaction(doc);

                tr.Start("tr test");




                if (!wallSymbols.Any())
                {

                    if (!doc.LoadFamily(f1, new FamilyLoadOptions(), out Family family))
                    {
                        // TaskDialog.Show("Error", "Error during loading family");[UI]

                    }

                    doc.Regenerate();

                    wallSymbols = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(x => x.FamilyName == "Allied_ARCO_Concrete_Wall").ToList();
                }

                if (!lumberSymbols.Any())
                {

                    if (!doc.LoadFamily(f2, new FamilyLoadOptions(), out Family family))
                    {
                        //TaskDialog.Show("Error", "Error during loading family");[UI]

                    }

                    doc.Regenerate();

                    lumberSymbols = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(x => x.FamilyName == "Allied_ARCO_Lumber").ToList();
                }

                var wallSymbol = wallSymbols.FirstOrDefault();
                var lumberSymbol = lumberSymbols.FirstOrDefault();



                if (!wallSymbol.IsActive) wallSymbol.Activate();
                if (!lumberSymbol.IsActive) lumberSymbol.Activate();



                for (int i = 0; i < PDFResult.Count; i++)
                {
                    try
                    {
                        WallInfo wi = wallInfo.Where(w => w.Name == PDFResult[i].Name).FirstOrDefault();
                        //if (PDFResult[i].Name == "1209850") 
                        //{

                        //}
                        List<Element> assemblyElements = new List<Element>();
                        PDFSheetAssemblyData PDFData = PDFResult[i];
                        //symbol.LookupParameter($"H{i}").Set(0);
                        double X = wi.WallBasePoint.X + (wi.Normal.X * PDFData.Height * 0.5 * 1.1);//i * 50;
                        double Y = wi.WallBasePoint.Y + (wi.Normal.Y * PDFData.Height * 0.5 * 1.1);//0;
                        double Z = wi.WallBasePoint.Z;//0;
                        FamilyInstance wallInstance = doc.Create.NewFamilyInstance(new XYZ(X, Y, Z), wallSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        assemblyElements.Add(wallInstance);
                        doc.Regenerate();
                        wallInstance.LookupParameter($"Unique").Set(PDFData.Name);
                        wallInstance.LookupParameter($"W").Set(PDFData.Width);
                        wallInstance.LookupParameter($"H").Set(PDFData.Height);
                        wallInstance.LookupParameter($"Th").Set(wi.WallThickness);

                        for (int j = 0; j < PDFData.Openings.Count; j++)
                        {
                            PDFRectangleData rec = PDFData.Openings[j];
                            wallInstance.LookupParameter($"O{j + 1}").Set(1);
                            wallInstance.LookupParameter($"W{j + 1}").Set(rec.Width);
                            wallInstance.LookupParameter($"H{j + 1}").Set(rec.Height);

                            if (wi.Normal.X > 0.001 || wi.Normal.Y < - 0.001)//+X || -Y
                            {
                                wallInstance.LookupParameter($"X{j + 1}").Set(-(rec.Location.X - rec.Width));
                                wallInstance.LookupParameter($"Y{j + 1}").Set((rec.Location.Y - rec.Height));
                            }
                            else if (wi.Normal.Y > 0.001 || wi.Normal.X < -0.001)//-X || +Y
                            {
                                wallInstance.LookupParameter($"X{j + 1}").Set((rec.Location.X ));
                                wallInstance.LookupParameter($"Y{j + 1}").Set(-(rec.Location.Y));
                            }
                            else
                            {
                                wallInstance.LookupParameter($"X{j + 1}").Set(-(rec.Location.X - rec.Width));
                                wallInstance.LookupParameter($"Y{j + 1}").Set((rec.Location.Y - rec.Height));
                            }

                            if (j == 19) break;
                        }

                        for (int j = 0; j < PDFData.Lumbers.Count; j++)
                        {
                            PDFRectangleData rec = PDFData.Lumbers[j];
                            double x = X - rec.Location.X;
                            double y = Y + rec.Location.Y;

                            if (wi.Normal.X > 0.001 || wi.Normal.Y < -0.001)//+X || -Y
                            {
                                x = X - rec.Location.X;
                                y = Y + rec.Location.Y;
                            }
                            else if (wi.Normal.Y > 0.001 || wi.Normal.X < -0.001)//-X || +Y
                            {
                                x = X + rec.Location.X;
                                y = Y - rec.Location.Y;
                            }
                            else
                            {
                                x = X - rec.Location.X;
                                y = Y + rec.Location.Y;
                            }



                            FamilyInstance lumberInstance = doc.Create.NewFamilyInstance(new XYZ(x, y, Z), lumberSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            assemblyElements.Add(lumberInstance);
                            double w = rec.Width;
                            double h = rec.Height;
                            lumberInstance.LookupParameter($"W").Set(w);
                            lumberInstance.LookupParameter($"H").Set(h);
                            lumberInstance.LookupParameter($"Th").Set(wi.WallThickness + 0.2);


                            lumberInstance.LookupParameter($"Length").Set(w >= h ? w : h);
                            lumberInstance.LookupParameter($"Size").Set(w >= h ? h : w);
                        }

                        //create assembly

                        ElementId categoryId = assemblyElements[0].Category.Id;
                        /**/
                        AssemblyInstance assemblyInstance = AssemblyInstance.Create(doc, assemblyElements.Select(e => e.Id).ToList(), categoryId);
                        /**/
                        generatedAssemblies.Add(assemblyInstance);
                        //assemblyInstance.Name = PDFData.Name;
                    }
                    catch (Exception)
                    {


                    }

                }




                tr.Commit();

                /**/
                Transaction t = new Transaction(doc);

                /**/
                t.Start("rename");

                /**/
                for (int i = 0; i < PDFResult.Count; i++)
                /**/
                {
                    /**/
                    try
                    /**/
                    {
                        /**/
                        generatedAssemblies[i].AssemblyTypeName = PDFResult[i].Name;
                        WallInfo wi = wallInfo.Where(w => w.Name == PDFResult[i].Name).FirstOrDefault();
                        if (Math.Abs(wi.Normal.X) > 0.001)
                        {
                            var bx = generatedAssemblies[i].get_BoundingBox(null);
                            XYZ md = generatedAssemblies[i].GetCenter();// new XYZ((bx.Max.X + bx.Min.X) / 2, (bx.Max.Y + bx.Min.Y) / 2, (bx.Max.Z + bx.Min.Z) / 2);
                            ElementTransformUtils.RotateElement(doc, generatedAssemblies[i].Id, Line.CreateUnbound(md, new XYZ(0,0,1)), 1.5708);
                        }
                            generatedAssemblies[i].AddMemberIds(new List<ElementId>() { wi.Wall.Id });
                        /**/
                    }
                    /**/
                    catch (Exception)
                    /**/
                    {

                        /**/
                        continue;
                        /**/
                    }
                    /**/    //pdfImages\\22.png

                    /**/
                }
                /**/
                t.Commit();
            }
            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        }

        public static (double, List<CurveLoop>, XYZ, XYZ, XYZ) GetWallBounds(Element wall)
        {
            Solid wallSolid = GetSolidOfElement(wall);
            FaceArray faces = wallSolid.Faces;

            double wallThickness = 0;
            List<CurveLoop> curves = null;
            XYZ extrudeDirection = null;
            XYZ origin = null;




            double majorArea = 0;
            int curvesCount = 0;

            List<PlanarFace> allFaces = new List<PlanarFace>();
            foreach (Face f in faces)
            {
                PlanarFace pf = f as PlanarFace;
                allFaces.Add(pf);
                if (Math.Abs(pf.FaceNormal.Z) > 0.001) continue;
                if (pf.Area - majorArea > 0.001) //>
                {
                    majorArea = pf.Area;
                    curves = pf.GetEdgesAsCurveLoops().ToList();
                    extrudeDirection = pf.FaceNormal;
                    origin = pf.Origin;
                    curvesCount = 0;
                    foreach (CurveLoop loop in curves)
                    {
                        foreach (Curve curve in loop)
                        {
                            curvesCount++;
                        }
                    }

                }
                else if (Math.Abs(pf.Area - majorArea) < 0.0001) //==
                {
                    List<CurveLoop> tempCurves = pf.GetEdgesAsCurveLoops().ToList();
                    int c = 0;
                    foreach (CurveLoop loop in tempCurves)
                    {
                        foreach (Curve curve in loop)
                        {
                            c++;
                        }
                    }

                    if (c < curvesCount)
                    {
                        majorArea = pf.Area;
                        curves = tempCurves;
                        extrudeDirection = pf.FaceNormal;
                        origin = pf.Origin;
                        curvesCount = c;
                    }

                }

            }

            allFaces = allFaces.OrderByDescending(f => f.Area).Where(f => Math.Abs(f.FaceNormal.Z) < 0.0001).ToList();
            wallThickness = allFaces[0].Origin.DistanceTo(allFaces[1].Origin);
            var bx = wall.get_BoundingBox(null);
            XYZ lowerMax = new XYZ(bx.Max.X, bx.Max.Y, bx.Min.Z);
            XYZ wallBasePoint = new XYZ((lowerMax.X + bx.Min.X) / 2, (lowerMax.Y + bx.Min.Y) / 2, lowerMax.Z);

            //XYZ wallBasePoint = new XYZ(origin.X, origin.Y, min_z);
            return (wallThickness, curves, extrudeDirection, origin, wallBasePoint);
        }
        public static Solid GetSolidOfElement(Element element, bool combineAllSolids = false)
        {
            if (element == null) return null;
            GeometryElement geomElement = element.get_Geometry(new Options());
            Solid elementGeometry = null;

            foreach (var s in geomElement)
            {
                if (s is Solid) return s as Solid;
            }

            foreach (GeometryInstance geomIns in geomElement)
            {
                foreach (var geom in geomIns.GetInstanceGeometry())
                {
                    if (geom is Solid && (geom as Solid).Volume > 0)
                    {
                        if (elementGeometry == null)
                        {
                            elementGeometry = geom as Solid;
                            continue;
                        }

                        if (combineAllSolids)
                        {
                            if ((geom as Solid).Volume > 0)
                            {
                                Solid _elementGeometry = null;
                                try
                                {
                                    _elementGeometry = BooleanOperationsUtils.ExecuteBooleanOperation(elementGeometry, geom as Solid, BooleanOperationsType.Union);
                                }
                                catch (Exception) { }

                                if (_elementGeometry != null) elementGeometry = _elementGeometry;
                            }
                        }
                        else
                        {
                            if ((geom as Solid).Volume > elementGeometry.Volume) elementGeometry = geom as Solid;
                        }

                    }
                }
            }
            return elementGeometry;
        }


    }
}
