using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace GameAi.EditorTools
{
    // 사전 생성 도구(7.22 부가 기능, S): 상황 키별 대사를 로컬 Qwen으로 미리 뽑아 검증한 뒤
    // barks.json에 source "pregen" 후보로 더한다. 실행 중에는 캐시에 없는 상황만 실시간 생성한다
    public static class BarkPregenerator
    {
        [System.Serializable] class ChatResponse { public Choice[] choices; }
        [System.Serializable] class Choice { public Message message; }
        [System.Serializable] class Message { public string content; }

        static readonly string[] Tensions = { "주의", "수색" };

        [MenuItem("Tools/Pregenerate Barks")]
        public static void Run()
        {
            var qwen = ConfigLoader.Load<QwenConfig>("qwen.json");
            if (!qwen.IsLocal)
            {
                Debug.LogError("[Pregen] qwen.json endpoint가 로컬 주소가 아닙니다.");
                return;
            }
            var events = new EventTypeTable(ConfigLoader.Load<EventTypeConfig>("events.json"));
            string path = Path.Combine(ConfigLoader.ConfigDir, "barks.json");
            var barks = ConfigLoader.Parse<BarkConfig>(File.ReadAllText(path), "barks.json");
            var added = new List<BarkEntry>();
            int ok = 0, fail = 0;
            var all = new List<EventTypeDef>(events.All);
            for (int e = 0; e < all.Count; e++)
            {
                EventTypeDef def = all[e];
                if (def.bark == "instant") continue;
                foreach (PersonaDef persona in barks.personas)
                    foreach (string tension in Tensions)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar("대사 사전 생성", def.name_ko + " / " + persona.label, (float)e / all.Count)) goto done;
                        var context = new BarkContext
                        {
                            Event = def.name_ko, Category = def.CategoryKo, Route = "직접", Direction = "앞", Distance = "중간", When = "방금",
                            Repeat = "처음", Role = "경비병", Persona = persona.label, PersonaStyle = persona.style, Task = "순찰", Tension = tension,
                            Body = "멀쩡", Allies = "없음", Place = "실내", Light = "어두움", Wind = "없음", Alert = "평상", DefaultEmotion = def.default_emotion,
                        };
                        string content = Ask(qwen, persona, context);
                        QwenBarkSource.ParseOutput(content, def.default_emotion, out string line, out string emotion);
                        if (BarkValidator.Validate(line, context, barks.banned_words, null, null) != "OK") { fail++; continue; }
                        added.Add(new BarkEntry { when = def.code, lines = new[] { line }, emotion = emotion, tension = tension, persona = persona.id, source = "pregen" });
                        ok++;
                    }
            }
        done:
            EditorUtility.ClearProgressBar();
            var merged = new List<BarkEntry>();
            foreach (BarkEntry b in barks.barks) if (b.source != "pregen") merged.Add(b);
            merged.AddRange(added);
            barks.barks = merged.ToArray();
            File.WriteAllText(path, JsonUtility.ToJson(barks, true), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("[Pregen] 사전 생성 대사 " + ok + "개 저장, 검증 실패 " + fail + "개");
        }

        static string Ask(QwenConfig qwen, PersonaDef persona, BarkContext context)
        {
            string system = "너는 2D 잠입 게임 속 적 경비병이다. 성격: " + persona.label + "(" + persona.style + "). " +
                            "상황에 맞는 혼잣말 한 줄을 한국어 20자 이내로. 입력에 없는 정보는 말하지 않는다. 숫자·영어 금지. " +
                            "반드시 {\"line\":\"대사\",\"emotion\":\"의심|놀람|긴장|분노|안도\"} JSON으로만 답한다.";
            string body = "{\"model\":" + BarkContext.Quote(qwen.model) + ",\"max_tokens\":" + qwen.max_tokens + ",\"temperature\":0.8,\"stream\":false," +
                          "\"messages\":[{\"role\":\"system\",\"content\":" + BarkContext.Quote(system) + "},{\"role\":\"user\",\"content\":" + BarkContext.Quote(context.ToJson()) + "}]}";
            using (var web = new UnityWebRequest(qwen.endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                web.downloadHandler = new DownloadHandlerBuffer();
                web.SetRequestHeader("Content-Type", "application/json");
                web.timeout = 60;
                var op = web.SendWebRequest();
                while (!op.isDone) System.Threading.Thread.Sleep(10);
                if (web.result != UnityWebRequest.Result.Success) return "";
                try
                {
                    ChatResponse response = JsonUtility.FromJson<ChatResponse>(web.downloadHandler.text);
                    return response?.choices != null && response.choices.Length > 0 ? response.choices[0].message.content : "";
                }
                catch (System.ArgumentException) { return ""; }
            }
        }
    }
}
