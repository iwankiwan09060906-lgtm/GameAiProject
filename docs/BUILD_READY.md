# 전체 개발 준비 자료 (BUILD_READY)

2026-11-10 기준. Claude Code에 명령 한 번으로 Phase 4~10과 Windows 빌드까지 끝내기 위해 필요한 것을 한곳에 모은 문서다. 규칙·수치는 여기 다시 쓰지 않고 기준 문서를 가리킨다.

| 기준 문서 | 담는 내용 |
| --- | --- |
| `MASTER_PROMPT.md` | 규칙·수치·데이터 형식·Phase 순서 (단일 기준) |
| `요구사항 정의서.md` / `기능 정의서.md` | 요구사항 ID·기능별 동작 |
| `DB설계.md` | 월드 이벤트 22종, Qwen 상황 대사, 데이터 표 |
| `ASSET_DIRECTION.md` 3장 / `ASSET_CREDITS.md` | 에셋 파일 목록 / 출처·라이선스 |

---

## 1. 현재 상태

| 항목 | 상태 |
| --- | --- |
| 완료 Phase | 0~3 (뼈대, 격자·맵·카메라, 플레이어·시야·화면 조명, 문·엄폐·무기·전투). 마지막 커밋 `1edd299`, 브랜치 `feature/phase-0-3` |
| EditMode 테스트 | `Core`·`Combat`·`Config`·`Grid`·`Health`·`Map`·`PlayerAndVision` (Phase 3 기준 112개 통과) |
| 남은 Phase | 4 소리·이벤트 → 5 적 AI·의심도·기본 대사 → 6 미션·결과 → 7 UI·메뉴·대화·사운드·Qwen → 8 밝기 격자·맵 2~4 → 9 S 기능 → 10 마감·빌드 |
| 맵 | `map_01.json`만 있음(80×60칸, `cell_m` 0.5, 조명 29개, 적 4명) |
| 자동 확인 도구 | `-smoke <폴더>` 실행 시 `SmokeRunner`가 정해진 조작을 재생해 판정·스크린샷을 남김. F3 성능 표시(`PerfOverlay`) |

## 2. 개발 환경

| 항목 | 값 | 확인 |
| --- | --- | --- |
| Unity | `C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe` | 설치됨. 6000.3.10f1·6000.6.7f1도 있으니 **6000.3.24f1만** 쓴다 |
| 빌드 대상 | Windows Standalone 64bit | `windowsstandalonesupport` 설치됨 |
| GPU | RTX 5070 Laptop, VRAM 8GB | 게임과 Qwen이 VRAM을 나눠 쓴다(5장) |
| 패키지 | URP 17.3(2D 렌더러·Pixel Perfect 포함), uGUI 2.0(TextMeshPro 포함), Test Framework 1.6, 2D Tilemap, Input System 1.20(Active Input Handling은 Both, 코드는 레거시 `Input`) | 모두 `Packages/manifest.json`에 있음. **새 패키지 설치 불필요** |
| 기타 도구 | git, Python, PowerShell, .NET | 있음 |

### 명령줄 실행 (Unity 에디터를 닫은 상태에서)

```powershell
$U = "C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe"
$P = "C:\Users\user\Desktop\20\3-1\GameAi\GameAiProject"

# 씬·프리팹·카탈로그 생성 (Tools/Build Project)
& $U -batchmode -quit -projectPath $P -executeMethod GameAi.EditorTools.ProjectBuilder.BuildProject -logFile build.log

# EditMode 테스트
& $U -batchmode -projectPath $P -runTests -testPlatform EditMode -testResults docs/evidence/editmode.xml -logFile test.log

# Windows 빌드 (Tools/Build Windows Player, 이미 있음) → Build/Game.exe
& $U -batchmode -quit -projectPath $P -executeMethod GameAi.EditorTools.ProjectBuilder.BuildWindowsPlayer -logFile player.log
```

> Unity 에디터가 열려 있으면 batchmode가 프로젝트를 잠가 실패한다.

## 3. 외부 API·서비스

### 3.1 Qwen (상황 대사) — Ollama

| 항목 | 값 |
| --- | --- |
| 서버 | Ollama 0.40.15 (설치됨, 백그라운드 실행 중) |
| 모델 | `qwen2.5:7b` (4.7GB, 받아 둠) |
| 주소 | `POST http://127.0.0.1:11434/v1/chat/completions` (OpenAI 호환) |
| 요청 본문 | `model`, `messages`(system + user), `max_tokens` 32, `temperature` 0.7, `response_format: {type: "json_schema", json_schema: {name, schema}}` |
| 응답 | `choices[0].message.content` = `{"line":"…","emotion":"…"}` |
| 실측(2026-11-10) | 첫 호출 **28.6초**(모델 불러오기), 이후 **0.23~0.30초**. 2초 한도 안에 충분히 들어온다 |
| 게임 쪽 | `UnityWebRequest`(내장 모듈, 패키지 추가 없음). 설정은 `qwen.json`(MASTER 9.12) |

실측 응답 예: 입력 "죽은 동료 발견·겁 많은 신참·적극 수색" → `{"line":"죽은 동료... 또 하나...","emotion":"긴장"}`. JSON 스키마 제약이 동작하고 감정은 5종 안에서 나온다. 다만 `세부`의 동료 이름을 쓰지 않는 경우가 있어 시스템 프롬프트에 "세부 값이 있으면 활용"을 넣는다.

**필수 처리**
- 첫 호출이 느리므로 메인 메뉴에서 1회 예열 요청(`warmup_on_start`)을 보내고 응답은 버린다.
- 서버가 꺼져 있으면(연결 거부) 경고 1회 후 기본 대사만 쓴다. 테스트(EditMode)는 Qwen 없이 가짜 응답으로 검증한다.
- 고정 시드 모드에서는 요청하지 않는다.

확인 명령(사람이 직접):
```powershell
ollama list                      # qwen2.5:7b 있는지
ollama run qwen2.5:7b "안녕"     # 모델 불러오기·예열
```

`llama-server`를 쓰고 싶으면 `qwen.json`의 `endpoint`만 `http://127.0.0.1:8080/v1/chat/completions`로 바꾼다.

### 3.2 그 밖의 API

외부 웹 API·계정·키는 없다. 사용하는 것은 Unity 내장 기능뿐이다.

| 용도 | Unity API |
| --- | --- |
| 조명·그림자 | URP 2D `Light2D`(Global·Point), `ShadowCaster2D` |
| 카메라 | `PixelPerfectCamera`(URP) |
| 글자 | TextMeshPro(`TMP_FontAsset`, Galmuri11 SDF) |
| 소리 | `AudioSource`, `AudioClip.Create`(코드 생성 톤) |
| 데이터 | `JsonUtility` + 래퍼 클래스(Newtonsoft 금지) |
| Qwen | `UnityWebRequest` |
| 시간 측정 | `System.Diagnostics.Stopwatch`(`PerfProbe`) |
| 빌드 | `BuildPipeline.BuildPlayer` |

## 4. 에셋

외부 에셋은 `Assets/_Project/Art·Audio·Fonts/`에 있다. **빠진 파일은 코드 생성으로 실행되므로 개발을 막지 않는다.**

### 4.1 그래픽

| 구분 | 상태 |
| --- | --- |
| 캐릭터(플레이어·경비병·소총병 idle/walk/shoot, 근접, 시체, 손에 든 총 3종) | 있음 |
| 타일(바닥 4종 + 금속 바닥 4종, 벽, 창문, 문 열림/닫힘) | 있음 |
| 엄폐물 `cover_low_*` 18장 · `cover_high_*` 8장, 풀숲 7장, 천장등 | 있음 |
| 전투 효과(혈흔 4, 피격 피 9프레임, 총구 화염 4, 탄피) | 있음 |
| 물건(목표·폭탄·탈출·유인탄) | 있음 |
| UI(조준선, ?·!, 방탄·밝기·소음 아이콘, 자세 실루엣 4, 패널 3, HUD 무기 그림 3), 초상화 2 | 있음 |
| 장식 소품 `deco_*` | 317장 있음 (라이선스 미확인, 6장) |
| `obj/obj_can.png`(깡통 8×8), `obj/obj_switch.png`(조명 스위치 8×8) | **없음 → 코드 생성** |
| 떨어진 무기 | 새 파일 없이 `chars/held_*` 사용 |

### 4.2 사운드

| 구분 | 상태 |
| --- | --- |
| 효과음 22개(사격 3, 장전, 빈 탄창, 탄피, 발소리, 문, 피격 3, 제압, 경고, 대사 표시음, 유인탄, 자료 확보, 야간 투시 2, 대화 넘김, 성공·실패·클릭) | 있음 |
| 배경음 `bgm_menu`·`bgm_game`, 환경음 `amb_outdoor_night` | 있음 |
| `sfx_can`(깡통), `sfx_scream`(비명), `sfx_switch`(스위치, C), `amb_wind_gust`(돌풍 4초) | **없음 → 코드 생성 톤**(MASTER 11.2에 생성 방법) |

빠진 소리를 실제 파일로 바꾸려면 CC0/CC BY 파일을 같은 ID 이름으로 `Audio/sfx/`(돌풍은 `Audio/bgm/`)에 넣고 `Tools/Build Project`를 다시 실행한다. 후보: Kenney Impact Sounds(CC0, 이미 쓰는 팩)의 `impactMetal_*`를 깡통으로 쓸 수 있다.

### 4.3 폰트

`Galmuri11.ttf` + `Galmuri11 SDF.asset`(HUD·대화·메뉴·대사), `NotoSansKR-Regular.ttf`(디버그). 둘 다 SIL OFL, 라이선스 파일 포함. 추가 작업 없음.

## 5. 데이터 파일

`Assets/StreamingAssets/Config/`

| 파일 | 상태 | 할 일 |
| --- | --- | --- |
| `game.json`, `weapons.json`, `enemies.json`, `difficulty.json`, `missions.json`, `dialogue.json`, `credits.txt` | 있음 | `dialogue.json`에 맵 2~4 브리핑·디브리핑 추가(Phase 8) |
| `ai.json` | 있음 (옛 형식) | `suspicion`·`clue`·`wind`·`hearing.floor_mult` 추가, `memory`를 소리 15·목격 8로, `gauge.door_clue` 삭제(MASTER 9.5) |
| `barks.json` | 있음 (상태 8종만) | 이벤트 코드 22종 기본 대사, `personas`, `banned_words` 추가(MASTER 9.8) |
| `events.json` | **없음** | 새로 작성(MASTER 7.21 표, 9.11) |
| `qwen.json` | **없음** | 새로 작성(MASTER 9.12, Ollama 주소·모델) |
| `maps/map_01.json` | 있음 | `open_doors`, 깡통 `k` 몇 개, 적 `persona`·`callsign` 추가 |
| `maps/map_02~04.json` | **없음** | MASTER 9.3 맵 규격으로 새로 설계(Phase 8). `AssetSource/build_map`으로 소품·엄폐 칸 생성 |

## 6. 사람이 해야 하는 것 (Claude Code가 못 하는 것)

| 할 일 | 언제 | 이유 |
| --- | --- | --- |
| 개발 명령 동안 **Unity 에디터 닫기** | 시작 전 | batchmode가 프로젝트를 잠근다 |
| **Ollama 켜 두기**, `ollama run qwen2.5:7b`로 예열 | 시작 전 | Qwen 연결 확인(없어도 개발은 되지만 실대사 확인 불가) |
| 라이선스 재확인: `사용(재확인 필요)` 12개 → 브라우저로 페이지 확인 후 `사용`으로 | 공개 전 | OpenGameArt가 자동 접속을 막음 |
| **newAsset 시트 출처 확인**(`deco_*` 317장, 금속 바닥) | 공개 저장소 push 전 | 상용 팩이면 재배포 금지. 확인 전엔 저장소를 공개하지 말 것 |
| 소리 귀로 듣고 어색한 것 교체 | Phase 7 이후 | 파일은 길이·파형으로만 고름 |
| 실제 플레이로 손맛·난이도 확인, 이벤트 점수·반경 조정(익재) | Phase 8 이후 | 수치는 초기값 |
| 미션별 클리어 영상 녹화 | Phase 10 | 화면 녹화는 사람이 |
| 최저 사양 팀원 PC에서 성능 측정(적 20명·60초) | Phase 10 | 합격선 7.14는 최저 사양 기준 |
| 수업 규정 확인(생성 도구, A* 직접 구현, 일정) | 제출 전 | 원문 우선 |

## 7. 막힐 수 있는 지점과 대응

| 위험 | 대응 |
| --- | --- |
| Qwen 첫 호출 28초 | 메인 메뉴 예열. 게임 중 첫 요청이 늦어도 2초 뒤 기본 대사로 넘어가므로 멈추지 않는다 |
| VRAM 8GB를 게임과 7B 모델이 나눠 씀 | 성능 측정은 Qwen 켠 상태와 끈 상태 둘 다 기록. 프레임이 떨어지면 `qwen2.5:3b`로 `model`만 바꾼다 |
| 한 번에 Phase 4~10 진행 시 중간 실패 | Phase마다 테스트 → 커밋 순서를 지킨다(실패한 Phase에서 멈추고 고침) |
| 맵 2~4 설계량이 큼 | MASTER 9.3 맵 제작 조건 + `MapValidatorTests`(도달 가능·밝은 칸 30~60%)로 자동 검증 |
| 문서 범위가 넓음(이벤트 22종 + Qwen) | M부터, S는 Phase 9 순서대로, C는 마지막. 못 한 것은 `DECISIONS.md`에 사유 |
| 비공개 에셋이 공개 저장소에 올라감 | 6장 확인 전에는 push하지 않는다 |

## 8. 완료 판정

MASTER 14장 "완료 정의" 10항목 + 13.2 체크리스트. 결과는 `docs/VERIFY.md`(지금 비어 있음)에 기록하고, 증거(테스트 결과 XML, 상태 전이 로그 3회, 스크린샷, 목표 화면 비교 3장)는 `docs/evidence/`에 둔다.

## 9. 명령 예시

Claude Code에 그대로 붙여 넣는다.

```
docs/BUILD_READY.md를 읽고 6장의 "시작 전" 항목이 끝났다고 가정한다.
docs/MASTER_PROMPT.md를 단일 기준으로 Phase 4부터 Phase 10까지 순서대로 구현한다.
- Phase마다 EditMode 테스트를 batchmode로 실행해 통과시키고 `phase-N: 요약`으로 커밋한다.
- 새 데이터 파일(events.json, qwen.json, map_02~04.json)과 기존 파일 갱신은 BUILD_READY 5장을 따른다.
- Qwen은 Ollama(qwen2.5:7b, 127.0.0.1:11434)로 연결하고, 서버가 없어도 기본 대사로 동작하게 한다.
- 빠진 에셋은 코드 생성으로 둔다.
- 문서에 없는 것은 가장 단순하게 정하고 DECISIONS.md에 한 줄로 적는다. 나에게 묻지 않는다.
- 마지막에 Windows 빌드(Build/Game.exe), VERIFY.md 기록, README.md를 완성한다.
- push는 하지 않는다.
```
