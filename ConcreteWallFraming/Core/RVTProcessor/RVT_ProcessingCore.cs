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
        static int ii = 100;
        public static string GetPanelName(XYZ wallBasePoint, XYZ direction, List<PanelTagObject> panelTagObjects)
        {
            XYZ wallBasePoint_2D = new XYZ(wallBasePoint.X, wallBasePoint.Y, 0);
            List<PanelTagObject> _panelTagObjects = panelTagObjects.Where(o => o.Direction.IsAlmostEqualTo(direction.Normalize()) || o.Direction.IsAlmostEqualTo(direction.Normalize() * -1)).OrderBy(o => o.ReferencePoint_2D.DistanceTo(wallBasePoint_2D)).ToList();
            PanelTagObject panelTagObject = _panelTagObjects.Where(o => !o.IsUsed).FirstOrDefault();//panelTagObjects.Where(o => o.Direction.IsAlmostEqualTo(direction.Normalize()) || o.Direction.IsAlmostEqualTo(direction.Normalize() * -1)).Where(o => !o.IsUsed).OrderBy(o => o.ReferencePoint_2D.DistanceTo(wallBasePoint_2D)).FirstOrDefault();
            if (panelTagObject != null)
            {
                panelTagObject.IsUsed = true;
                return "P" + panelTagObject.PanelNo;
            }
            ii++;
            return "p" + ii;
        }
        public static List<PanelTagObject> PreparePanelsNames(List<Element> walls, List<double> wallThicknesses, List<List<CurveLoop>> curvesList, List<XYZ> directions, List<XYZ> origins, List<XYZ> wallBasePoints, List<PanelTagObject> panelTagObjects)
        {

            for (int i = 0; i < walls.Count; i++)
            {
                var wallThickness = wallThicknesses[i];
                var curves = curvesList[i];
                var direction = directions[i];
                var origin = origins[i];
                var wallBasePoint = wallBasePoints[i];

                XYZ wallBasePoint_2D = new XYZ(wallBasePoint.X, wallBasePoint.Y, 0);

                foreach (PanelTagObject o in panelTagObjects)
                {
                    if (o.Direction.IsAlmostEqualTo(direction.Normalize()) || o.Direction.IsAlmostEqualTo(direction.Normalize() * -1))
                    {
                        o.Walls.Add(new WallObject(walls[i], wallThickness, curves,direction,origin, wallBasePoint, o.ReferencePoint_2D.DistanceTo(wallBasePoint_2D)));
                    }
                }
            }

            List<PanelTagObject> ss = panelTagObjects.GroupBy(o => o.NearestWall.wall.Id.IntegerValue).Select(t => t.ToList()).Select(t => t.FirstOrDefault()).ToList();//.ForEach(g => g.OrderBy(o => o.NearestWall.distanceToTag));

            return ss;


        }
        public static void ProcessRVT(Document doc)
        {
            FilteredElementCollector fec = new FilteredElementCollector(doc);
            List<Wall> allWalls = fec.WhereElementIsNotElementType().OfClass(typeof(Wall)).Cast<Wall>().ToList();
            List<FamilyInstance> allPanelTags = new FilteredElementCollector(doc).WhereElementIsNotElementType().OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().Where(s => s.ViewSpecific && s.LookupParameter("Panel No") != null && s.LookupParameter("Panel No").AsString() != string.Empty && int.TryParse(s.LookupParameter("Panel No").AsString(), out int ss)).Where(s => Math.Abs( s.FacingOrientation.Z) < 0.01 ).ToList();
            List<FamilyInstance> f_allPanelTags = allPanelTags.GroupBy(t => t.LookupParameter("Panel No").AsString()).Select(t => t.First()).ToList();
            List<FamilyInstance> o_f_allPanelTags = f_allPanelTags.OrderBy(t => int.Parse(t.LookupParameter("Panel No").AsString())).ToList();
            List<PanelTagObject> panelTagObjects = o_f_allPanelTags.Select(fi => new PanelTagObject(fi, int.Parse(fi.LookupParameter("Panel No").AsString()), (fi.Location as LocationPoint).Point, fi.FacingOrientation.Normalize())).ToList();


            //List<int> nums = o_f_allPanelTags.Select(t => int.Parse(t.LookupParameter("Panel No").AsString())).ToList();
            ElementClassFilter filter = new ElementClassFilter(typeof(Part));
            List<List<Part>> allParts = allWalls.Select(w => w.GetDependentElements(filter).Select(id => doc.GetElement(id) as Part).Where(p => doc.GetElement(p.GetSourceElementIds().ToList().First().HostElementId) is Part).ToList()).ToList();

            
                List<Element> walls = new List<Element>();
                List<double> wallThicknesses = new List<double>();
                List<List<CurveLoop>> curvesList = new List<List<CurveLoop>>();
                List<XYZ> directions = new List<XYZ>();
                List<XYZ> origins = new List<XYZ>();
                List<XYZ> wallBasePoints = new List<XYZ>();
            for (int i = 0; i < allWalls.Count; i++)
            {


                if (allParts[i].Any())
                {
                    foreach (Part p in allParts[i])
                    {

                        (double wallThickness, List<CurveLoop> curves, XYZ direction, XYZ origin, XYZ wallBasePoint) = GetWallBounds(p);
                        walls.Add(p);
                        wallThicknesses.Add(wallThickness);
                        curvesList.Add(curves);
                        directions.Add(direction);
                        origins.Add(origin);
                        wallBasePoints.Add(wallBasePoint);
                    }
                }
                else
                {//has no parts

                    (double wallThickness, List<CurveLoop> curves, XYZ direction, XYZ origin, XYZ wallBasePoint) = GetWallBounds(allWalls[i]);
                    walls.Add(allWalls[i]);
                    wallThicknesses.Add(wallThickness);
                    curvesList.Add(curves);
                    directions.Add(direction);
                    origins.Add(origin);
                    wallBasePoints.Add(wallBasePoint);



                }
            }
            List<PanelTagObject> ssss = PreparePanelsNames(walls, wallThicknesses, curvesList, directions, origins, wallBasePoints, panelTagObjects);

            List<WallInfo> wallInfo = new List<WallInfo>();
            for (int i = 0; i < allWalls.Count; i++)
            {

                /////////////////////////////////////////////////////////
                if (allParts[i].Any())
                {
                    foreach (Part p in allParts[i])
                    {
                        if (p.Id.IntegerValue == 1136309)
                        {

                        }
                        (double wallThickness, List<CurveLoop> curves, XYZ direction, XYZ origin, XYZ wallBasePoint) = GetWallBounds(p);
                        //string panelName = p.LookupParameter("Comments").AsString().Split(' ').ToList()[0];
                        var eee = ssss.FirstOrDefault(o => o.NearestWall.wall.Id.IntegerValue == p.Id.IntegerValue);
                        string panelName = "";
                        if (eee != null)
                        {
                            panelName = "P" + eee.PanelNo;
                        }
                        else 
                        {
                        ii++;
                        panelName = "p" + ii;
                        
                        }

                        //string panelName = ssss.FirstOrDefault(o => o.NearestWall.wall.Id.IntegerValue == p.Id.IntegerValue).//GetPanelName(wallBasePoint, direction, panelTagObjects);

                        wallInfo.Add(new WallInfo(panelName, p, allWalls[i].Id, wallThickness, curves, direction, origin, wallBasePoint));
                    }
                }
                else
                {//has no parts

                    (double wallThickness, List<CurveLoop> curves, XYZ direction, XYZ origin, XYZ wallBasePoint) = GetWallBounds(allWalls[i]);
                    //string panelName = allWalls[i].LookupParameter("Comments").AsString().Split(' ').ToList()[0];
                    //string panelName = GetPanelName(wallBasePoint, direction, panelTagObjects);
                    var eee = ssss.FirstOrDefault(o => o.NearestWall.wall.Id.IntegerValue == allWalls[i].Id.IntegerValue);
                    string panelName = "";
                    if (eee != null)
                    {
                        panelName = "P" + eee.PanelNo;
                    }
                    else 
                    {
                    ii++;
                    panelName = "p" + ii;
                    
                    }
                    wallInfo.Add(new WallInfo(panelName, allWalls[i], ElementId.InvalidElementId, wallThickness, curves, direction, origin, wallBasePoint));
                }
            }
            wallInfo = wallInfo.OrderBy(w => int.Parse(w.Name.Substring(1))).ToList();
            List<string> names = wallInfo.Where(wi => wi.IsValid && wi.Bounds.Any()).Select(wi => wi.Name).ToList();
            List<List<PDF_Analyzer.Geometry.Rectangle>> rectanglesLists = wallInfo.Where(wi => wi.IsValid && wi.Bounds.Any()).Select(wi => wi.Bounds).ToList();
            double lumberThickness = 0.235026;
            List<PDFSheetAssemblyData> PDFResult = PDF_Analyzer.Core.RectanglesAnalyze_Run(rectanglesLists, names, lumberThickness, false);

            List<PDFSheetVertualAssemblyData> PDFVertualResult = PDF_Analyzer.Core.HelperPDFAnalyze_Run(false);
            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            List<AssemblyInstance> generatedAssemblies = new List<AssemblyInstance>();
            List<ElementId> generatedAssembliesIds = new List<ElementId>();
            List<string> generatedAssembliesNames = new List<string>();

            if (PDFResult.Any())
            {
                ////LoadFamilies
                Assembly runningAssembly = Assembly.GetExecutingAssembly();
                string appDirectory = runningAssembly.ManifestModule.FullyQualifiedName.Remove(runningAssembly.ManifestModule.FullyQualifiedName.Length - runningAssembly.ManifestModule.Name.Length);
                //string setPositionBatchPath = System.IO.Path.Combine(runningAssembly.ManifestModule.FullyQualifiedName.Remove(runningAssembly.ManifestModule.FullyQualifiedName.Length - runningAssembly.ManifestModule.Name.Length), "00. Panel Shops - Field Use.pdf");
                //C:\Users\pc\AppData\Roaming\Autodesk\Revit\Addins\2025
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


                //Get Lowest Z
                double lowestZ = wallInfo.OrderBy(w => w.WallBasePoint.Z).Select(w => w.WallBasePoint.Z).FirstOrDefault();


                ////////string lastWallGroup = "";
                ////////int groupIndex = 0;
                ////////XYZ lastBasePoint = new XYZ();
                ////////List<ElementId> lastSharedLumberIds = new List<ElementId> ();
                ////////List<XYZ> lastSharedLumberLocations = new List<XYZ> ();

                double callibrationValue = -1;
                for (int i = 0; i < PDFResult.Count; i++)
                {
                    try
                    {
                        PDFSheetVertualAssemblyData vd = PDFVertualResult.Where(r => r.Name == PDFResult[i].Name).FirstOrDefault();
                        //if (vd == null) continue;
                        if(callibrationValue == -1 && vd != null) callibrationValue = PDFResult[i].Width / vd.wallRectangle.Width();

                        WallInfo wi = wallInfo.Where(w => w.Name == PDFResult[i].Name).FirstOrDefault();

                       


                        ////////var panelInfo = wi.Wall.LookupParameter("Comments").AsString().Split(' ').ToList();

                        ////////if (lastWallGroup == panelInfo[1])
                        ////////{
                        ////////    groupIndex++;
                        ////////}
                        ////////else
                        ////////{
                        ////////    lastWallGroup = panelInfo[1];
                        ////////    lastBasePoint = new XYZ();
                        ////////    lastSharedLumberIds = new List<ElementId>();
                        ////////    lastSharedLumberLocations = new List<XYZ>();
                        ////////    groupIndex = 0;
                        ////////}
                        //if (PDFResult[i].Name == "1209850") 
                        //{

                        //}
                        if (wi.Wall.Id.IntegerValue == 1156144)
                        {

                        }
                        List<Element> assemblyElements = new List<Element>();
                        PDFSheetAssemblyData PDFData = PDFResult[i];
                        //symbol.LookupParameter($"H{i}").Set(0);
                        double X = vd != null? (-75)+  callibrationValue * vd.wallRectangle.Center.X : wi.WallBasePoint.X + (wi.Normal.X * PDFData.Height * 0.5 * 1.1);//i * 50;
                        double Y = vd != null? (215)+ -callibrationValue * vd.wallRectangle.Center.Y : wi.WallBasePoint.Y + (wi.Normal.Y * PDFData.Height * 0.5 * 1.1);//0;
                        double Z = lowestZ;//wi.WallBasePoint.Z;//0;
                        XYZ groupDirection = new XYZ();
                        ////////if (groupIndex != 0) 
                        ////////{
                        ////////    groupDirection = (new XYZ(X, Y, Z) - lastBasePoint).Normalize();
                        ////////    X = X + (groupDirection.X * lumberThickness *groupIndex);
                        ////////    Y = Y + (groupDirection.Y * lumberThickness *groupIndex);
                        ////////    Z = Z + (groupDirection.Z * lumberThickness *groupIndex);
                        ////////}
                        ////////lastBasePoint = new XYZ(X, Y, Z);

                        FamilyInstance wallInstance = doc.Create.NewFamilyInstance(new XYZ(X, Y, Z), wallSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        assemblyElements.Add(wallInstance);
                        doc.Regenerate();
                        wallInstance.LookupParameter($"Unique").Set(PDFData.Name);
                        wallInstance.LookupParameter($"Allied_Panel").Set(wi.Name);
                        wallInstance.LookupParameter($"Allied_Group").Set(vd != null ? vd.Group : "N/A");//.Set(panelInfo[1].Remove(panelInfo[1].Length - 1).Substring(1));
                        wi.Wall.LookupParameter($"Allied_Panel").Set(wi.Name);
                        wi.Wall.LookupParameter($"Allied_Group").Set(vd != null ? vd.Group : "N/A");//.Set(panelInfo[1].Remove(panelInfo[1].Length - 1).Substring(1));


                        wallInstance.LookupParameter($"W").Set(PDFData.Width);
                        wallInstance.LookupParameter($"H").Set(PDFData.Height);
                        wallInstance.LookupParameter($"Th").Set(wi.WallThickness);

                        for (int j = 0; j < PDFData.Openings.Count; j++)
                        {
                            PDFRectangleData rec = PDFData.Openings[j];
                            wallInstance.LookupParameter($"O{j + 1}").Set(1);
                            wallInstance.LookupParameter($"W{j + 1}").Set(rec.Width);
                            wallInstance.LookupParameter($"H{j + 1}").Set(rec.Height);

                            if (wi.Normal.X > 0.001 || wi.Normal.Y < -0.001)//+X || -Y
                            {
                                wallInstance.LookupParameter($"X{j + 1}").Set(-(rec.Location.X - rec.Width));
                                wallInstance.LookupParameter($"Y{j + 1}").Set((rec.Location.Y - rec.Height));
                            }
                            else if (wi.Normal.Y > 0.001 || wi.Normal.X < -0.001)//-X || +Y
                            {
                                wallInstance.LookupParameter($"X{j + 1}").Set((rec.Location.X));
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

                            XYZ refPoint = new XYZ((groupDirection.X * x), (groupDirection.Y * y), 0);




                            FamilyInstance lumberInstance = doc.Create.NewFamilyInstance(new XYZ(x, y, Z), lumberSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            assemblyElements.Add(lumberInstance);
                            double w = rec.Width;
                            double h = rec.Height;
                            lumberInstance.LookupParameter($"Allied_Panel").Set(wi.Name);
                            lumberInstance.LookupParameter($"Allied_Group").Set(vd != null ? vd.Group : "N/A"); //Set(panelInfo[1].Remove(panelInfo[1].Length - 1).Substring(1));
                            lumberInstance.LookupParameter($"W").Set(w);
                            lumberInstance.LookupParameter($"H").Set(h);
                            lumberInstance.LookupParameter($"Th").Set(wi.WallThickness + lumberThickness);


                            lumberInstance.LookupParameter($"Length").Set(w >= h ? w : h);
                            lumberInstance.LookupParameter($"Size").Set(w >= h ? h : w);
                        }

                        //create assembly

                        ElementId categoryId = assemblyElements[0].Category.Id;
                        /**/
                        AssemblyInstance assemblyInstance = AssemblyInstance.Create(doc, assemblyElements.Select(e => e.Id).ToList(), categoryId);
                        /**/
                        generatedAssemblies.Add(assemblyInstance); 
                        generatedAssembliesIds.Add(assemblyInstance.Id);
                        generatedAssembliesNames.Add(wi.Name);
                        //assemblyInstance.Name = PDFData.Name;
                    }
                    catch (Exception e)
                    {


                    }

                }




                tr.Commit();

                /**/
                Transaction t = new Transaction(doc);

                /**/
                t.Start("PostProcessing");

                /**/
                for (int i = 0; i < PDFResult.Count; i++)
                /**/
                {
                    /**/
                    try
                    /**/
                    {
                        /**/
                        //if (PDFResult[i].Name == "P33")
                        //{
                            
                        //}
                        int ind = generatedAssembliesNames.IndexOf(PDFResult[i].Name); if (ind == -1) continue;
                        ElementId assemblyId = generatedAssembliesIds[ind]; if (assemblyId == ElementId.InvalidElementId) continue;
                        AssemblyInstance generatedAssembly = doc.GetElement(assemblyId) as AssemblyInstance; if (generatedAssembly == null) continue;
                        generatedAssembly.AssemblyTypeName = PDFResult[i].Name;

                        PDFSheetVertualAssemblyData vd = PDFVertualResult.Where(r => r.Name == PDFResult[i].Name).FirstOrDefault();
                        if (vd == null) 
                        {
                            WallInfo wi = wallInfo.Where(w => w.Name == PDFResult[i].Name).FirstOrDefault();
                            if (Math.Abs(wi.Normal.X) > 0.001)
                            {
                                var bx = generatedAssembly.get_BoundingBox(null);
                                XYZ md = generatedAssembly.GetCenter();// new XYZ((bx.Max.X + bx.Min.X) / 2, (bx.Max.Y + bx.Min.Y) / 2, (bx.Max.Z + bx.Min.Z) / 2);
                                ElementTransformUtils.RotateElement(doc, assemblyId, Line.CreateUnbound(md, new XYZ(0, 0, 1)), 1.5708);
                            }
                            //generatedAssembly.Disassemble();
                            //continue;
                        }
                        else
                        {
                            WallInfo wi = wallInfo.Where(w => w.Name == PDFResult[i].Name).FirstOrDefault();
                            var rec = vd.wallRectangle;
                            if ((rec.Width() >= rec.Height() && PDFResult[i].Height > PDFResult[i].Width) || (rec.Width() <= rec.Height() && PDFResult[i].Height < PDFResult[i].Width))
                            {
                                var bx = generatedAssembly.get_BoundingBox(null);
                                XYZ md = generatedAssembly.GetCenter();// new XYZ((bx.Max.X + bx.Min.X) / 2, (bx.Max.Y + bx.Min.Y) / 2, (bx.Max.Z + bx.Min.Z) / 2);
                                ElementTransformUtils.RotateElement(doc, generatedAssembly.Id, Line.CreateUnbound(md, new XYZ(0, 0, 1)), 1.5708);
                            }
                            generatedAssembly.Disassemble();
                        }
                        //if (Math.Abs(wi.Normal.X) > 0.001)
                        //{
                        //    var bx = generatedAssembly.get_BoundingBox(null);
                        //    XYZ md = generatedAssembly.GetCenter();// new XYZ((bx.Max.X + bx.Min.X) / 2, (bx.Max.Y + bx.Min.Y) / 2, (bx.Max.Z + bx.Min.Z) / 2);
                        //    ElementTransformUtils.RotateElement(doc, generatedAssemblies[i].Id, Line.CreateUnbound(md, new XYZ(0, 0, 1)), 1.5708);
                        //}

                        //generatedAssembly.AddMemberIds(new List<ElementId>() { wi.Wall.Id });
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
