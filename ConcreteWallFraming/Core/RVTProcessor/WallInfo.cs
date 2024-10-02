using Autodesk.Revit.DB;
using PDF_Analyzer.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Rectangle = PDF_Analyzer.Geometry.Rectangle;

namespace ConcreteWallFraming.Core.RVTProcessor
{
    public class WallInfo
    {
        public Element Wall { get; set; }
        public string Name { get; set; }
        public double WallThickness { get; set; }
        public ElementId ParentId { get; set; }
        public bool IsPart { get { return ParentId != ElementId.InvalidElementId; } }
        public List<CurveLoop> RawBounds { get; set; }
        public XYZ Normal { get; set; }
        public XYZ Origin { get; set; }

        public XYZ WallBasePoint { get; set; }

        private XYZ uAxis { get; set; }
        private XYZ vAxis { get; set; }

        public List<Rectangle> Bounds { get; set; } = new List<Rectangle>();

        public bool IsValid { get; set; } = true;

        public WallInfo(string name, Element wall, ElementId parentId, double wallThickness, List<CurveLoop> rawBounds, XYZ normal, XYZ origin, XYZ wallBasePoint) 
        {
            if (wall.Id.IntegerValue == 1318311)
            {
                
            }
            Name = name;// wall.Id.IntegerValue.ToString();
            Wall = wall;
            WallThickness = wallThickness;
            ParentId = parentId;
            RawBounds = rawBounds;
            Normal = normal;
            Origin = origin;
            WallBasePoint = wallBasePoint;

            // Calculate the U and V axes
            uAxis = Normal.CrossProduct(new XYZ(0, 0, 1));
            vAxis = Normal.CrossProduct(uAxis);


            foreach (CurveLoop loop in rawBounds) 
            {
                List<PDF_Analyzer.Geometry.Line> lines = new List<PDF_Analyzer.Geometry.Line>();
                foreach (Curve c in loop) 
                {
                    XYZ sp = c.GetEndPoint(0);
                    XYZ ep = c.GetEndPoint(1);

                    UV _sp = this.XYZtoUV(sp);
                    UV _ep = this.XYZtoUV(ep);

                    lines.Add( new PDF_Analyzer.Geometry.Line(new PDF_Analyzer.Geometry.Vector(_sp.U, _sp.V), new PDF_Analyzer.Geometry.Vector(_ep.U, _ep.V)));
                }
                if (lines.Count == 4)
                {
                    Bounds.Add(Rectangle.FromLines(lines));
                }else if (lines.Count > 4)
                {
                    List<List<PDF_Analyzer.Geometry.Line>> formedLines = new List<List<PDF_Analyzer.Geometry.Line>>();
                    // order lines

                    // generate bound lines [main wall bounds]
                    List<PDF_Analyzer.Geometry.Vector> points = lines.Select(l => l.Start).ToList();
                    points.AddRange(lines.Select(l => l.End).ToList());
                    double max_x = points.OrderByDescending(p => p.X).Select(p => p.X).FirstOrDefault();
                    double max_y = points.OrderByDescending(p => p.Y).Select(p => p.Y).FirstOrDefault();

                    double min_x = points.OrderBy(p => p.X).Select(p => p.X).FirstOrDefault();
                    double min_y = points.OrderBy(p => p.Y).Select(p => p.Y).FirstOrDefault();

                    List<PDF_Analyzer.Geometry.Line> mainLines = new List<PDF_Analyzer.Geometry.Line>();
                    mainLines.Add(new PDF_Analyzer.Geometry.Line(new PDF_Analyzer.Geometry.Vector(max_x, max_y), new PDF_Analyzer.Geometry.Vector(min_x, max_y)));
                    mainLines.Add(new PDF_Analyzer.Geometry.Line(new PDF_Analyzer.Geometry.Vector(min_x, max_y), new PDF_Analyzer.Geometry.Vector(min_x, min_y)));
                    mainLines.Add(new PDF_Analyzer.Geometry.Line(new PDF_Analyzer.Geometry.Vector(min_x, min_y), new PDF_Analyzer.Geometry.Vector(max_x, min_y)));
                    mainLines.Add(new PDF_Analyzer.Geometry.Line(new PDF_Analyzer.Geometry.Vector(max_x, min_y), new PDF_Analyzer.Geometry.Vector(max_x, max_y)));
                    formedLines.Add(mainLines);
                    // remove the lines that belongs to the main wall bounds
                    List<int> toBeRemoved = new List<int>();
                    foreach (var ml in mainLines)
                    {
                        lines.ForEach(l =>
                        {
                            if (ml.IsContain(l))
                            {
                                int index = lines.IndexOf(l);
                                toBeRemoved.Add(index);
                                //lines[index] = null;
                            }
                        });
                    }
                    foreach (int i in toBeRemoved)
                    {
                        lines[i] = null;
                    }


                    // for each 3-line & 2-line groups generate the rectangle
                    List<List<PDF_Analyzer.Geometry.Line>> brokeLinesList = BreakWithNulls(lines);

                    foreach (List<PDF_Analyzer.Geometry.Line> brokeLines in brokeLinesList)
                    {
                        if (brokeLines.Count == 2)
                        {
                            double x1 = brokeLines[0].Start.X;
                            double y1 = brokeLines[0].Start.Y;

                            double x2 = brokeLines[1].End.X;
                            double y2 = brokeLines[0].End.Y;



                            List<PDF_Analyzer.Geometry.Line> generatedLines = new List<PDF_Analyzer.Geometry.Line>();
                            generatedLines.Add(new PDF_Analyzer.Geometry.Line(new PDF_Analyzer.Geometry.Vector(x1, y1), new PDF_Analyzer.Geometry.Vector(x1, y2)));
                            generatedLines.Add(new PDF_Analyzer.Geometry.Line(new PDF_Analyzer.Geometry.Vector(x1, y2), new PDF_Analyzer.Geometry.Vector(x2, y2)));
                            generatedLines.Add(new PDF_Analyzer.Geometry.Line(new PDF_Analyzer.Geometry.Vector(x2, y2), new PDF_Analyzer.Geometry.Vector(x2, y1)));
                            generatedLines.Add(new PDF_Analyzer.Geometry.Line(new PDF_Analyzer.Geometry.Vector(x2, y1), new PDF_Analyzer.Geometry.Vector(x1, y1)));
                            formedLines.Add(generatedLines);


                        } else if (brokeLines.Count == 3) 
                        {
                            brokeLines.Add(new PDF_Analyzer.Geometry.Line(brokeLines[2].End, brokeLines[0].Start));
                            formedLines.Add(brokeLines);
                        }
                        else if (brokeLines.Count == 4)
                        {
                            //[TODO] RECTS CANNOT BE CREATED!
                            formedLines.Add(brokeLines);
                        }

                    }




                    // proceed
                   //// formedLines.ForEach(_lines => Bounds.Add(Rectangle.FromLines(_lines)));

                    foreach (var _lines in formedLines)
                    {
                       Rectangle r = Rectangle.FromLines(_lines);
                        if (r == null)
                        {
                            
                        }
                        Bounds.Add(r);
                    }
                    //Bounds.Add(Rectangle.FromLines(mainLines));
                }
                else
                {
                    IsValid = false;
                }
            }
            Bounds = Bounds.Where(b => b != null).OrderByDescending/*.OrderBy*/(r => r.Area).ToList();

        }
        public static List<List<PDF_Analyzer.Geometry.Line>> BreakWithNulls(List<PDF_Analyzer.Geometry.Line> inputList)
        {
            List<List<PDF_Analyzer.Geometry.Line>> resultList = new List<List<PDF_Analyzer.Geometry.Line>>();

            List<PDF_Analyzer.Geometry.Line> currentSublist = new List<PDF_Analyzer.Geometry.Line>();

            foreach (var item in inputList)
            {
                if (item != null)
                {
                    currentSublist.Add(item);
                }
                else if (currentSublist.Count > 0)
                {
                    resultList.Add(new List<PDF_Analyzer.Geometry.Line>(currentSublist));
                    currentSublist.Clear();
                }
            }

            if (currentSublist.Count > 0)
            {
                resultList.Add(new List<PDF_Analyzer.Geometry.Line>(currentSublist));
            }

            /////
            ///

            if (resultList.Count >= 2)
            {

                if (resultList[0].Count == 1)
                {
                    //add to the end of the last
                    resultList[resultList.Count - 1].Add(resultList[0][0]);
                    //clear this
                    resultList[0].Clear();
                } else if (resultList[resultList.Count - 1].Count == 1) 
                {
                    //add to the start of the first
                    resultList[0].Insert(0, resultList[resultList.Count - 1][0]);
                    //clear this
                    resultList[resultList.Count - 1].Clear();
                }
            }
            //filter empty or count==1
            resultList = resultList.Where(lst => lst.Count > 1).ToList();

            return resultList;
        }
        public UV XYZtoUV(XYZ xyzPoint) 
        {
            // Project the XYZ point onto the U and V axes
            double uCoordinate = (xyzPoint - Origin).DotProduct(uAxis);
            double vCoordinate = (xyzPoint - Origin).DotProduct(vAxis);

            return new UV(uCoordinate, vCoordinate);
        }
        public XYZ UVtoXYZ(UV uvPoint) 
        {
            XYZ convertedXYZ = Origin + uvPoint.U * uAxis + uvPoint.V * vAxis;
            return convertedXYZ;
        }
    }
}
