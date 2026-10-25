using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace FFDimAddin2020
{
    [Transaction(TransactionMode.ReadOnly)]
    public class FireDimSettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            using (FireDimSettingsForm f = new FireDimSettingsForm())
            {
                f.ShowDialog();
            }
            return Result.Succeeded;
        }
    }
}