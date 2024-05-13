using System.Linq;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;

namespace Assistant.Controllers
{
    public static class LocalizationController
    {
        private static string currentLanguage { get; set; } = string.Empty;
        public enum Language { English, Spanish }

        // Link enum values to language codes
        private static readonly Dictionary<Language, string> Languages = new Dictionary<Language, string>
        {
            { Language.English, "en-US" },
            { Language.Spanish, "es-ES" }
        };

        /// <summary>
        /// Changes the current thread's UI culture to the one in @currentLanguage 
        /// if it is not empty, otherwise grabs it from the settings. 
        /// Optionally saves @currentLanguage to the settings
        /// </summary>
        /// <param name="save"></param>
        public static void InitializeLocale(bool save = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(currentLanguage))
                    currentLanguage = Properties.Settings.Default.LanguageCode;

                if (!string.IsNullOrWhiteSpace(currentLanguage))
                {
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(currentLanguage);

                    if (save)
                    {
                        Properties.Settings.Default.LanguageCode = currentLanguage;
                        Properties.Settings.Default.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in InitializeLocale: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the @currentLanguage
        /// </summary>
        /// <returns></returns>
        public static string GetLanguage()
        {
            return currentLanguage;
        }

        /// <summary>
        /// Sets the @currentLanguage to a given language
        /// Defaults to English if the language has no key
        /// in the @Languages dictionary
        /// </summary>
        /// <param name="language"></param>
        /// <param name="save"></param>
        public static void SetLanguage(Language language, bool save = true)
        {
            try
            {
                if (Languages.ContainsKey(language))
                {
                    currentLanguage = Languages[language];
                    InitializeLocale(save);
                }
                else
                {
                    currentLanguage = Languages[Language.English];
                    InitializeLocale(save);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SetLanguage: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns a string representation of the current
        /// language found in the @Languages dictionary
        /// based on a given language code
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static string GetLanguageFromCode(string code)
        {
            try
            {
                var language = Languages.FirstOrDefault(x => x.Value == code).Key;
                return language.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetLanguageFromCode: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns the language code corresponding
        /// to the given language if it is found in
        /// the @Languages dictionary. Defaults to English
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public static string GetCodeFromLanguage(Language language)
        {
            try
            {
                if (Languages.ContainsKey(language))
                    return Languages[language];
                else
                    return Languages[Language.English];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCodeFromLanguage: {ex.Message}");
                return null;
            }
        }
    }
}
