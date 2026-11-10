using System.Diagnostics;
using UnityEngine;

namespace GameAi
{
    // 프레임 시간과 "게임 코드가 쓴 시간"을 따로 모은다.
    // 프레임 시간이 길어도 게임 코드 시간이 짧으면 원인은 게임 밖(전원·수직동기·GPU)이다.
    // 최근 Capacity개만 링 버퍼로 들고 있어서 오래 켜 둬도 메모리가 늘지 않는다(7.14).
    public sealed class PerfProbe
    {
        // 이 시간(ms)을 넘긴 프레임을 "끊김"으로 센다. 33ms는 30fps 아래로 떨어진 프레임이다
        public const float HitchMs = 33f;

        const int Capacity = 600;

        readonly float[] frames = new float[Capacity];
        readonly float[] sections = new float[Capacity];
        readonly Stopwatch clock = new Stopwatch();
        int frameCount;
        int frameNext;
        int sectionCount;
        int sectionNext;

        // 직전 프레임 전체 시간(ms). 렌더링과 수직동기 대기를 포함한다
        public float LastFrameMs { get; private set; }

        // 한 프레임이 걸린 시간(초)을 기록한다
        public void Record(float deltaSeconds)
        {
            LastFrameMs = deltaSeconds * 1000f;
            Push(frames, ref frameCount, ref frameNext, LastFrameMs);
        }

        // 게임 코드 구간의 시작. GameManager.Update 맨 앞에서 부른다
        public void BeginSection()
        {
            clock.Restart();
        }

        // 구간의 끝. 걸린 시간(ms)을 기록하고 돌려준다. 렌더링은 이 구간에 들어가지 않는다
        // 마지막 구간 시간(ms)
        public float LastSectionMs { get; private set; }

        public float EndSection()
        {
            clock.Stop();
            float ms = (float)clock.Elapsed.TotalMilliseconds;
            Push(sections, ref sectionCount, ref sectionNext, ms);
            LastSectionMs = ms;
            return ms;
        }

        public void Reset()
        {
            frameCount = 0;
            frameNext = 0;
            sectionCount = 0;
            sectionNext = 0;
        }

        public float AverageFrameMs { get { return Average(frames, frameCount); } }
        public float MaxFrameMs { get { return Max(frames, frameCount); } }
        public float AverageSectionMs { get { return Average(sections, sectionCount); } }
        public float MaxSectionMs { get { return Max(sections, sectionCount); } }

        // 한 줄 요약. 예: "프레임 300개, 평균 8.3ms(120fps), 최대 21.0ms, 33ms 초과 0회"
        public string FrameSummary()
        {
            if (frameCount == 0) return "측정값 없음";

            int hitches = 0;
            for (int i = 0; i < frameCount; i++)
                if (frames[i] > HitchMs) hitches++;

            float average = AverageFrameMs;
            return "프레임 " + frameCount + "개, 평균 " + average.ToString("F1") + "ms(" + (1000f / average).ToString("F0")
                + "fps), 최대 " + MaxFrameMs.ToString("F1") + "ms, " + HitchMs + "ms 초과 " + hitches + "회";
        }

        // 게임 코드 구간 요약
        public string SectionSummary()
        {
            if (sectionCount == 0) return "측정값 없음";
            return "게임 코드 평균 " + AverageSectionMs.ToString("F2") + "ms, 최대 " + MaxSectionMs.ToString("F2") + "ms";
        }

        // 링 버퍼에 값을 넣는다. 꽉 차면 가장 오래된 값을 덮어쓴다
        static void Push(float[] buffer, ref int count, ref int next, float value)
        {
            buffer[next] = value;
            next = (next + 1) % Capacity;
            if (count < Capacity) count++;
        }

        static float Average(float[] buffer, int count)
        {
            if (count == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < count; i++) sum += buffer[i];
            return sum / count;
        }

        static float Max(float[] buffer, int count)
        {
            float max = 0f;
            for (int i = 0; i < count; i++)
                if (buffer[i] > max) max = buffer[i];
            return max;
        }
    }
}
