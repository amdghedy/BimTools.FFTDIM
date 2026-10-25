using System;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.UI;

namespace FFDimAddin2020
{
    public class FireDimApp : IExternalApplication
    {
        // ============================================================
        // RIBBON CONFIGURATION
        // ============================================================

        private const string TabName = "BIM TOOLS";
        private const string PanelName = "Dim's Tool";

        // ============================================================
        // STARTUP
        // ============================================================

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // ----------------------------------------------------
                // 1. Create the ribbon tab if it does not already exist
                // ----------------------------------------------------

                try
                {
                    application.CreateRibbonTab(TabName);
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    // Tab already exists.
                }

                // ----------------------------------------------------
                // 2. Get existing panel or create it
                // ----------------------------------------------------

                RibbonPanel panel = GetOrCreateRibbonPanel(
                    application,
                    TabName,
                    PanelName);

                // ----------------------------------------------------
                // 3. Get the current assembly path
                // ----------------------------------------------------

                string path = Assembly.GetExecutingAssembly().Location;

                // ----------------------------------------------------
                // 4. Fire Dimension button
                // ----------------------------------------------------

                PushButtonData btnDim = new PushButtonData(
                    "FireDimButton",
                    "Fire Dim",
                    path,
                    typeof(FireDimCommand).FullName);

                btnDim.ToolTip =
                    "Dimensions for firefighting pipes in the ACTIVE 2D floor plan only.";

                btnDim.AvailabilityClassName =
                    typeof(FireDimAvailability).FullName;

                AddButtonIfNotExists(panel, btnDim);

                // ----------------------------------------------------
                // 5. Settings button
                // ----------------------------------------------------

                PushButtonData btnSet = new PushButtonData(
                    "FireDimSettingsButton",
                    "Settings",
                    path,
                    typeof(FireDimSettingsCommand).FullName);

                btnSet.ToolTip =
                    "Fire Dim options (filter, sequences, offsets). Saved between sessions.";

                AddButtonIfNotExists(panel, btnSet);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "FFDimAddin2020",
                    "Failed to initialize the Fire Dim add-in.\n\n" +
                    ex.Message);

                return Result.Failed;
            }
        }

        // ============================================================
        // SHUTDOWN
        // ============================================================

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        // ============================================================
        // GET OR CREATE RIBBON PANEL
        // ============================================================

        private RibbonPanel GetOrCreateRibbonPanel(
            UIControlledApplication application,
            string tabName,
            string panelName)
        {
            // Look for an existing panel with the requested name.
            RibbonPanel existingPanel = application
                .GetRibbonPanels(tabName)
                .FirstOrDefault(p =>
                    string.Equals(
                        p.Name,
                        panelName,
                        StringComparison.OrdinalIgnoreCase));

            // Panel already exists.
            if (existingPanel != null)
            {
                return existingPanel;
            }

            // Panel does not exist, so create it.
            return application.CreateRibbonPanel(
                tabName,
                panelName);
        }

        // ============================================================
        // ADD BUTTON ONLY IF IT DOES NOT ALREADY EXIST
        // ============================================================

        private void AddButtonIfNotExists(
            RibbonPanel panel,
            PushButtonData buttonData)
        {
            bool exists = panel
                .GetItems()
                .Any(item =>
                    string.Equals(
                        item.Name,
                        buttonData.Text,
                        StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                panel.AddItem(buttonData);
            }
        }
    }

    // ================================================================
    // COMMAND AVAILABILITY
    // ================================================================

    public class FireDimAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(
            UIApplication app,
            Autodesk.Revit.DB.CategorySet categories)
        {
            try
            {
                if (app == null)
                    return false;

                if (app.ActiveUIDocument == null)
                    return false;

                Autodesk.Revit.DB.View view =
                    app.ActiveUIDocument.Document.ActiveView;

                if (view == null)
                    return false;

                if (view.IsTemplate)
                    return false;

                return view.ViewType ==
                       Autodesk.Revit.DB.ViewType.FloorPlan;
            }
            catch
            {
                return false;
            }
        }
    }
}