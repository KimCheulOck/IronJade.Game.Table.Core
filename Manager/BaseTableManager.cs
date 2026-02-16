using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class BaseTableManager
{
    public static string LocalPath
    {
        get
        {
            if (localPath == null)
            {
                string devicePath = Application.dataPath.Replace("/Assets", "");
                localPath = $"{devicePath}/Table/ClientJson/";
            }

            return localPath;
        }
    }

    private static List<string> forbiddenWord = new List<string>();
    private static string localPath;

    public static string GetForbiddenWord(string word)
    {
        return Regex.Replace(word, @"[^¤¡-¤¾¤¿-¤Ó°¡-ÆRa-zA-Z0-9]", "", RegexOptions.Singleline);
    }

    public static string GetNicknamForbiddenEndWord(string word)
    {
        return Regex.Replace(word, @"[^°¡-ÆRa-zA-Z0-9]", "", RegexOptions.Singleline);
    }

    public static bool CheckForbiddenWord(string word, out string result)
    {
        for (int i = 0; i < forbiddenWord.Count; ++i)
        {
            if (word.IndexOf(forbiddenWord[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result = forbiddenWord[i];
                return true;
            }
        }

        result = string.Empty;
        return false;
    }
}