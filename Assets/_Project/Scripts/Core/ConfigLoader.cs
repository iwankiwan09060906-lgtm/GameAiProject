using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace GameAi
{
    // 이 표시가 붙은 필드는 JSON에 없어도 경고하지 않는다
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class OptionalKeyAttribute : Attribute { }

    // StreamingAssets/Config의 JSON을 읽는다. 파일이나 값이 없으면 기본값 + 경고 로그.
    public static class ConfigLoader
    {
        public static string ConfigDir
        {
            get { return Path.Combine(Application.streamingAssetsPath, "Config"); }
        }

        public static T Load<T>(string relativePath) where T : class, new()
        {
            string path = Path.Combine(ConfigDir, relativePath);
            if (!File.Exists(path))
            {
                Debug.LogWarning("[Config] 파일이 없어 기본값을 사용합니다: " + relativePath);
                return new T();
            }
            return Parse<T>(File.ReadAllText(path), relativePath);
        }

        public static string ReadText(string relativePath)
        {
            string path = Path.Combine(ConfigDir, relativePath);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        public static T Parse<T>(string json, string label) where T : class, new()
        {
            var result = new T();
            try
            {
                JsonUtility.FromJsonOverwrite(json, result);
            }
            catch (ArgumentException e)
            {
                Debug.LogError("[Config] JSON 형식 오류, 기본값을 사용합니다: " + label + " / " + e.Message);
                return new T();
            }
            foreach (string key in FindMissingKeys(json, typeof(T)))
                Debug.LogWarning("[Config] 값이 없어 기본값을 사용합니다: " + label + " / " + key);
            return result;
        }

        public static List<string> FindMissingKeys(string json, Type type)
        {
            var missing = new List<string>();
            CollectMissing(json, type, "", missing);
            return missing;
        }

        static void CollectMissing(string json, Type type, string prefix, List<string> missing)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.GetCustomAttribute<OptionalKeyAttribute>() != null) continue;
                string path = prefix + field.Name;
                if (!json.Contains("\"" + field.Name + "\""))
                {
                    missing.Add(path);
                    continue;
                }
                Type t = field.FieldType;
                if (t.IsClass && t != typeof(string) && !t.IsArray && t.IsSerializable)
                    CollectMissing(json, t, path + ".", missing);
            }
        }
    }
}
