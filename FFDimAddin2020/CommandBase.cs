using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Exceptions;

namespace BimTools.FFTDIM
{
    /// <summary>
    /// Base class for all FFTDIM external commands. Centralises:
    ///  - the "no document open" guard,
    ///  - cancellation of user-initiated aborts,
    ///  - crash reporting through the Revit failure message.
    /// </summary>
    public abstract class CommandBase : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    TaskDialog.Show(App.Brand, "Please open a model first.");
                    return Result.Cancelled;
                }
                return OnExecute(commandData, uidoc, ref message);
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (System.Exception ex)
            {
                message = ex.Message + Environment.NewLine + ex.StackTrace;
                return Result.Failed;
            }
        }

        protected abstract Result OnExecute(ExternalCommandData commandData, UIDocument uidoc, ref string message);
    }

    /// <summary>View suitability guards shared by the commands.</summary>
    internal static class ViewGuard
    {
        public static bool IsGraphicalView(View view, out string reason)
        {
            if (view == null)
            {
                reason = "There is no active view.";
                return false;
            }
            switch (view.ViewType)
            {
                case ViewType.Schedule:
                case ViewType.DrawingSheet:
                case ViewType.ColumnSchedule:
                case ViewType.Legend:
                    reason = string.Format(
                        "The active view type '{0}' cannot contain model dimensions. " +
                        "Open a plan, section, elevation or drafting view and run the command again.",
                        view.ViewType);
                    return false;
            }
            reason = null;
            return true;
        }

        public static bool IsPlanView(View view, out string reason)
        {
            if (!IsGraphicalView(view, out reason)) return false;
            if (!(view is ViewPlan))
            {
                reason = "This command works in plan views only. Switch to a floor or ceiling plan and try again.";
                return false;
            }
            return true;
        }
    }
}
