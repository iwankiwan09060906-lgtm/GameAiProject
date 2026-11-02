namespace GameAi
{
    // missions.json에서 맵 ID로 파일을 찾는다
    public static class MapRegistry
    {
        public static string FindFile(MissionConfig missions, string mapId)
        {
            foreach (var entry in missions.maps)
                if (entry.id == mapId) return entry.file;
            return null;
        }
    }
}
