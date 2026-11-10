# 2D 탑뷰 전술 잠입 슈팅 — 에셋 디렉션 및 적용 가이드

이 문서는 팀이 정한 **목표 게임 화면 3장**(`docs/reference/`)을 기준으로 그래픽·사운드·UI의 방향, 무료 에셋, 파일 목록을 정한다. 외부 에셋은 2026-11-03에 `Assets/_Project/`에 정리해 넣었고, 실제 파일 목록은 3장이 기준이다.
수치·규칙은 `MASTER_PROMPT.md`(7장 규칙, 11장 에셋), `요구사항 정의서.md`(2.3 레퍼런스 화면 결정 #21~#30), `기능 정의서.md`를 따른다.

---

## 0. 목표 화면 (레퍼런스)

| 파일 | 장면 | 이 화면에서 가져올 것 |
| --- | --- | --- |
| [ref_01_combat.png](reference/ref_01_combat.png) | 실내 교전 | 따뜻한 조명이 비치는 실내와 어두운 야외의 대비, 픽셀아트 가구·바닥, 총구 화염·탄피·혈흔·시체, 적 머리 위 대사, 꺾쇠형 조준선, 좌하단 통합 HUD |
| [ref_02_dialogue.png](reference/ref_02_dialogue.png) | 브리핑 대화 | 화면 하단 대화창: 왼쪽 초상화와 이름표(빨강), 오른쪽 대사, 번호가 붙은 선택지(빨강 강조 막대) |
| [ref_03_nightvision.png](reference/ref_03_nightvision.png) | 야간 투시 | 녹색 단색 + 노이즈 필터, 문 앞의 상호작용 안내 `[SPACE] 열기`, HUD의 자세 실루엣과 장비 충전량(%) |

### 0.1 화면 요소 분석

| 요소 | 레퍼런스에서 보이는 모습 | 우리 게임 적용 | 관련 기능 |
| --- | --- | --- | --- |
| 시점 | 정탑뷰(위에서 수직), 카메라가 플레이어를 따라감 | 정탑뷰, 커서 쪽으로 최대 3m 앞당김(look-ahead) | FN-PLY-01 |
| 지형 | 검은 벽과 밝은 테두리, 나무 마루·타일·카펫 바닥, 욕조·소파·침대·상자 | 1m=32px 픽셀아트 타일, 벽은 검정 + 1px 하이라이트 + 바닥 쪽 그늘. 맵 1은 0.5m 칸이라 벽이 캐릭터보다 얇고, 금속 바닥과 시설·사무 가구 소품을 많이 깐 어두운 현대 시설 | FN-MAP-01 |
| 조명 | 천장등 아래만 노랗게 밝고 나머지는 어두움, 야외는 남색 어둠 | URP 2D 라이트(점광원 + 전역광), 벽 그림자. **밝기가 AI 감지에 영향** | FN-VIS-04 |
| 문 | 벽 사이의 얇은 금색 막대 | 닫힘 = 얇은 막대, 열림 = 90° 회전 막대 | FN-MAP-02 |
| 창문 | 벽 중간의 흰 직사각형 | `W` 타일: 시야 투과, 이동·총알 차단 | FN-COM-02 |
| 전투 연출 | 총구 화염, 노란 탄 궤적, 바닥 탄피, 피 웅덩이, 쓰러진 시체 | 궤적선 0.1초, 총구 화염 0.05초, 탄피·혈흔·시체는 미션 동안 남음 | FN-COM-04 |
| 적 대사 | 머리 위 짧은 외침(감정별 색: 의심·긴장 흰색, 놀람 노랑, 분노 빨강, 안도 회색) | 상태가 바뀌거나 이벤트를 인지할 때 20자 이내 한 줄 대사 2초 표시(?/! 아이콘과 함께) | FN-UI-07, FN-BRK-04 |
| 조준선 | 가운데가 빈 4갈래 꺾쇠 | 흰색 꺾쇠, 사격·이동 시 벌어짐(표시만, 명중률에는 영향 없음) | FN-UI-08 |
| HUD | 좌하단에 세로로 묶인 패널 하나 | 아래 0.2 참고 | FN-UI-01 |
| 상호작용 | 대상 근처에 `SPACE Open` 키 안내 | `[SPACE] 열기 / 닫기 / 자료 확보 / 해체` | FN-UI-09 |
| 대화 | 하단 대화창, 초상화, 선택지 | 미션 시작·종료 브리핑 | FN-UI-10 |
| 야간 투시 | 녹색 필터, 어두운 곳도 보임 | N 키 토글, 배터리 소모 | FN-PLY-08 |

### 0.2 HUD 배치 (좌하단 통합 패널)

```
┌──────────────────────────┐
│ ■■■■■■■■■■□□□□□□  (방탄)  │ ← 체력(빨강) / 방탄(하늘색) 2줄 막대 + 방탄 아이콘
│ 💡 ▮▮▮▮▮▮▮▮▮▮▯▯▯▯▯▯▯     │ ← 밝기 미터(초록): 내가 서 있는 곳이 얼마나 밝은가
│ 🔊 ▮▮▯▯▯▯▯▯▯▯▯▯▯▯▯▯▯     │ ← 소음 미터(노랑): 지금 내는 소리 크기
├──────────────────────────┤
│ [무기 그림]        ▮▮▮▮ │ ← 무기 아이콘 + 탄창 눈금
│ 16 | 34                  │ ← 탄창 | 예비 (야간 투시 켜면 배터리 %)
│ ─── P-9 ───              │ ← 무기 이름
└──────────────────────────┘  🚶 ← 자세 실루엣(서기·걷기·달리기·웅크리기)
```
- 좌상단: 목표 문구·타이머(작은 글씨). 우상단: 전역 경계 3단계(평상·경계·전투).
- 패널 배경은 반투명 검정, 테두리는 무기 이름 칸만 금색(`#C9A24A`).

---

## 1. 에셋 디렉션

### 1.1 그래픽: 따뜻한 조명 아래의 어두운 픽셀아트 (Lit Low-Light Pixel Art)
- **픽셀아트, 1m = 32px**: 모든 스프라이트는 PPU 32, Point 필터, 압축 없음. 카메라는 Pixel Perfect Camera(기준 해상도 960×540, 1920×1080에서 정확히 2배).
- **빛이 주인공**: 바닥과 가구는 원래 색(갈색 마루, 청록 카펫, 회색 타일)으로 칠하되, 실제 화면 밝기는 2D 라이트가 결정한다. 전역광은 아주 어둡게(실내 0.15, 야외 0.08, 남색 `#1A1830`), 천장등은 따뜻한 노랑(`#FFC873`) 점광원.
- **벽은 검정**: 벽 윗면 `#0B0B0E`, 방 안쪽 가장자리에 1px 밝은 선(`#3A3A44`). 벽은 2D 그림자를 드리운다.
- **캐릭터는 작고 읽기 쉽게**: 지름 0.8m(약 26px). 플레이어는 밝은 회색 옷 + 흰 하이라이트, 경비병은 어두운 붉은 갈색, 소총병은 짙은 녹갈색. 피아 구분은 **실루엣과 무기 모양**으로 하고, 디버그(F3)에서만 색 원을 덧그린다.
- **피와 흔적은 남는다**: 혈흔(`#6E0F0F`)·탄피(`#D9B24C`, 2px)·시체는 미션이 끝날 때까지 바닥에 남는다. 화면이 지저분해질수록 교전의 흔적이 보이도록.
- **야간 투시**: 녹색 단색(`#7CFF9A` 계열) + 필름 노이즈 + 가장자리 비네팅. 셰이더 1개(풀스크린 패스) 또는 카메라 위 반투명 쿼드로 처리.

### 1.2 UI: 검정 패널 + 빨강 강조 + 픽셀 폰트
- 기본색: 패널 `#0E0E12`(알파 0.85), 글자 `#E8E4DA`, 강조 빨강 `#C8323C`, 금색 테두리 `#C9A24A`.
- 대화창은 화면 하단 폭 전체, 위쪽 1px 빨강 선. 초상화 128×160px(픽셀 4배 확대), 이름표는 빨강 바탕 흰 글씨. 선택된 선택지는 빨강 막대.
- 글꼴은 한글 픽셀 폰트(갈무리 11) 하나로 통일한다. 숫자·영문도 같은 폰트.

### 1.3 사운드: 건조한 효과음 + 저음 앰비언트
- 효과음은 0.05~0.4초로 짧고 잔향이 적게. 소리는 AI가 듣는 정보이므로 서로 겹쳐도 구분되어야 한다.
- 배경음은 낮게 깔리는 드론·패드. 발소리와 발각 경고음이 묻히지 않게 한다.
- 적 대사는 **음성 없이 글자만** 띄운다(녹음 부담 제거). 대신 짧은 "웅얼" 효과음(`sfx_bark`) 1종을 같이 재생한다.

---

## 2. 무료 에셋

> 원칙(결정 #5): 사용이 허용된 무료 에셋 또는 직접 제작. 라이선스를 다운로드 페이지에서 **직접 확인한 뒤** `ASSET_CREDITS.md`에 기록한다. 레퍼런스 화면과 똑같은 스타일의 무료 에셋은 거의 없으므로, **바닥·벽·가구는 무료 타일을 고치고, 캐릭터·초상화·UI는 직접 그리는 것**을 기본으로 한다(Aseprite 임포터가 프로젝트에 설치되어 있음).
>
> **라이선스 기준**
> - **CC0**: 우선 사용. 표기 의무는 없지만 `ASSET_CREDITS.md`에 기록한다.
> - **CC-BY (3.0/4.0)**: 게임 크레딧 화면과 `ASSET_CREDITS.md`에 지정 문구를 적는 조건으로 허용한다.
> - **쓰지 않음**: CC-BY-SA, GPL, 비상업(NC), "재배포 금지", 라이선스 표기 없음, 유료. 공개 GitHub 저장소에 원본 파일이 올라가므로 재배포 금지 조건이 있으면 쓸 수 없다.
>
> 아래 에셋의 상태와 라이선스 확인 기록은 `ASSET_CREDITS.md`가 기준이다.

### 2.0 레퍼런스 요소 → 에셋 대응

레퍼런스의 핵심은 **"색이 있는 픽셀 소품 + 강한 2D 조명"** 이다. 무료 타일은 밝고 평평하게 칠해져 있으므로, 분위기는 에셋이 아니라 **조명·그림자·후처리**로 만든다(2.4).

| 레퍼런스 요소 | 받아올 에셋 | 우리가 손볼 것 |
| --- | --- | --- |
| 나무 마루·체크 타일·청록 카펫 | Modern Houses | 16px 타일은 2배 확대, 채도 낮춤, 카펫은 청록 재채색 |
| 검은 벽 | Tiny Top Down Pack | 거의 검정으로 어둡게 |
| 얇은 금색 문 | Pixel Topdown Shooter(dyru) | 벽 조각을 금색 재채색 |
| 욕조·소파·침대·상자 | Modern Houses, Crimelike Furniture, dyru | 위에서 본 모습 위주로 골라 엄폐물(`c`/`C`)로 지정 |
| 어두운 정원·풀숲 | Grass Tiles 32×32, Trees & Bushes | Trees & Bushes는 잔디가 배경에 구워져 있어 작은 나무 2종만 잘라 씀 |
| 총을 든 캐릭터 | Pixel Topdown Shooter(dyru) | 회색 캐릭터를 플레이어, 주황 캐릭터를 경비병, 녹색 체크 캐릭터를 소총병으로 재채색. 총은 별도 그림(`held_*`)을 겹침 |
| 총구 화염·혈흔·시체 | Reactorcore 총구 화염, Reactorcore 혈흔, Blood Splatters | 혈흔은 `#6E0F0F`로 재채색, 시체는 캐릭터 + 혈흔 합성(임시본) |
| 꺾쇠 조준선, `?` `!` | Kenney Crosshair Pack, Emotes Pack | 조준선에 어두운 1px 외곽선 추가 |
| HUD 무기 그림 | CC0 Firearm Icons, Weapon Pack Firearms 02 | 빨강 윤곽으로 재채색 |
| 초상화·대화창·HUD 패널 | 없음 | 임시본을 코드로 그려 넣음(3.1). 팀이 Aseprite로 그리면 같은 이름으로 덮어씀 |
| 노란 천장등, 남색 야외, 벽 그림자, 야간 투시 | 없음 | Unity 기능으로 제작(2.4) |

### 2.1 그래픽

| 에셋 | 제작자 | 활용처 | 라이선스 | 링크 |
| --- | --- | --- | --- | --- |
| "Modern Houses" Tileset TopDown | Ritpop | 현대식 실내 바닥·벽·가구(16px → 32px 2배) | CC0 | [opengameart](https://opengameart.org/node/138632) · [itch.io](https://ritpop.itch.io/modern-houses-tileset-topdown) |
| Crimelike — Furniture | extradave | 32×32 욕조(피 묻은 변형 포함)·소파·침대 등 가구 | CC0 | [opengameart](https://opengameart.org/node/4726) |
| Tiny Top Down Pack | Screaming Brain Studios | 32×32 벽 타일(10가지 스타일 중 어두운 패널 1장) | CC0 | [opengameart](https://opengameart.org/content/tiny-top-down-pack) |
| Grass Tiles [32x32] | CDmir | 야외 바닥(`tile_floor_outdoor`) | CC0 | [opengameart](https://opengameart.org/content/grass-tiles-32x32-0) |
| Trees & Bushes | ansimuz | 야외 관목·소나무(시트가 불투명이라 잔디를 지워서 사용, 큰 나무는 크기가 맞지 않아 안 씀) | CC0 | [opengameart](https://opengameart.org/content/trees-bushes) |
| Pixel Topdown Shooter — Assets | dyru | **캐릭터 바탕**: 5명 × 4자세(32×32). 총 3종·상자·서류·방탄 아이콘·문으로 쓴 벽 조각도 포함 | **CC BY 4.0** (크레딧 필수) | [itch.io](https://dyru.itch.io/top-down-shooter) |
| Gun Muzzle Flash Effects | Reactorcore | 총구 화염 4장(24×20). 사격마다 무작위 1장 | CC0 | [opengameart](https://opengameart.org/content/gun-muzzle-flash-effects-fire-and-ion-and-melee) |
| Gore Blood Gibs Meat Chunks | Reactorcore | 피격 순간 피 애니메이션(Blood2_Burst 9프레임) | CC0 | [opengameart](https://opengameart.org/content/gore-blood-gibs-meat-chunks) |
| Blood Splatters | AntumDeluge | 32×32 바닥 혈흔 데칼 | CC0 | [opengameart](https://opengameart.org/node/147730) |
| CC0 Firearm Icons | AntumDeluge | HUD 권총 그림 바탕(turacept 원본 50×25) | CC0 | [opengameart](https://opengameart.org/node/120825) |
| Weapon Pack — Firearms 02 | niro58 | HUD 소총 그림 바탕 | CC0 | [itch.io](https://niro58.itch.io/weapon-pack-firearms-02) |
| Crosshair Pack | Kenney | 꺾쇠형 조준선 | CC0 | [kenney.nl](https://kenney.nl/assets/crosshair-pack) |
| Emotes Pack | Kenney | `?` `!` 인지 아이콘 | CC0 | [kenney.nl](https://kenney.nl/assets/emotes-pack) |

**검토했지만 쓰지 않는 것**

| 에셋 | 이유 |
| --- | --- |
| Bloodsplatter and Bloodsplash Animation (overcrafted, CC0) | 640×800 고해상도 그림이라 픽셀 화면과 맞지 않음. Reactorcore 혈흔으로 대체 |
| Pixel Art Top Down Soldiers (tbbk, CC0) | 창병·장교 변형이 있는 중세풍이고 애니메이션이 없어 현대 전술 분위기와 맞지 않음 |
| Top Down Shooter Character Animations (JJ99) | 정탑뷰로 분위기는 맞지만 **재배포 금지** 조건 → 공개 저장소에 올릴 수 없음 |
| Top Down Shooter Character (ChrisJulch) | 페이지에 라이선스 표기가 없음 |
| [LPC] House interior 등 LPC 실내 타일 | CC-BY-SA / GPL |
| Kenney Top-down Shooter | 벡터 스타일이라 레퍼런스와 맞지 않음(코드 생성 대체 참고용) |
| 유료 실내 가구 팩(itch.io·Asset Store) | 유료 |

### 2.2 효과음·배경음

| 에셋 | 제작자 | 활용처 | 라이선스 | 링크 |
| --- | --- | --- | --- | --- |
| The Free Firearm Sound Library | Ben Jaszczak 외 | 권총·소총 사격(사격음만 있는 팩) | CC0 | [opengameart](https://opengameart.org/content/the-free-firearm-sound-library) |
| 25 CC0 Bang / Firework SFX | rubberduck | 소음권총 | CC0 | [opengameart](https://opengameart.org/content/25-cc0-bang-firework-sfx) |
| Impact Sounds | Kenney | 발소리, 피격, 근접 제압, 유인탄, 탄피 | CC0 | [kenney.nl](https://kenney.nl/assets/impact-sounds) |
| RPG Audio | Kenney | 문 개폐, 자료 확보, 재장전 | CC0 | [kenney.nl](https://kenney.nl/assets/rpg-audio) |
| Interface Sounds | Kenney | 버튼, 빈 탄창, 발각 경고음, 대화 넘김, 성공·실패 | CC0 | [kenney.nl](https://kenney.nl/assets/interface-sounds) |
| Abandoned passages (horror ambience loop) | congusbongus | **인게임 배경음** `bgm_game` — 어둡고 좁은 실내의 불안한 앰비언트 루프 | CC0 | [opengameart](https://opengameart.org/content/abandoned-passages-horror-ambience-loop) |
| Dark Cavern Ambient | Paul Wortmann | 메뉴 배경음 `bgm_menu` (루프 버전 002) | CC0 | [opengameart](https://opengameart.org/content/dark-cavern-ambient) |
| Crickets ambient noise (loopable) | Ted Kerr | 야외 환경음 `amb_outdoor_night` — 레퍼런스의 밤 정원 | CC0 | [opengameart](https://opengameart.org/content/crickets-ambient-noise-loopable) |
| Dark Ambience Loop (예비) | Iwan Gabovitch (qubodup) | 추격·교전 중 배경음 후보 | **CC-BY 3.0** (크레딧 필수) | [opengameart](https://opengameart.org/content/dark-ambience-loop) |

> 이전 안에서 `bgm_game`으로 정했던 **Dark Ambience Loop는 CC0가 아니라 CC-BY 3.0 / GPL**이었다. 기본 배경음은 CC0인 Abandoned passages로 바꾸고, Dark Ambience Loop는 크레딧을 넣는 조건의 예비 후보로 내렸다.

### 2.3 폰트

| 폰트 | 활용처 | 라이선스 | 링크 |
| --- | --- | --- | --- |
| 갈무리(Galmuri11) | HUD·대화창·메뉴·적 대사 전부 | SIL OFL 1.1 | [GitHub](https://github.com/Quiple/Galmuri) |
| Noto Sans KR | 디버그 오버레이처럼 작은 글자가 많이 나올 때 | SIL OFL 1.1 | [Google Fonts](https://fonts.google.com/noto/specimen/Noto+Sans+KR) |

Neo둥근모는 받은 파일이 소스뿐이고 `.ttf`가 없어 제외했다. 갈무리가 너무 가늘어 보이면 릴리스 페이지에서 `.ttf`를 받아 다시 검토한다.

### 2.4 에셋 없이 Unity 기능으로 만드는 것

레퍼런스 분위기의 대부분은 아래 설정에서 나온다. 에셋을 하나도 받지 않아도 이것만 하면 화면 인상이 가까워진다.

| 효과 | 만드는 방법 | 값 |
| --- | --- | --- |
| 어두운 전역광 | URP 2D Renderer + Global Light 2D | 판정 값은 실내 0.15, 야외 0.08. 화면 세기 = 실내 값 × `global_intensity_scale`(10), 색 `#1A1830`. 야외는 바닥 타일 색을 야외/실내 비율로 어둡게 |
| 노란 천장등 | 방마다 Point Light 2D(맵 JSON의 `lamp` 위치) | 색 `#FFC873`, 반경 4~6m, Falloff 0.6 |
| 벽 그림자 | 벽 타일에 Shadow Caster 2D(Composite) | 그림자 강도 0.9 |
| 총구 화염 빛 | 사격 순간 0.05초 켜지는 Point Light 2D | 색 `#FFD38A`, 반경 3m |
| 야간 투시 | 카메라 위 전체 화면 쿼드 + 녹색 곱하기, 노이즈 텍스처(코드 생성 256×256, 매 프레임 UV 이동), 가장자리 비네팅 | 색 `#7CFF9A`, 전역광 강도 0.6으로 올림 |
| 필름 그레인 | 위 노이즈 텍스처를 평소에는 알파 0.04로 깔기 | — |
| 픽셀 정렬 | Pixel Perfect Camera | 기준 960×540, PPU 32 |

---

## 3. 에셋 파일 목록 (`Assets/_Project/`)

아래 파일은 **이미 들어 있다**(2026-11-03). `AssetCatalog`(MASTER_PROMPT 11.0)가 이 이름으로 찾고, 파일이 없으면 "없을 때" 칸의 코드 생성 에셋을 쓴다. 스프라이트는 모두 PPU 32, Point 필터.

**교체 방법**: 같은 이름의 파일로 덮어쓴 뒤 `Tools/Build Project`를 실행한다. 장식 소품(`deco_*`, `floor_*`)은 `AssetSource/build_deco.ps1`이 `AssetSource/newasset/` 시트에서 다시 만들고, 맵 1의 방·소품 배치는 `AssetSource/build_map.ps1`(결과 확인용 `AssetSource/map_preview.png`)이 만든다. 재채색 값이나 자르는 위치를 바꾸려면 `AssetSource/build_assets.cs`를 고치고 `AssetSource/build_assets.ps1`을 실행한다. 이 스크립트는 3.1의 스프라이트와 사격음 2개를 원본에서 다시 만들고, 3.2의 나머지 소리는 원본을 이름만 바꿔 넣은 것이다. 초상화·소품 도트는 `AssetSource/drawn/*.txt`(팔레트 + 문자 격자)를 고쳐도 된다. `AssetSource/`는 `Assets/` 밖이라 Unity가 임포트하지 않는다.

### 3.1 그래픽 (`Art/`)

| 파일 | 대상 | 크기 | 원본 | 없을 때 |
| --- | --- | --- | --- | --- |
| `tiles/tile_floor_wood.png` · `tile_floor_tile.png` · `tile_floor_carpet.png` | 실내 바닥 3종(맵 JSON `floor` 영역별) | 32×32 | Modern Houses 16px 2배. 카펫은 청록 재채색, 타일은 회색조 | 단색 + 2px 격자 무늬 |
| `tiles/tile_floor_outdoor.png` | 야외 바닥 | 32×32 | Grass Tiles | 단색 + 2px 격자 무늬 |
| `tiles/tile_wall.png` | 벽 `#` | 32×32 | Tiny Top Down을 거의 검정으로 어둡게. **1px 테두리는 타일에 없다** | 검정 + 1px 테두리 |
| `tiles/tile_window.png` | 창문 `W` | 32×32 | 벽 타일 + 흰 막대 | 벽 + 흰 막대 |
| `tiles/door_closed.png` / `door_open.png` | 문 `D` | 32×8 / 8×32 | dyru 벽 조각을 금색 재채색 | 금색 막대 |
| `props/cover_low_*.png` (20장) | 낮은 엄폐물 `c`: 소파 8·침대 4·욕조 4·책상·테이블 2·상자 | 32×32 | Crimelike, Modern Houses, dyru | 갈색 사각 + 줄무늬 |
| `props/cover_high_*.png` (8장) | 높은 엄폐물 `C`: 카운터 3·수납장·냉장고·오븐·오디오·큰 상자 | 32×32 | Crimelike, Modern Houses, dyru | 진한 갈색 사각 |
| `props/foliage_*.png` (7장) | 야외 풀숲 `f` 타일(장식, 이동만 막음) | 32×32 | Trees & Bushes 2장, Grass Tiles 5장 | 진한 녹색 원 |
| `chars/{player,guard,rifleman}_idle.png` / `_shoot.png` | 서 있기 / 사격 | 32×32 | dyru 재채색 | 원 + 총 막대 |
| `chars/{player,guard,rifleman}_walk.png` | 걷기(2프레임 시트) | 64×32 | dyru 재채색 | 코드 생성 1프레임 |
| `chars/player_melee.png` | 근접 제압 | 32×32 | dyru 재채색 | 사격 프레임으로 대체 |
| `chars/held_{pistol,silenced_pistol,rifle}.png` | 손에 든 총(캐릭터 위에 겹침) | 16×16 | dyru | 표시 안 함(코드 생성 캐릭터에 총 막대가 있음) |
| `chars/corpse_{guard,rifleman}.png` | 시체 | 32×32 | dyru 캐릭터 + 혈흔 합성(임시본) | 눕힌 실루엣 + 혈흔 |
| `fx/muzzle_flash_*.png` (4장) | 총구 화염. 왼쪽 끝이 총구 | 24×20 | Reactorcore | 노란 별 |
| `fx/blood_*.png` (4장) | 바닥 혈흔(`#6E0F0F` 재채색) | 32×32 | Blood Splatters | 붉은 얼룩 |
| `fx/hit_blood_*.png` (9장) | 피격 순간 피 애니메이션(번호 순서로 재생) | 20×20 | Reactorcore | 혈흔을 작게 흩뿌림 |
| `fx/casing.png` | 탄피 | 2×2 | 코드로 그림 | 금색 점 |
| `props/lamp.png` | 천장등 위치 표시 | 8×8 | 코드로 그림 | 노란 점 |
| `obj/obj_target.png` | 목표 서류 | 32×32 | dyru 2배 | 노란 서류 |
| `obj/obj_bomb.png` · `obj_exit.png` · `obj_decoy.png` | 폭탄·탈출 지점·유인탄 | 32×32 | 코드로 그림 | 빨간 상자 / 초록 윤곽 / 주황 원 |
| `obj/obj_can.png` | 깡통(맵 문자 `k`) | 8×8 | 파일 없음 | 회색 원통 `#9A9AA0` |
| `obj/obj_switch.png` | 조명 스위치(C) | 8×8 | 파일 없음 | 흰 사각 `#E8E4DA` |
| `ui/crosshair.png` | 꺾쇠 조준선(1px 어두운 외곽선 포함) | 32×32 | Kenney Crosshair | 흰 꺾쇠 4개 |
| `ui/icon_suspect.png` / `icon_detect.png` | `?` / `!` | 16×16 | Kenney Emotes 재채색 | 노랑 / 빨강 글자 |
| `ui/icon_armor.png` | HUD 방탄 아이콘 | 16×16 | dyru 하늘색 재채색 | 단색 기호 |
| `ui/icon_light.png` · `icon_noise.png` | HUD 밝기(전구)·소음(스피커) 아이콘 | 16×16 | 코드로 그림 | 단색 기호 |
| `ui/stance_{idle,walk,run,crouch}.png` | 자세 실루엣 | 32×64 | 코드로 그림 | 흰 사람 모양 |
| `ui/panel_dark.png` · `panel_gold.png` · `panel_dialogue.png` | HUD 패널 / 무기 이름 칸 금색 테두리 / 대화창(위쪽 빨강 1px). 9-slice(테두리 2px) | 16×16 | 코드로 그림 | 단색 Image |
| `ui/weapon_{pistol,silenced_pistol,rifle}.png` | HUD 무기 그림 | 64×32 | Firearm Icons(권총), Weapon Pack(소총) 빨강 윤곽 재채색 | 빨강 윤곽 총 |
| `portraits/accomplice.png` · `portraits/player.png` | 대화 초상화(`dialogue.json`의 화자 2명, 4배 표시) | 32×40 | 코드로 그림(임시본) | 이름 첫 글자가 적힌 사각 |
| `tiles/floor_metal_dark.png` · `floor_metal_plate.png` · `floor_metal_blue.png` · `floor_ground.png` | 맵 1 바닥(`floor_sprites`로 `w`·`t`·`k`·`o`에 대응). 0.5m 칸마다 16px 조각을 잘라 그림 | 32×32 / 64×64 | newAsset 시설 시트·지면 시트 | 맵의 기본 바닥(`tile_floor_*`) |
| `deco/deco_*.png` (317장) | 맵 장식 소품(맵 JSON `props`). 시설(`deco_server_*`, `deco_console_*`, `deco_elevator*`, 화물·드럼통·화장실 등), `deco_office_*`(사무 가구), `deco_ruin_*`(폐허·잔해), `deco_terr_*`(흙·얼룩·돌·콘크리트판), `deco_item_*`(작은 물건) | 그림마다 다름(32px = 1m) | newAsset 시트 8장(`AssetSource/build_deco.ps1`) | 그리지 않음(판정은 엄폐 칸이 맡으므로 실행에는 영향 없음) |

**캐릭터 사용법**: 모든 캐릭터는 오른쪽(+X)을 본다. dyru 캐릭터에는 총이 없으므로 `held_*`를 겹쳐 그린다. 기준점은 캐릭터 칸의 아래쪽 손 (25, 24)px이고, 총 그림의 왼쪽 끝이 손잡이다.

3장의 이름이 있는 파일은 "파일 없음" 표시 항목을 빼고 모두 들어 있다. "파일 없음"은 코드 생성으로 실행되며 같은 이름 파일을 넣으면 바뀐다. 떨어진 무기는 따로 그림 없이 `chars/held_*`를 쓴다. "코드로 그림"은 외부 에셋이 없어 `AssetSource/`에서 만든 임시본이고, 팀이 그린 그림이 나오면 같은 이름으로 덮어쓴다. 화자를 `dialogue.json`에 더하면 `portraits/{speaker_id}.png`도 더한다.

### 3.2 사운드 (`Audio/sfx/`, `Audio/bgm/`)

파일 이름이 ID다. 확장자는 `.ogg`·`.wav`·`.mp3`를 모두 받는다. 길이는 실제 파일 기준이다.

| ID | 용도 | 길이 | 원본 |
| --- | --- | --- | --- |
| `sfx_shot_pistol` / `sfx_shot_rifle` (`.wav`) | 사격 | 0.12s | Firearm Library `X_39P`(Walther PPQ) / `D_32P`(AR-15) 첫 발 |
| `sfx_shot_silenced` | 소음 사격 | 0.26s | 25 Bang `shot_03` |
| `sfx_reload` | 재장전 | 0.45s | RPG Audio `metalClick` |
| `sfx_empty` | 빈 탄창 | 약 0.1s | Interface `click_002` |
| `sfx_casing` | 탄피 떨어짐 | 0.35s | Impact `impactMetal_light_000` |
| `sfx_footstep` | 발소리 | 0.11s | Impact `footstep_concrete_000` |
| `sfx_door` | 문 | 0.92s | RPG Audio `doorOpen_1` |
| `sfx_hit_player` / `sfx_hit_armor` / `sfx_hit_enemy` | 피격(체력 / 방탄 / 적) | 0.43 / 0.54 / 0.12s | Impact `impactPunch_medium_000` / `impactPlate_light_000` / `impactSoft_medium_000` |
| `sfx_takedown` | 근접 제압 | 0.51s | Impact `impactSoft_heavy_000` |
| `sfx_alert` | 발각 경고음 | 0.50s | Interface `error_006` |
| `sfx_bark` | 적 대사 표시음 | 0.12s | Interface `bong_001` |
| `sfx_decoy` | 유인탄 | 0.27s | Impact `impactMetal_medium_000` |
| `sfx_can` | 깡통 차는 소리 | 0.25s | 파일 없음(코드 생성 톤) |
| `sfx_scream` | 적 비명 | 0.3s | 파일 없음(코드 생성 톤) |
| `sfx_switch` | 조명 스위치(C) | 0.05s | 파일 없음(코드 생성 톤) |
| `sfx_pickup` | 자료 확보 | 0.28s | RPG Audio `bookPlace1` |
| `sfx_nvg_on` / `sfx_nvg_off` | 야간 투시 켜기·끄기 | 0.21s | Interface `maximize_003` / `minimize_003` |
| `sfx_dialogue_next` | 대화 넘김 | 0.04s | Interface `select_001` |
| `sfx_success` / `sfx_fail` / `sfx_ui_click` | 결과·버튼 | 0.54 / 0.50 / 0.10s | Interface `confirmation_002` / `error_005` / `click_001` |
| `bgm_menu` | 메뉴 배경음(`Audio/bgm/`) | 루프 120s | Dark Cavern Ambient 002 |
| `bgm_game` | 게임 배경음(`Audio/bgm/`) | 루프 31s | Abandoned passages `freezer` |
| `amb_outdoor_night` (`.mp3`) | 야외 환경음(`Audio/bgm/`) | 루프 | Crickets ambient noise |
| `amb_wind_gust` | 돌풍(`Audio/bgm/`, 야외에서 4초) | 4s | 파일 없음(코드 생성 톤) |

소리는 파일 길이와 파형으로만 골랐고 귀로 듣지 않았다. Phase 7에서 들어 보고 어울리지 않으면 같은 팩의 다른 파일로 바꾼다.

### 3.3 폰트 (`Fonts/`)
- `Galmuri11.ttf` → TextMeshPro 폰트 에셋 `Galmuri11 SDF.asset`(Atlas Render Mode: Raster, Sampling Point Size 12의 배수, 기본 24).
- `NotoSansKR-Regular.ttf`: 디버그 오버레이 전용.
- `*-OFL.txt`: 폰트 라이선스. 폰트와 함께 배포해야 하므로 지우지 않는다.
- 폰트가 없으면 Unity 기본 폰트로 표시하고 `DECISIONS.md`에 적는다.

### 3.4 크레딧 화면
CC-BY 에셋을 쓰면 메인 메뉴의 "크레딧" 화면과 `ASSET_CREDITS.md` 5장에 지정 문구를 넣는다. CC0 에셋도 감사 표시로 제작자 이름을 같은 화면에 적는다.
