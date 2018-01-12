using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.EditorInput;

namespace LS.MapClean.Addin.Utils
{
    /// <summary>
    /// Utility class to contain methods related to AutoCAD prompt
    /// </summary>
    internal static class AcadPromptUtil
    {
        // FDS-3524: Can't specify basepoint by context menu when place an 2D asset on Japanese build.
        // For Asian languages, we need to put short-cut character in a pair of brackets following the corresponding keywords.
        // For other languages, each localKeyword is supposed to contain one capital letter which works as the shortcut.
        public static TPromptOptions CreatePromptOptions<TPromptOptions>(string message, string[] globalKeywords, string[] localKeywords, char[] defaultShortcut)
            where TPromptOptions : PromptOptions
        {
            Debug.Assert(globalKeywords != null && localKeywords != null && defaultShortcut != null
                && globalKeywords.Count() == localKeywords.Count() && localKeywords.Count() == defaultShortcut.Count());

            TPromptOptions options;

            Autodesk.AutoCAD.Runtime.DynamicLinker dLinker = Autodesk.AutoCAD.Runtime.SystemObjects.DynamicLinker;
            var lcid = dLinker.ProductLcid;
            var cur = new CultureInfo(lcid);
            var langName = cur.ThreeLetterWindowsLanguageName;

            var isSupportedAnsianLanguage = false;
            switch (langName.ToUpperInvariant())
            {
                // If current locale is one of the supported Ansian language.
                case /*MSG0*/"CHS":
                case /*MSG0*/"CHT":
                case /*MSG0*/"JPN":
                case /*MSG0*/"KOR":
                    {
                        isSupportedAnsianLanguage = true;
                        break;
                    }
                default:
                    break;
            }

            if (isSupportedAnsianLanguage)
            {
                var messageAndKeywords = Environment.NewLine + message;

                // Fix regression caused by CL#193711 (in Eastman SP1) and CL#193802 (in Main branch) which are attempts to fix FDS-3524.
                // In Japanese build, in "FACT_CreateRouting" command, on picking "next station", there is no keywords to be added, so a pair of "[]" with empty string inside will be added.
                // That can cause an exception.
                // We need to skip adding keywords routine if there are no keywords to be added.
                if (localKeywords.Any())
                {
                    messageAndKeywords += (/*MSG0*/" [" + BuildAsianKeywordString(localKeywords, defaultShortcut) + /*MSG0*/"]");

                    // In AutoCAD prompt API, keywords are separated with blank.
                    options = (TPromptOptions)Activator.CreateInstance(typeof(TPromptOptions), new object[] { messageAndKeywords, CombineGlobalKeywords(globalKeywords) });
                }
                else
                {
                    // In AutoCAD prompt API, keywords are separated with blank.
                    options = (TPromptOptions)Activator.CreateInstance(typeof(TPromptOptions), new object[] { messageAndKeywords });
                }
            }
            else
            {
                // For non-Asian languages, a localKeyword is supposed to contain capital letter which can work as the shortcut.

                options = (TPromptOptions)Activator.CreateInstance(typeof(TPromptOptions), new object[] { Environment.NewLine + message });

                for (int i = 0; i < globalKeywords.Count(); ++i)
                {
                    options.Keywords.Add(globalKeywords[i], localKeywords[i], localKeywords[i]);
                }
            }

            return options;
        }

        private static string BuildAsianKeywordString(string[] localKeywords, char[] shortcuts)
        {
            Debug.Assert(localKeywords != null && shortcuts != null && localKeywords.Count() == shortcuts.Count());

            // The result messageAndKeywords for Japanese should be something like: "円弧(A)/2分の1幅(H)/長さ(L)/元に戻す(U)/幅(W)"
            var messageAndKeywords = String.Empty;

            for (var i = 0; i < localKeywords.Count(); ++i)
            {
                var localKeyword = localKeywords[i];
                var shortcut = shortcuts[i];

                messageAndKeywords += localKeyword + /*MSG0*/"(" + shortcut + /*MSG0*/")";

                // if this isn't the last keyword to process, we need to add a "/" as a separator.
                if (i != localKeywords.Count() - 1)
                {
                    messageAndKeywords += /*MSG0*/"/";
                }
            }

            return messageAndKeywords;
        }

        private static string CombineGlobalKeywords(string[] globalKeywords)
        {
            var combinedKeywords = String.Empty;

            for (var i = 0; i < globalKeywords.Count(); ++i)
            {
                var globalKeyword = globalKeywords[i];
                combinedKeywords += globalKeyword;

                if (i != globalKeywords.Count() - 1)
                {
                    // Separate keywords with blank.
                    combinedKeywords += /*MSG0*/" ";
                }
            }

            return combinedKeywords;
        }

        public static bool AskContinue(string message, Editor editor)
        {
            var options = AcadPromptUtil.CreatePromptOptions<PromptKeywordOptions>(message, new string[] { "Yes", "No" },
                new string[] { "是", "否" }, new char[] { 'Y', 'N' });
            options.AllowNone = true;
            PromptResult promptResult = null;
            do
            {
                promptResult = editor.GetKeywords(options);
            } while (promptResult.Status != PromptStatus.OK
                && promptResult.Status != PromptStatus.Cancel
                && promptResult.Status != PromptStatus.None);

            if (promptResult.Status == PromptStatus.Cancel)
                return false;

            if (promptResult.Status == PromptStatus.OK && promptResult.StringResult == "No")
                return false;

            return true;
        }
    }
}
