# DECISIONS

문서에 없어서 스스로 정한 것을 한 줄씩 적는다.

- 2026-10-26 풀숲·나무(`props/foliage_*`)를 맵 문자 `f`로 추가: 이동만 막고 시야·총알·빛·소리는 투과(ASSET_DIRECTION "장식, 이동만 막음"을 가장 단순하게 해석). 요구사항 #32.
- 2026-10-26 크레딧 문구는 `StreamingAssets/Config/credits.txt` 한 파일에서 읽어 그대로 표시(ASSET_CREDITS 5장과 같게 유지, 없으면 "외부 에셋 없음" 기본 문구). 요구사항 #31.
- 2026-10-26 야외 환경음은 플레이어 칸 바닥이 `o`일 때 0.5초 페이드 인/아웃, 배경음 음량을 따름. 파일 위치는 `Audio/bgm/`.
- 2026-10-26 조명 연출 값(전역광 색, 천장등 색·Falloff, 그림자 강도, 총구 빛, 야간 투시, 필름 그레인)은 `game.json`의 `lighting`에 둠.
- 2026-10-26 변형 스프라이트(`_*`)는 맵 소품이면 칸 좌표 해시로, 혈흔·총구 화염이면 연출 난수원으로 고름. 캐릭터 시트는 가로 32×32 프레임, 걷기 8fps·사격 0.1초.
- 2026-10-26 외부 에셋 연결은 `AssetCatalog`(ScriptableObject) + 에디터 `AssetCatalogBuilder`로 정함. 이유: 빌드된 게임은 `Assets/` 경로를 못 읽고, `Resources`는 폴더 규칙을 강제하며, StreamingAssets 런타임 로딩(PNG/OGG 디코딩)은 코드가 많음. 카탈로그는 씬이 직접 참조하므로 빌드에 자동 포함되고, 파일이 없으면 코드 생성 폴백. 교체 절차: 파일을 넣고 `Tools/Build Project`.
- 2026-10-26 Dark Ambience Loop(추격·교전 배경음 예비)는 아직 ID를 주지 않음. 쓰기로 하면 `bgm_combat`을 추가하고 CC BY 3.0 크레딧을 넣는다.
