using System.Collections.Generic;
using UnityEngine;

namespace Dino.Species
{
    /// <summary>
    /// The list of all playable species. One asset at Assets/_Game/Resources/SpeciesCatalog.asset,
    /// loaded with SpeciesCatalog.Load(). Add a new species = create its asset and add it here.
    /// </summary>
    [CreateAssetMenu(menuName = "Dino/Species Catalog", fileName = "SpeciesCatalog")]
    public class SpeciesCatalog : ScriptableObject
    {
        public List<SpeciesDefinition> species = new List<SpeciesDefinition>();

        public SpeciesDefinition Find(string id)
        {
            for (int i = 0; i < species.Count; i++)
                if (species[i] != null && species[i].id == id) return species[i];
            return null;
        }

        public IEnumerable<SpeciesDefinition> Sorted()
        {
            var list = new List<SpeciesDefinition>(species);
            list.RemoveAll(s => s == null);
            list.Sort((a, b) => a.sortOrder != b.sortOrder ? a.sortOrder.CompareTo(b.sortOrder)
                                                           : string.CompareOrdinal(a.id, b.id));
            return list;
        }

        static SpeciesCatalog _cached;
        public static SpeciesCatalog Load()
        {
            if (_cached == null) _cached = Resources.Load<SpeciesCatalog>("SpeciesCatalog");
            return _cached;
        }
    }
}
