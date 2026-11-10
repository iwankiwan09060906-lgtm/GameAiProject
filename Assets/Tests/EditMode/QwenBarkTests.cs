using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GameAi.Tests
{
    // 상황 상태값·응답 검증(7.22)
    public class QwenBarkTests
    {
        static BarkContext Context(string direction = "앞", string callsign = null)
        {
            var c = new BarkContext { Event = "문소리", Category = "소리", Direction = direction, Distance = "중간", When = "방금", Persona = "결단한 고참", Task = "순찰", Tension = "주의" };
            if (callsign != null) c.Detail.Add(new KeyValuePair<string, string>("동료 이름", callsign));
            return c;
        }

        static string V(string line, BarkContext c = null, IList<string> recent = null)
        {
            return BarkValidator.Validate(line, c ?? Context(), new[] { "AI", "모델", "플레이어", "게임" }, recent, null);
        }

        [Test] public void 정상_대사는_통과() { Assert.AreEqual("OK", V("문 쪽이다. 확인한다.")); }
        [Test] public void 빈_대사() { Assert.AreEqual("EMPTY", V("")); }
        [Test] public void 이십자_초과() { Assert.AreEqual("TOO_LONG", V("이건 정말로 너무 길어서 이십 자를 넘는 대사다")); }
        [Test] public void 이십자는_통과() { Assert.AreEqual("OK", V("가나다라마바사아자차카타파하가나다라마바")); }
        [Test] public void 줄바꿈() { Assert.AreEqual("MULTI_LINE", V("뭐지?\n누구야")); }
        [Test] public void 영어나_숫자() { Assert.AreEqual("NOT_KOREAN", V("Who is there?")); Assert.AreEqual("NOT_KOREAN", V("의심도 47이야")); }
        [Test] public void 입력에_없는_방향() { Assert.AreEqual("LEAK", V("왼쪽에서 소리가!", Context("앞"))); Assert.AreEqual("OK", V("왼쪽에서 소리가!", Context("왼쪽"))); }
        [Test] public void 무기_이름() { Assert.AreEqual("LEAK", V("소총 소리였어")); }
        [Test] public void 호칭은_세부에_있을_때만() { Assert.AreEqual("LEAK", V("박 상병 어디 갔지?")); Assert.AreEqual("OK", V("박 상병? 정신 차려!", Context(callsign: "박 상병"))); }
        [Test] public void 금지어() { Assert.AreEqual("BANNED", V("이건 게임이잖아")); }
        [Test] public void 반복은_문장부호와_공백을_무시하고_버린다() { Assert.AreEqual("REPEAT", V("누구 있어?!", null, new List<string> { "누구 있어" })); }

        [Test]
        public void 따옴표와_생각_태그를_지운다()
        {
            Assert.AreEqual("뭐지?", BarkValidator.Clean("<think>음</think>\"뭐지?\""));
        }

        [Test]
        public void JSON_출력을_읽고_감정이_틀리면_기본_감정()
        {
            QwenBarkSource.ParseOutput("{\"line\":\"박 상병? 정신 차려!\",\"emotion\":\"놀람\"}", "의심", out string line, out string emotion);
            Assert.AreEqual("박 상병? 정신 차려!", line);
            Assert.AreEqual("놀람", emotion);
            QwenBarkSource.ParseOutput("{\"line\":\"뭐야\",\"emotion\":\"기쁨\"}", "의심", out _, out emotion);
            Assert.AreEqual("의심", emotion);
            QwenBarkSource.ParseOutput("그냥 한 줄", "긴장", out line, out emotion);
            Assert.AreEqual("그냥 한 줄", line);
            Assert.AreEqual("긴장", emotion);
        }

        [Test]
        public void 방향은_정면_기준_4분할()
        {
            Vector2 o = Vector2.zero;
            Assert.AreEqual("앞", BarkContext.DirectionOf(Vector2.right, o, new Vector2(5f, 1f)));
            Assert.AreEqual("뒤", BarkContext.DirectionOf(Vector2.right, o, new Vector2(-5f, 0f)));
            Assert.AreEqual("왼쪽", BarkContext.DirectionOf(Vector2.right, o, new Vector2(0f, 5f)));
            Assert.AreEqual("오른쪽", BarkContext.DirectionOf(Vector2.right, o, new Vector2(0f, -5f)));
        }

        [Test]
        public void 구간값()
        {
            Assert.AreEqual("가까움", BarkContext.DistanceOf(3.9f));
            Assert.AreEqual("중간", BarkContext.DistanceOf(10f));
            Assert.AreEqual("멀음", BarkContext.DistanceOf(10.1f));
            Assert.AreEqual("방금", BarkContext.WhenOf(1.9f));
            Assert.AreEqual("조금 전", BarkContext.WhenOf(9f));
            Assert.AreEqual("오래 전", BarkContext.WhenOf(10f));
            Assert.AreEqual("두 번째", BarkContext.RepeatOf(1));
            Assert.AreEqual("멀쩡", BarkContext.BodyOf(0.7f));
            Assert.AreEqual("부상", BarkContext.BodyOf(0.3f));
            Assert.AreEqual("위독", BarkContext.BodyOf(0.29f));
            Assert.AreEqual("여럿", BarkContext.AlliesOf(2));
            Assert.AreEqual("교전 준비", BarkContext.TaskOf(EnemyState.Engage, false, false));
            Assert.AreEqual("초소 대기", BarkContext.TaskOf(EnemyState.Patrol, false, false));
            Assert.AreEqual("복귀", BarkContext.TaskOf(EnemyState.Patrol, true, true));
        }

        [Test]
        public void 입력_JSON에는_숫자_좌표_의심도가_없다()
        {
            BarkContext c = Context(callsign: "박 상병");
            c.Recent.Add("오늘따라 조용하네.");
            string json = c.ToJson();
            StringAssert.Contains("\"사건\":\"문소리\"", json);
            StringAssert.Contains("\"세부\":{\"동료 이름\":\"박 상병\"}", json);
            StringAssert.Contains("\"최근 대사\":[\"오늘따라 조용하네.\"]", json);
            foreach (char ch in json) Assert.IsFalse(char.IsDigit(ch), "숫자가 들어감: " + json);
            Assert.IsTrue(JsonLooksValid(json));
        }

        static bool JsonLooksValid(string json)
        {
            int depth = 0; bool inString = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString) { if (c == '\\') i++; else if (c == '"') inString = false; continue; }
                if (c == '"') inString = true;
                else if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
            }
            return depth == 0 && !inString;
        }

        [Test]
        public void 캐시_키는_상황_구간값으로_만든다()
        {
            Assert.AreEqual("SND_DOOR|직접|앞|중간|처음|순찰|주의|결단한 고참", Context().CacheKey("SND_DOOR"));
        }

        [Test]
        public void 로컬_주소만_허용()
        {
            Assert.IsTrue(new QwenConfig { endpoint = "http://127.0.0.1:11434/v1/chat/completions" }.IsLocal);
            Assert.IsTrue(new QwenConfig { endpoint = "http://localhost:8080/v1/chat/completions" }.IsLocal);
            Assert.IsFalse(new QwenConfig { endpoint = "https://api.example.com/v1" }.IsLocal);
        }
    }
}
