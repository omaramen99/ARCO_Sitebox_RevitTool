#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using PDF_Analyzer;
using PDF_Analyzer.Result;
using System.Linq;
using System.Reflection;
using System.IO;
using ConcreteWallFraming.Core;
using ConcreteWallFraming.Core.Common;

#endregion

namespace ConcreteWallFraming.Core.PDFProcessor
{
    public class PDF_ProcessingCore
    {
        public static void ProcessPDF(Document doc)
        {
            List<PDFSheetAssemblyData> PDFResult = PDF_Analyzer.Core.PDFAnalyze_Run(false);
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
                    List<Element> assemblyElements = new List<Element>();
                    PDFSheetAssemblyData PDFData = PDFResult[i];
                    //symbol.LookupParameter($"H{i}").Set(0);
                    double X = i * 500 / 10;
                    double Y = 0;
                    FamilyInstance wallInstance = doc.Create.NewFamilyInstance(new XYZ(X, Y, 0), wallSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                    assemblyElements.Add(wallInstance);
                    doc.Regenerate();
                    wallInstance.LookupParameter($"W").Set(PDFData.Width / 10);
                    wallInstance.LookupParameter($"H").Set(PDFData.Height / 10);
                    wallInstance.LookupParameter($"Th").Set(1);

                    for (int j = 0; j < PDFData.Openings.Count; j++)
                    {
                        PDFRectangleData rec = PDFData.Openings[j];
                        wallInstance.LookupParameter($"O{j + 1}").Set(1);
                        wallInstance.LookupParameter($"W{j + 1}").Set(rec.Width / 10);
                        wallInstance.LookupParameter($"H{j + 1}").Set(rec.Height / 10);
                        wallInstance.LookupParameter($"X{j + 1}").Set(rec.Location.X / 10);
                        wallInstance.LookupParameter($"Y{j + 1}").Set(-rec.Location.Y / 10);
                    }

                    for (int j = 0; j < PDFData.Lumbers.Count; j++)
                    {
                        PDFRectangleData rec = PDFData.Lumbers[j];
                        double x = X + rec.Location.X / 10;
                        double y = Y - rec.Location.Y / 10;
                        FamilyInstance lumberInstance = doc.Create.NewFamilyInstance(new XYZ(x, y, 0), lumberSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        assemblyElements.Add(lumberInstance);
                        double w = rec.Width / 10;
                        double h = rec.Height / 10;
                        lumberInstance.LookupParameter($"W").Set(w);
                        lumberInstance.LookupParameter($"H").Set(h);
                        lumberInstance.LookupParameter($"Th").Set(1.2);


                        lumberInstance.LookupParameter($"Length").Set(w >= h ? w : h);
                        lumberInstance.LookupParameter($"Size").Set(w >= h ? h : w);
                    }

                    //create assembly

                    ElementId categoryId = assemblyElements[0].Category.Id;
                    AssemblyInstance assemblyInstance = AssemblyInstance.Create(doc, assemblyElements.Select(e => e.Id).ToList(), categoryId);
                    generatedAssemblies.Add(assemblyInstance);
                    //assemblyInstance.Name = PDFData.Name;
                }




                tr.Commit();

                Transaction t = new Transaction(doc);

                t.Start("rename");

                for (int i = 0; i < PDFResult.Count; i++)
                {
                    try
                    {
                        generatedAssemblies[i].AssemblyTypeName = PDFResult[i].Name;
                    }
                    catch (Exception)
                    {

                        continue;
                    }
                    //pdfImages\\22.png
                    var assemblySheet = AssemblyViewUtils.CreateSheet(doc, generatedAssemblies[i].Id, ElementId.InvalidElementId);
                    assemblySheet.Name = PDFResult[i].Name;




                    string saveBatchPath = System.IO.Path.Combine(runningAssembly.ManifestModule.FullyQualifiedName.Remove(runningAssembly.ManifestModule.FullyQualifiedName.Length - runningAssembly.ManifestModule.Name.Length), $"pdfImages\\{PDFResult[i].Page}.png");




                    ImageTypeOptions options = new ImageTypeOptions(saveBatchPath, false, ImageTypeSource.Import);
                    ImageType type = ImageType.Create(doc, options);

                    ImageInstance.Create(
                                        doc,
                                        assemblySheet,
                                        type.Id,
                                        new ImagePlacementOptions()
                                    );


                }
                t.Commit();
            }
        }
    }
}
