using Autodesk.AutoCAD.ApplicationServices;
using LS.MapClean.Addin.Palettes;

namespace LS.MapClean.Addin.Main
{
    class DocumentEventHandlers
    {
        public static void RegisterDocumentEvents()
        {
            var docMgr = Application.DocumentManager;
            docMgr.DocumentActivated += OnDocumentActivated;
            docMgr.DocumentBecameCurrent += OnDocumentBecameCurrent;
            docMgr.DocumentDestroyed += OnDocumentDestroyed;
        }

        public static void UnregisterDocumentEvents()
        {
            var docMgr = Application.DocumentManager;
            docMgr.DocumentActivated -= OnDocumentActivated;
            docMgr.DocumentBecameCurrent -= OnDocumentBecameCurrent;
            docMgr.DocumentDestroyed -= OnDocumentDestroyed;
        }

        static void OnDocumentActivated(object sender, DocumentCollectionEventArgs args)
        {
            // Do nothing so far.
        }

        static void OnDocumentBecameCurrent(object sender, DocumentCollectionEventArgs args)
        {
            if (args.Document != null)
            {
                var paletteSetTypes = new PaletteSetType[] { PaletteSetType.MapClean };
                AllPaletteSets.RestoreVisibility(paletteSetTypes, args.Document);
            }
            else
            {
                AllPaletteSets.ClosePalettes();
            }
        }

        static void OnDocumentDestroyed(object sender, DocumentDestroyedEventArgs args)
        {
            // If there are many documents, and one of them is about to be closed, the MdiActiveDocument is null.
            // We should test the Count, if the value is 1, that means the last document is going to close.
            if (Application.DocumentManager.Count == 1)
            {
                AllPaletteSets.ClosePalettes();
            }
        }
    }
}
