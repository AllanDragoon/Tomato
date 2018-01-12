using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;

namespace LS.MapClean.Addin.Palettes
{
    class AllPaletteSets
    {
        static PaletteSetBase[] _allPaletteSets;

        public static void InitPaletteSets()
        {
            CreateAllPaletteSets();
            RestoreVisibility(Application.DocumentManager.MdiActiveDocument);
        }

        private static void CreateAllPaletteSets()
        {
            Array paletteSetTypes = Enum.GetValues(typeof(PaletteSetType));
            _allPaletteSets = new PaletteSetBase[paletteSetTypes.Length];

            foreach (PaletteSetType type in paletteSetTypes)
            {
                PaletteSetBase paletteSet;
                switch (type)
                {
                    case PaletteSetType.MapClean:
                        paletteSet = new MapCleanPaletteSet();
                        break;
                    case PaletteSetType.ActionSequence:
                        paletteSet = new ActionSequencePaletteSet();
                        break;
                    default:
                        throw new InvalidOperationException("Invalid palette set type: " + type.ToString());
                }

                _allPaletteSets[(int)type] = paletteSet;
            }
        }

        public static void DisposePaletteSets()
        {
            if (_allPaletteSets == null) return;

            // Dispose of any paletteSet objects that need to be disposed (i.e., they implement IDisposable)
            foreach (var disposable in _allPaletteSets.OfType<IDisposable>())
                disposable.Dispose();

            _allPaletteSets = null;
        }

        /// <summary>
        /// Restore the saved visibility state of the specified tool palette(s).
        /// If forceDisplay is set, then want to ignore the saved state and show anyway.
        /// </summary>
        /// <param name="paletteSetsToShow"></param>
        /// <param name="doc"></param>
        /// <param name="forceDisplay"></param>
        public static void RestoreVisibility(PaletteSetType[] paletteSetsToShow, Document doc)
        {
            // Show any palette set whose state variable is on
            foreach (var whichPalette in paletteSetsToShow)
            {
                var paletteSet = GetPaletteSet(whichPalette);
                RestoreVisibility(paletteSet, doc);
            }
        }

        public static void RestoreVisibility(Document doc)
        {
            RestoreVisibility(_allPaletteSets, doc);
        }

        private static void RestoreVisibility(IEnumerable<PaletteSetBase> paletteSets, Document doc)
        {
            foreach (var paletteSet in paletteSets)
            {
                RestoreVisibility(paletteSet, doc);
            }
        }

        private static void RestoreVisibility(PaletteSetBase paletteSet, Document doc)
        {
            if (paletteSet.HasVisibleState())
            {
                paletteSet.Show(doc);
            }
        }

        public static PaletteSetBase GetPaletteSet(PaletteSetType whichPaletteSet)
        {
            var retval = _allPaletteSets[(int)whichPaletteSet];
            if (retval == null)
                throw new InvalidOperationException(); // should have all been created in InitPaletteSets()

            return retval;
        }

        public static TPaletteSet GetPaletteSet<TPaletteSet>() where TPaletteSet : PaletteSetBase
        {
            Type paletteSetType = typeof(TPaletteSet);

            if (paletteSetType == typeof(MapCleanPaletteSet))
                return (TPaletteSet)GetPaletteSet(PaletteSetType.MapClean);
            else if (paletteSetType == typeof (ActionSequencePaletteSet))
                return (TPaletteSet) GetPaletteSet(PaletteSetType.ActionSequence);

            throw new InvalidOperationException(); // should have all been created in InitPaletteSets()
        }

        /// <summary>
        /// Show the palette by palette type and document.
        /// </summary>
        /// <param name="paletteSetType"></param>
        /// <param name="document"></param>
        /// <returns></returns>
        public static PaletteSetBase DisplayPaletteSet(PaletteSetType paletteSetType, Document document)
        {
            PaletteSetBase paletteSet = null;

            if (_allPaletteSets == null)
            {
                var paletteSetTypes = Enum.GetValues(typeof(PaletteSetType));
                _allPaletteSets = new PaletteSetBase[paletteSetTypes.Length];
            }
            else
            {
                paletteSet = _allPaletteSets[(int)paletteSetType];
            }

            if (paletteSet == null)
            {
                switch (paletteSetType)
                {
                    case PaletteSetType.MapClean:
                        paletteSet = new MapCleanPaletteSet();
                        break;
                    case PaletteSetType.ActionSequence:
                        paletteSet = new ActionSequencePaletteSet();
                        break;
                    default:
                        return null;
                }

                _allPaletteSets[(int)paletteSetType] = paletteSet;
            }

            paletteSet.Show(document);
            return paletteSet;
        }

        public static void ClosePaletteSet(PaletteSetType paletteSetType)
        {
            if (_allPaletteSets == null || _allPaletteSets.Length <= (int)paletteSetType)
                return;

            var paletteSet = _allPaletteSets[(int)paletteSetType];
            if (paletteSet != null)
                paletteSet.Hide(temporary:true);
        }

        /// <summary>
        /// Temporary close, will need to show the palettes after newing a document
        /// </summary>
        public static void ClosePalettes()
        {
            foreach (var paletteSet in _allPaletteSets)
                paletteSet.Hide(true);
        }

        public static bool IsPaletteSetVisible(PaletteSetType paletteSetType)
        {
            bool result = false;
            if (_allPaletteSets == null || _allPaletteSets.Length <= (int)paletteSetType)
                return result;

            var paletteSet = _allPaletteSets[(int)paletteSetType];
            if (paletteSet != null && paletteSet.PaletteSet != null)
                result = paletteSet.PaletteSet.Visible;
            return result;
        }
    }
}
