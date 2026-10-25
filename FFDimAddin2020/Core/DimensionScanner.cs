using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace BimTools.FFTDIM.Core
{
    /// <summary>
    /// Collects and inspects Dimension elements in a view. Kept free of issue logic so
    /// both the auditor and the redundancy finder can share the same primitives.
    /// </summary>
    public static class DimensionScanner
    {
        /// <summary>Values smaller than this (≈ 0.5 mm) are treated as zero-length.</summary>
        public const double ZeroValueToleranceFeet = 0.00164042;

        /// <summary>Collects all Dimension elements visible in the given view.</summary>
        public static IList<Dimension> Collect(Document doc, ElementId viewId)
        {
            return new FilteredElementCollector(doc, viewId)
                .OfClass(typeof(Dimension))
                .Cast<Dimension>()
                .Where(d => d != null)
                .ToList();
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
