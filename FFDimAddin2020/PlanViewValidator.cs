using Autodesk.Revit.DB;

namespace FFDimAddin2020
{
    internal static class PlanViewValidator
    {
        public static bool IsUsableFloorPlan(View view, out string reason)
        {
            reason = "";
            if (view == null) { reason = "No active view."; return false; }
            if (view.IsTemplate) { reason = "Active view is a template."; return false; }
            if (view.ViewType != ViewType.FloorPlan) { reason = "Active view is not a Floor Plan."; return false; }

            try
            {
                if (view.GetCategoryHidden(new ElementId(BuiltInCategory.OST_PipeCurves)))
                { reason = "Pipes category is hidden in this view."; return false; }
            }
            catch { /* category check not critical */ }
            return true;
        }
    }
}