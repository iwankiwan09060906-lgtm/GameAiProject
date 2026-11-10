using System.Text;
using TMPro;

namespace GameAi
{
    // 글꼴 아틀라스 미리 채우기. 빌드에서 동적 아틀라스는 비어 있는 채로 시작해, 처음 보는 글자가 나올 때마다
    // 아틀라스를 다시 올리며 프레임이 끊긴다. 실행 후 한 번 자주 쓰는 한글과 데이터 문장을 넣어 둔다
    public static class FontPrewarm
    {
        static bool done;

        public static void Run(TMP_FontAsset font)
        {
            if (done || font == null) return;
            done = true;
            var sb = new StringBuilder();
            foreach (string file in new[] { "glyphs.txt", "barks.json", "dialogue.json", "credits.txt" })
            {
                string text = ConfigLoader.ReadText(file);
                if (text != null) sb.Append(text);
            }
            font.TryAddCharacters(sb.ToString(), out string missing);
        }
    }
}
