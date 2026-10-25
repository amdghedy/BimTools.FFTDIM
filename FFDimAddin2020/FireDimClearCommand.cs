using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace FFDimAddin2020
{
    [Transaction(TransactionMode.Manual)]
    public class FireDimClearCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) { message = "No active document."; return Result.Failed; }
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            // 1) witness ticks: collect CurveElement (native type), then filter DetailCurve
            List<ElementId> ticks = new List<ElementId>();
            foreach (Element el in new FilteredElementCollector(doc, view.Id).OfClass(typeof(CurveElement)))
            {
                DetailCurve dc = el as DetailCurve;
                if (dc == null) continue;
                try
                {
                    Element style = dc.LineStyle as Element;
                    if (style != null && style.Name == "Fire Dim Witness") ticks.Add(dc.Id);
                }
                catch { }
            }
            HashSet<ElementId> tickIds = new HashSet<ElementId>(ticks);

            // 2) dimensions that reference those ticks
            List<ElementId> dims = new List<ElementId>();
            foreach (Element el in new FilteredElementCollector(doc, view.Id).OfClass(typeof(Dimension)))
            {
                Dimension dim = el as Dimension;
                if (dim == null) continue;
                try
                {
                    foreach (Reference r in dim.References)
                    {
                        if (tickIds.Contains(r.ElementId)) { dims.Add(dim.Id); break; }
                    }
                }
                catch { }
            }

            if (dims.Count == 0 && ticks.Count == 0)
            {
                TaskDialog.Show("Fire Dim Clear", "Nothing created by Fire Dim was found in this view.");
                return Result.Succeeded;
            }

            List<ElementId> all = new List<ElementId>();
            all.AddRange(dims);
            all.AddRange(ticks);

            using (Transaction tx = new Transaction(doc, "Fire Dim Clear"))
            {
                tx.Start();
                doc.Delete(all);
                tx.Commit();
            }

            TaskDialog.Show("Fire Dim Clear", string.Format(
                "Deleted {0} dimensions and {1} witness ticks from view {2}.", dims.Count, ticks.Count, view.Title));
            return Result.Succeeded;
        }
    }
}