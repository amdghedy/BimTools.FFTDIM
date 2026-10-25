using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimTools.FFTDIM.Core
{
    /// <summary>
    /// Detects duplicate dimensions — two dimensions that reference the exact same set of
    /// model geometry. Such pairs always measure the same distance and one of them is redundant.
    /// </summary>
    public static class RedundancyFinder
    {
        /// <summary>
        /// Groups dimensions by reference-set key and returns every group holding more than
        /// one dimension. Within each group the first element (lowest element id) is the
        /// suggested keeper; the rest are candidates for deletion.
        /// </summary>
        public static List<List<Dimension>> FindDuplicateGroups(IList<Dimension> dimensions)
        {
            var groups = new Dictionary<string, List<Dimension>>();
            if (dimensions == null) return new List<List<Dimension>>();

            foreach (Dimension dim in dimensions)
            {
                string key = DimensionScanner.GetReferenceKey(dim);
                if (key == null) continue;

                List<Dimension> bucket;
                if (!groups.TryGetValue(key, out bucket))
                {
                    bucket = new List<Dimension>();
                    groups[key] = bucket;
                }
                bucket.Add(dim);
            }

            return groups.Values.Where(g => g.Count > 1).ToList();
        }
    }
}
