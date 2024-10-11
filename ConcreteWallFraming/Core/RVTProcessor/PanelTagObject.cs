using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConcreteWallFraming.Core.RVTProcessor
{
    public class PanelTagObject
    {
        public FamilyInstance TagInstance { get; set; }
        public int PanelNo { get; set; }
        public XYZ ReferencePoint { get; set; }
        public XYZ ReferencePoint_2D { get { return new XYZ(ReferencePoint.X, ReferencePoint.Y, 0); } }
        public XYZ Direction {  get; set; }
        public bool IsUsed { get; set; } = false;
        public List<WallObject> Walls { get; set; } = new List<WallObject>();
        public List<WallObject> OrderedWalls { get { return Walls.OrderBy(w => w.distanceToTag).ToList(); } }
        public WallObject NearestWall { get { return OrderedWalls.FirstOrDefault(); } }
        public PanelTagObject(FamilyInstance tagInstance, int panelNo, XYZ referencePoint , XYZ direction) 
        {
            TagInstance = tagInstance;
            PanelNo = panelNo;
            ReferencePoint = referencePoint;
            Direction = direction;
        }
    }

    public class WallObject 
    {
        public Element wall {  get; set; }

        double wallThickness { get; set; }
        List<CurveLoop> curves { get; set; }
        public XYZ direction { get; set; }
        public XYZ origin { get; set; }
        public XYZ wallBasePoint { get; set; }


        public double distanceToTag { get; set; }

        public WallObject(Element wall, double wallThickness, List<CurveLoop> curves, XYZ direction, XYZ origin, XYZ wallBasePoint, double distanceToTag)
        {
            this.wall = wall;
            this.wallThickness = wallThickness;
            this.curves = curves;
            this.direction = direction;
            this.origin = origin;
            this.wallBasePoint = wallBasePoint;


            this.distanceToTag = distanceToTag;
        }
    }
}
