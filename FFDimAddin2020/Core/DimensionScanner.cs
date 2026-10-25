using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimTools.FFTDIM.Core
{
    /// <summary>
    /// Collects and inspects Dimension elements in a view. Kept free of issue logic so
    /// both the auditor and the redundancy finder can share the same primitives.
    /// </summary>
    public static class DimensionScanner
    {
        /// <summary>
        /// Default zero-value tolerance (≈ 0.5 mm). Used when no <see cref="AppSettings"/>
        /// is supplied; the auditor otherwise derives the tolerance from the user settings.
        /// </summary>
        public const double ZeroValueToleranceFeet = 0.00164042;

        /// <summary>Collects all Dimension elements visible in the given view (spot dimensions excluded).</summary>
        public static IList<Dimension> Collect(Document doc, ElementId viewId)
        {
            return Collect(doc, viewId, false);
        }

        /// <summary>
        /// Collects Dimension elements visible in the given view. Spot dimensions (spot
        /// elevations and spot coordinates) are only included when the user opted in via
        /// settings — their fixed zero-ish values would otherwise flood the zero-value and
        /// duplicate checks with noise.
        /// </summary>
        public static IList<Dimension> Collect(Document doc, ElementId viewId, bool includeSpotDimensions)
        {
            IEnumerable<Dimension> query = new FilteredElementCollector(doc, viewId)
                .OfClass(typeof(Dimension))
                .Cast<Dimension>()
                .Where(d => d != null);

            if (!includeSpotDimensions)
            {
                query = query.Where(d => !(d is SpotDimension));
            }

            return query.ToList();
        }

        /// <summary>
        /// True when at least one reference of the dimension resolves into a linked
        /// Revit model. Such references point at the RevitLinkInstance element, so the
        /// nested geometry cannot be analysed in detail by this tool.
        /// </summary>
        public static bool ReferencesLinkedModel(Document doc, Dimension dim)
        {
            if (doc == null || dim == null) return false;
            foreach (ElementId referenceId in GetReferenceElementIds(dim))
            {
                if (doc.GetElement(referenceId) is RevitLinkInstance) return true;
            }
            return false;
        }

        /// <summary>Returns the model element ids behind a dimension's references.</summary>
        public static List<ElementId> GetReferenceElementIds(Dimension dim)
        {
            var ids = new List<ElementId>();
            if (dim == null || dim.References == null) return ids;

            foreach (Reference reference in dim.References)
            {
                if (reference == null) continue;
                ElementId id = reference.ElementId;
                if (id != null && id != ElementId.InvalidElementId) ids.Add(id);
            }
            return ids;
        }

        /// <summary>
        /// Stable key for duplicate detection: the sorted set of referenced element ids.
        /// Two dimensions with the same key measure the same geometry.
        /// Returns null when the dimension has no usable references.
        /// </summary>
        public static string GetReferenceKey(Dimension dim)
        {
            List<ElementId> ids = GetReferenceElementIds(dim);
            if (ids.Count == 0) return null;

            var parts = new List<int>(ids.Count);
            foreach (ElementId id in ids) parts.Add(id.IntegerValue);
            parts.Sort();
            return string.Join("|", parts);
        }

        /// <summary>All numeric values of a dimension: the single value, or every segment value.</summary>
        public static List<double> GetValues(Document doc, Dimension dim)
        {
            var values = new List<double>();
            if (dim == null) return values;

            if (dim.Value.HasValue)
            {
                values.Add(dim.Value.Value);
            }
            else if (dim.Segments != null)
            {
                foreach (DimensionSegment segment in dim.Segments)
                {
                    if (segment != null && segment.Value.HasValue) values.Add(segment.Value.Value);
                }
            }
            return values;
        }
    }
}
