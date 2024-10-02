#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using PDF_Analyzer;
using PDF_Analyzer.Result;
using System.Linq;
using System.Reflection;
using System.IO;
using ConcreteWallFraming.Core;
using ConcreteWallFraming.Core.PDFProcessor;
using ConcreteWallFraming.Core.RVTProcessor;
using ConcreteWallFraming.Core.DWGProcessor;


#endregion

namespace ConcreteWallFraming

{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;
            #region
            ////// Access current selection

            ////Selection sel = uidoc.Selection;

            ////// Retrieve elements from database

            ////FilteredElementCollector colWall
            ////  = new FilteredElementCollector(doc)
            ////    .WhereElementIsNotElementType()
            ////    .OfCategory(BuiltInCategory.INVALID)
            ////    .OfClass(typeof(Wall));

            ////// Filtered element collector is iterable

            ////foreach (Wall wall in colWall)
            ////{
            ////    message += "Nombre:" + wall.Name + "\n";
            ////    message += "ID:" + wall.Id + "\n";
            ////}

            ////TaskDialog tsk = new TaskDialog("Nombres e ID");
            ////tsk.MainInstruction = message;
            ////tsk.MainContent = "Revit sample";
            ////tsk.AllowCancellation = true;
            ////tsk.CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel;
            ////tsk.Show();
            #endregion

           
            //PDF_ProcessingCore.ProcessPDF(doc);
            //DWG_ProcessingCore.ProcessDWG(doc);
            RVT_ProcessingCore.ProcessRVT(doc);



            return Result.Succeeded;
        }
    }

}
