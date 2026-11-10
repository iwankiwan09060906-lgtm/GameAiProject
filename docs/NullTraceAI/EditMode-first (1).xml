# 적 NPC AI 작업 전달

기존 팀 작업과 분리된 `Assets/NullTraceAI` 폴더에 적 NPC AI, 자체 A*, 문 연결, 검증 코드와 독립 시연을 추가합니다. 이 변경으로 기존 팀 씬에 AI가 자동 연결되지는 않습니다.

## 추가 범위

- Runtime: 감각·개인 기억·Utility·행동 트리, 유한 순찰과 복귀, 지연 무전, 자체 A*, NPC가 여는 문, 경로/기억/대사 부하 제한.
- Tests: EditMode와 PlayMode 회귀 및 20/40명 발견·재탐색 표본.
- Demo/Editor/Scenes/Prefabs: 원·사각형 NPC로 시험하는 별도 샌드박스와 생성기. 새 씬·프리팹만 포함합니다.
- 이 문서 폴더: 인수인계, 공개 API, 코드 읽기 안내, 원본 샌드박스 검증 결과.

기존 `Assets/_Project`, 기존 씬·프리팹, 팀 README/설계 문서, Packages와 ProjectSettings는 변경하지 않았습니다. `.meta`는 함께 포함하며 기존 에셋과 GUID가 겹치지 않는지 검사했습니다. Library/Temp/Logs/obj/빌드와 내부 채팅·작업 상태 파일은 포함하지 않습니다.

## 먼저 읽기

1. [최종 인수인계 및 사양](Docs/적_NPC_AI_인수인계_및_사양확인_2026-11-06.md)
2. [연결 안내](Docs/Integration.md) · [공개 연결 규약](Docs/IntegrationContract.md)
3. [코드 읽는 순서](Docs/CodeReadingGuide.md)
4. [208개 원본 검증 결과](Results/2026-11-06/Verification.md) · [시연 사용법](SourceREADME.md)

## 팀 프로젝트에서 확인할 경계

원본 AI는 **Unity 6000.3.24f1**에서 EditMode 180개와 PlayMode 28개를 통과했습니다. 이 저장소의 실제 `ProjectSettings/ProjectVersion.txt`는 **6000.3.10f1**입니다. 이번 전달에서 버전을 올리지 않았고, 팀 프로젝트의 해당 버전에서 다시 실행한 결과는 아직 없습니다. 팀 문서에 적힌 6000.3.24f1과 실제 설정의 차이는 팀에서 확인해야 합니다.

이 전달물은 MonoBehaviour 어댑터, Physics2D 차폐/충돌, EnemyProfile 설정, 컴포넌트 이벤트와 자동 Update를 사용하는 독립 구현입니다. 팀 MASTER_PROMPT의 GameManager Tick 순서·격자 DDA·JSON 수치·공통 이벤트 버스에 맞춘 본게임 통합은 별도 작업입니다. 기존 팀 사양을 대체하거나 구현 완료했다고 해석하지 않습니다. 팀 플레이어·피해·맵·모델·실제 Qwen도 연결하지 않았습니다.

저장된 샌드박스는 장애물 레이어 8을 사용합니다. 팀의 레이어 설정과 맞는지 먼저 확인하세요. `Null Trace > Rebuild generated sandbox`는 샌드박스 씬/프리팹과 Build Settings의 씬 목록을 재생성하므로 팀 작업 프로젝트에서 임의로 실행하지 않습니다. 재생성이 필요하면 씬 담당자와 먼저 조율하고 별도 복제본에서 확인하세요.

20/40명 수치는 단순 도형과 실제 시험 타겟이 있는 발견·재탐색 구간의 경과 시간입니다. 사격 부하, 최종 맵/모델의 FPS, 허용 최대 NPC 수를 보장하지 않습니다. 이 브랜치는 PR에서 팀원 최소 1명의 확인을 거쳐 병합합니다.
