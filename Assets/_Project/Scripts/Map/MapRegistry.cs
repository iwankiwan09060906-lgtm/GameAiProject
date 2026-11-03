namespace GameAi
{
    // missions.json에서 맵 ID로 맵 파일 경로를 찾는다
    public static class MapRegistry
    {
        // 없으면 null
        public static string FindFile(MissionConfig missions, string mapId)
        {
            foreach (var entry in missions.maps)
                if (entry.id == mapId) return entry.file;
            return null;
        }
    }
}
