using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace GameAi
{
    // 이 표시가 붙은 필드는 JSON에 없어도 경고하지 않는다(선택 항목)
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class OptionalKeyAttribute : Attribute { }

    // StreamingAssets/Config의 JSON을 읽는다(5번 규칙: 수치는 코드에 박지 않는다).
    // 파일이나 값이 없으면 코드 안의 기본값을 쓰고 경고 로그를 남긴다.
    public static class ConfigLoader
    {
        public static string ConfigDir
        {
            get { return Path.Combine(Application.streamingAssetsPath, "Config"); }
        }

        // Config 폴더 기준 상대 경로의 JSON을 T로 읽는다. 파일이 없으면 기본값 객체
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

        // 형식을 바꾸지 않고 글자 그대로 읽을 때(맵 JSON, credits.txt). 없으면 null
        public static string ReadText(string relativePath)
        {
            string path = Path.Combine(ConfigDir, relativePath);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        // JSON 글자를 T로 바꾼다. label은 로그에 찍을 파일 이름
        public static T Parse<T>(string json, string label) where T : class, new()
        {
            // new T()로 기본값을 먼저 채우고 JSON에 있는 값만 덮어쓰므로, 빠진 값은 자동으로 기본값이 된다
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

        // JSON 글자에서 type의 필드 이름이 하나도 안 나오는 것을 찾는다(중첩 클래스는 "a.b" 경로로)
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

                // 값이 클래스면 안쪽 키도 확인한다(문자열·배열은 제외)
                Type t = field.FieldType;
                if (t.IsClass && t != typeof(string) && !t.IsArray && t.IsSerializable)
                    CollectMissing(json, t, path + ".", missing);
            }
        }
    }
}
