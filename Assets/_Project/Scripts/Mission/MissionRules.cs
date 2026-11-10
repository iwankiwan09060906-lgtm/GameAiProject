using System;
using UnityEngine;

namespace GameAi
{
    public enum MissionResult { None, Success, Fail }

    // 같은 프레임 승패 판정(7.9): 사망이 있으면 실패, 그다음 목표 달성은 성공, 그다음 제한 시간 종료는 실패
    public static class ResultEvaluator
    {
        public static MissionResult Resolve(bool objectiveDone, bool timeUp, bool playerDead)
        {
            if (playerDead) return MissionResult.Fail;
            if (objectiveDone) return MissionResult.Success;
            if (timeUp) return MissionResult.Fail;
            return MissionResult.None;
        }
    }

    // 발각 횟수(7.10): 직접 목격으로 게이지 100에 처음 닿은 순간 1회. 같은 시간대 여러 적은 1회,
    // 모든 살아 있는 적이 순찰로 돌아간 뒤 다시 발각되면 +1. 경보로 안 것은 DetectionEvent가 아니므로 세지 않는다
    public sealed class DetectionCounter
    {
        bool active;

        public int Count { get; private set; }

        public void OnDetection()
        {
            if (active) return;
            active = true;
            Count++;
        }

        public void Tick(bool allPatrolling)
        {
            if (active && allPatrolling) active = false;
        }
    }

    [Serializable]
    public class MissionRecord
    {
        public string map = "map_01";
        public string difficulty = "normal";
        public float clear_time_s;
        public int detections;
        public int kills;
    }

    // 기록 비교(7.11): 클리어 시간(짧을수록, 소수점 셋째 자리) > 발각 횟수 > 처치 수(적을수록). 같으면 기존 유지
    public static class RecordRules
    {
        public static bool IsBetter(MissionRecord candidate, MissionRecord existing)
        {
            if (existing == null) return true;
            long a = (long)Math.Round(candidate.clear_time_s * 1000.0), b = (long)Math.Round(existing.clear_time_s * 1000.0);
            if (a != b) return a < b;
            if (candidate.detections != existing.detections) return candidate.detections < existing.detections;
            if (candidate.kills != existing.kills) return candidate.kills < existing.kills;
            return false;
        }

        public static float Round3(float seconds)
        {
            return (float)(Math.Round(seconds * 1000.0) / 1000.0);
        }
    }
}
