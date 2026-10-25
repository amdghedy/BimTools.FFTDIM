using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace BimTools.FFTDIM.Core
{
    /// <summary>
    /// Identifies fire-fighting elements in a model using a combination of:
    /// 1. Revit categories (sprinklers, pipes, fittings, mechanical equipment, generic models);
    /// 2. Piping system classification (Revit's "FireProtect*" classifications);
    /// 3. The piping system connected through the element's connectors;
    /// 4. Keyword heuristics on family/type names ("sprinkler", "hydrant", "hose", "fire", "spk").
    ///
    /// The heuristics are deliberately conservative and documented in the README; they may
    /// over- or under-select in projects with unusual naming. Results should be spot-checked.
    /// </summary>
    public static class FireFightingFilter
    {
        private static readonly string[] EquipmentKeywords = { "fire pump", "jockey", "booster", "hydrant", "fire" };
        private static readonly string[] GenericKeywords = { "sprinkler", "hydrant", "hose reel", "fire", "spk" };
        private static readonly string[] PipeKeywords = { "sprinkler", "hydrant", "hose", "fire" };

        /// <summary>Collects all fire-fighting elements visible in the given view.</summary>
        public static List<FireElementInfo> CollectInView(Document doc, View view)
        {
            var categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Sprinklers,
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_PipeFittings,
                BuiltInCategory.OST_PipeInsulations,
                BuiltInCategory.OST_MechanicalEquipment,
                BuiltInCategory.OST_GenericModel,
                BuiltInCategory.OST_SpecialityEquipment
            };

            var categoryFilter = new ElementMulticategoryFilter(categories);
            var results = new List<FireElementInfo>();

            foreach (Element element in new FilteredElementCollector(doc, view.Id)
                         .WhereElementIsNotElementType()
                         .WherePasses(categoryFilter))
            {
                if (element == null || !IsFireFighting(element)) continue;
                results.Add(Describe(element));
            }

            return results;
        }

        /// <summary>Builds a descriptive record for one element.</summary>
        public static FireElementInfo Describe(Element element)
        {
            var instance = element as FamilyInstance;
            var mepCurve = element as MEPCurve;

            string system = null;
            Parameter classification = element.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_CLASSIFICATION);
            if (classification != null) system = classification.AsString();
            if (string.IsNullOrEmpty(system) && mepCurve != null && mepCurve.MEPSystem != null)
            {
                system = mepCurve.MEPSystem.Name;
            }

            return new FireElementInfo
            {
                Id = element.Id,
                Name = element.Name,
                Category = element.Category != null ? element.Category.Name : "Element",
                Family = instance != null && instance.Symbol != null && instance.Symbol.Family != null
                    ? instance.Symbol.Family.Name
                    : "-",
                Type = instance != null && instance.Symbol != null ? instance.Symbol.Name : element.Name,
                System = string.IsNullOrEmpty(system) ? "-" : system,
                IsDimensioned = false
            };
        }

        /// <summary>Decides whether one element belongs to the fire-fighting domain.</summary>
        public static bool IsFireFighting(Element element)
        {
            if (element == null || element.Category == null) return false;
            int categoryId = element.Category.Id.IntegerValue;

            if (categoryId == (int)BuiltInCategory.OST_Sprinklers) return true;

            if (categoryId == (int)BuiltInCategory.OST_PipeCurves ||
                categoryId == (int)BuiltInCategory.OST_PipeFittings ||
                categoryId == (int)BuiltInCategory.OST_PipeInsulations)
            {
                return IsFirePiping(element);
            }

            if (categoryId == (int)BuiltInCategory.OST_MechanicalEquipment)
            {
                return MatchesKeyword(element, EquipmentKeywords);
            }

            if (categoryId == (int)BuiltInCategory.OST_GenericModel ||
                categoryId == (int)BuiltInCategory.OST_SpecialityEquipment)
            {
                return MatchesKeyword(element, GenericKeywords);
            }

            return false;
        }

        private static bool IsFirePiping(Element element)
        {
            // 1. Piping system classification (Revit uses "FireProtectWet", "FireProtectDry", ...).
            Parameter classification = element.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_CLASSIFICATION);
            if (classification != null)
            {
                string text = classification.AsString();
                if (!string.IsNullOrEmpty(text) && text.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            // 2. System name on curve elements.
            var mepCurve = element as MEPCurve;
            if (mepCurve != null && mepCurve.MEPSystem != null &&
                mepCurve.MEPSystem.Name.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // 3. Connected system on fittings (the fitting itself may not carry a classification).
            var instance = element as FamilyInstance;
            if (instance != null && instance.MEPModel != null && instance.MEPModel.ConnectorManager != null)
            {
                try
                {
                    foreach (Connector connector in instance.MEPModel.ConnectorManager.Connectors)
                    {
                        if (connector == null || connector.Domain != Domain.DomainPiping) continue;
                        MEPSystem system = connector.MEPSystem;
                        if (system != null && system.Name.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    // ConnectorManager can throw for unusual families; keyword check still applies.
                }
            }

            // 4. Family / type name keywords.
            return MatchesKeyword(element, PipeKeywords);
        }

        private static bool MatchesKeyword(Element element, string[] keywords)
        {
            string familyName = null;
            var instance = element as FamilyInstance;
            if (instance != null && instance.Symbol != null && instance.Symbol.Family != null)
            {
                familyName = instance.Symbol.Family.Name;
            }

            string[] names = { element.Name, familyName };
            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name)) continue;
                foreach (string keyword in keywords)
                {
                    if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }
            return false;
        }
    }
}
