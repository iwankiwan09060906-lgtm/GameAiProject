# 2D 탑뷰 전술 잠입 슈팅 — 에셋 디렉션 및 적용 가이드

본 문서는 `MASTER_PROMPT.md`(11장 에셋), `요구사항 정의서.md`(결정 #5, SND-02), `기능 정의서.md`를 기준으로 게임의 시각·청각적 통일성을 유지하기 위한 **에셋 디렉션**, **100% 무료(CC0 / OFL) 다운로드 링크**, **폴더 및 파일명 매핑 규칙**을 정의합니다.

---

## 1. 에셋 디렉션 (아트 & 사운드의 '느낌')

### 1.1 그래픽(Art): 고대비 미니멀 전술 탑뷰 (High-Contrast Minimalist Tactical)
- **어둡고 절제된 배경과 지형**: 바닥(`#2A2D33`)과 벽(`#7A808C`), 엄폐물(`#5B6C80`, `#3E4A5A`)은 채도가 낮은 무채색·청회색 톤을 유지합니다. 90° 부채꼴 시야(12m)와 탐색 기억(방문 지형 30~40% 회색조, 미탐색 완전 암전) 오버레이가 화면 전체에 적용되므로, 바닥 타일은 복잡한 무늬가 없는 **플랫 벡터(Flat Vector) 도형 스타일**이어야 시야 경계선과 총알 궤적선(`#FFE680`)이 명확히 보입니다.
- **즉각적인 피아식별**: 어두운 배경 위에서 플레이어(파랑 `#3C8DFF`), 경비병(빨강 `#E04545`), 소총병(진한 빨강 `#A82020`), 목표물(노랑 `#F2C230`), 탈출 지점(초록 `#37C871`), 유인탄(주황 `#FF9A3C`)이 한눈에 구분되어야 합니다.
- **엄폐물 높이감 구분**: 반엄폐형(낮은 엄폐물 `c`, 총알 차단·시야 투과)은 낮은 상자·테이블 형태로, 완전 차폐형(높은 엄폐물 `C`, 시야·총알 모두 차단)은 캐비닛·기둥·두꺼운 벽 형태로 직관적인 차이를 둡니다.

### 1.2 사운드(Audio): 건조한 어택감(Dry SFX) + 다크 앰비언트(Dark Ambient)
- **정보 전달형 효과음(SFX)**: 사격음·발소리·문 개폐음·유인탄 소리는 적 AI의 청각 감지(벽 1개당 감쇠, 세기 0.2 이상 반응)를 일으키는 핵심 정보입니다. 소리가 겹쳐도 명확히 구분되도록 **잔향이 적고 길이가 짧은(0.05초~0.4초) 건조한 사운드**를 사용합니다.
- **저음 위주의 배경음(BGM)**: 발소리나 발각 경고음(`!`, 약 1초)이 묻히지 않도록 화려한 멜로디 대신 **낮게 깔리는 신스 패드(Pad)·드론(Drone) 스타일의 스텔스 앰비언트**를 사용합니다.

---

## 2. 파트별 100% 무료 에셋 즉시 다운로드 링크

> **다운로드 팁 (Kenney.nl)**: 링크 접속 후 파란색 **`Download`** 버튼을 누르면 후원 팝업이 뜹니다. 결제 없이 하단의 회색 글씨 **`Continue without donating...`**을 클릭하면 즉시 `.zip` 파일이 무료로 다운로드됩니다.

### 2.1 2D 탑뷰 그래픽 (캐릭터 · 무기 · 타일 · 엄폐물 · 문)

| 에셋 팩 이름 | 느낌 및 활용처 | 라이선스 | 다운로드 링크 |
| --- | --- | --- | --- |
| **Kenney - Top-down Shooter** | **메인 그래픽 팩(580개 파일)**. 벡터 도형 스타일의 탑뷰 캐릭터(권총·소총 파지 자세), 바닥·벽 타일, 문, 상자/가구(낮은·높은 엄폐물) 포함 | CC0 1.0 | [다운로드 페이지](https://kenney.nl/assets/top-down-shooter) |
| **OpenGameArt - Assets for Top-Down Shooter** | 미니멀 탑뷰 슈터용 스프라이트 모음. 프로토타입 도형에서 디테일을 보강할 때 보조 타일·오브젝트로 활용 | CC0 1.0 | [다운로드 페이지](https://opengameart.org/content/assets-for-top-down-shooter) |

### 2.2 조준선(Crosshair) · 인지 아이콘(`?` / `!`) · HUD UI

| 에셋 팩 이름 | 느낌 및 활용처 | 라이선스 | 다운로드 링크 |
| --- | --- | --- | --- |
| **Kenney - Crosshair Pack** | 마우스 커서 조준선(FN-PLY-01)에 바로 쓸 수 있는 **200종의 고시인성 조준선 스프라이트** | CC0 1.0 | [다운로드 페이지](https://kenney.nl/assets/crosshair-pack) |
| **Kenney - Emotes Pack** | 적 머리 위 **인지 표시(의심 `?`, 발견 `!`)** 아이콘 스프라이트(FN-UI-02) 포함 | CC0 1.0 | [다운로드 페이지](https://kenney.nl/assets/emotes-pack) |
| **Kenney - UI Pack (Sci-Fi)** | 어두운 배경과 어울리는 모던/전술 스타일의 체력바, 버튼, 슬라이더, 드롭다운 UI 프레임 | CC0 1.0 | [다운로드 페이지](https://opengameart.org/content/ui-pack-sci-fi) |
| **Kenney - Game Icons** | 무기/탄약 아이콘, 전역 경계도 아이콘, 목표(`T` 서류/폭탄), 탈출구(`X`) 표시용 단색 벡터 아이콘 | CC0 1.0 | [다운로드 페이지](https://kenney.nl/assets/game-icons) |

### 2.3 효과음 (SFX — 사격 · 재장전 · 발소리 · 문 · UI · 경고음)

| 에셋 팩 이름 | 느낌 및 활용처 (`Assets/_Project/Audio/sfx/`) | 라이선스 | 다운로드 링크 |
| --- | --- | --- | --- |
| **The Free Firearm Sound Library** | 실제 총기 녹음 기반 CC0 라이브러리. 권총(`sfx_shot_pistol`), 소총(`sfx_shot_rifle`), 재장전(`sfx_reload`), 빈 탄창(`sfx_empty`) 추출용 | CC0 1.0 | [다운로드 페이지](https://opengameart.org/content/the-free-firearm-sound-library) |
| **25 CC0 Bang / Firework SFX** | 가볍고 타격감이 명확한 단발 총성 모음. 볼륨·피치를 낮춰 소음권총(`sfx_shot_silenced`) 등으로 활용 | CC0 1.0 | [다운로드 페이지](https://opengameart.org/content/25-cc0-bang-firework-sfx) |
| **Kenney - Impact Sounds** | 건조하고 짧은 타격·발걸음·충돌음 130종. 발소리(`sfx_footstep`), 피격음(`sfx_hit_*`), 근접 제압(`sfx_takedown`), 유인탄(`sfx_decoy`)용 | CC0 1.0 | [다운로드 페이지](https://kenney.nl/assets/impact-sounds) |
| **Kenney - RPG Audio** | 문 개폐음(`sfx_door`), 목표물 확보(`sfx_pickup`) 등 잠입 상호작용 효과음 포함 | CC0 1.0 | [다운로드 페이지](https://kenney.nl/assets/rpg-audio) |
| **Kenney - Interface Sounds** | 버튼 클릭(`sfx_ui_click`), 발각 경고음(`sfx_alert`), 미션 성공/실패(`sfx_success`, `sfx_fail`)용 전자음 100종 | CC0 1.0 | [다운로드 페이지](https://kenney.nl/assets/interface-sounds) |

### 2.4 배경음악 (BGM — 메뉴 및 인게임 스텔스 앰비언트)

| 에셋 팩 이름 | 느낌 및 활용처 (`Assets/_Project/Audio/bgm/`) | 라이선스 | 다운로드 링크 |
| --- | --- | --- | --- |
| **Dark Ambience Loop** | 발소리·총성을 방해하지 않고 긴장감을 유지하는 저음 앰비언트 루프. 인게임 배경음(`bgm_game`)용 | CC0 1.0 | [다운로드 페이지](https://opengameart.org/content/dark-ambience-loop) |
| **Dark Cavern Ambient** | 서서히 깔리는 저음 드론 사운드 루프. 메인 메뉴 배경음(`bgm_menu`)용 | CC0 1.0 | [다운로드 페이지](https://opengameart.org/content/dark-cavern-ambient) |

### 2.5 한글 폰트 (UI 및 디버그 오버레이용)

| 폰트 이름 | 느낌 및 활용처 (`Assets/_Project/Fonts/`) | 라이선스 | 다운로드 링크 |
| --- | --- | --- | --- |
| **Google Fonts - Noto Sans KR** | 어두운 HUD 패널 및 F3 디버그 오버레이의 한글·숫자 가독성이 가장 뛰어난 표준 폰트 | OFL 1.1 | [다운로드 페이지](https://fonts.google.com/noto/specimen/Noto+Sans+KR) |

---

## 3. 프로젝트 폴더(`Assets/_Project/`) 파일명 매핑 규칙

`MASTER_PROMPT.md` 11장 지침에 따라, 외부 에셋 파일이 없으면 코드 생성 임시 에셋이 작동하고 아래 경로에 **동일한 이름의 파일**을 넣으면 자동으로 교체됩니다.

### 3.1 그래픽 파일 매핑 (`Assets/_Project/Art/`)

| 파일명 (`Assets/_Project/Art/`) | 대응 요소 | 기본 색상/규격 가이드 | 추천 추출 원본 (Kenney 팩 기준) |
| --- | --- | --- | --- |
| `tile_floor.png` | 바닥 타일 (`.`) | `#2A2D33` 어두운 회색, 1m 정사각 | *Top-down Shooter* → `Tiles/tile_01.png` (어둡게 틴트) |
| `tile_wall.png` | 벽 타일 (`#`) | `#7A808C` 밝은 회색 + 테두리 | *Top-down Shooter* → `Tiles/tile_09.png` |
| `door_closed.png` | 닫힌 문 (`D`) | `#8B5A2B` 갈색 직사각 | *Top-down Shooter* → `Tiles/tile_463.png` |
| `door_open.png` | 열린 문 (`D`) | `#8B5A2B` 얇은 막대/열린 형태 | *Top-down Shooter* → `Tiles/tile_464.png` |
| `cover_low.png` | 낮은 엄폐물 (`c`) | `#5B6C80` 청회색 (시야 투과) | *Top-down Shooter* → `Tiles/tile_129.png` (작은 상자/테이블) |
| `cover_high.png` | 높은 엄폐물 (`C`) | `#3E4A5A` 진한 청회색 (시야 차단) | *Top-down Shooter* → `Tiles/tile_130.png` (대형 컨테이너/기둥) |
| `char_player.png` | 플레이어 (`P`) | `#3C8DFF` 파랑, 지름 0.8m | *Top-down Shooter* → `Hitman 1/hitman1_gun.png` |
| `char_guard.png` | 경비병 (Guard) | `#E04545` 빨강, 지름 0.8m | *Top-down Shooter* → `Soldier 1/soldier1_gun.png` |
| `char_rifleman.png` | 소총병 (Rifleman) | `#A82020` 진한 빨강, 지름 0.8m | *Top-down Shooter* → `Soldier 1/soldier1_machine.png` |
| `obj_target.png` | 목표물 (`T`) | `#F2C230` 노랑 마름모/서류가방 | *Game Icons* → `suitcase.png` 또는 `bomb.png` |
| `obj_exit.png` | 탈출 지점 (`X`) | `#37C871` 초록 사각 윤곽 | *Game Icons* → `exitDoor.png` |
| `obj_decoy.png` | 유인탄 | `#FF9A3C` 주황 작은 원 | *Top-down Shooter* → 소형 투척물 스프라이트 |
| `icon_suspect.png` | 의심 아이콘 (`?`) | 노랑 `?` | *Emotes Pack* → `emote_question.png` |
| `icon_detect.png` | 발견 아이콘 (`!`) | 빨강 `!` | *Emotes Pack* → `emote_exclamation.png` |
| `ui_crosshair.png` | 마우스 조준선 | 흰색/밝은 노랑 크로스헤어 | *Crosshair Pack* → `crosshair001.png` |

### 3.2 사운드 파일 매핑 (`Assets/_Project/Audio/`)

확장자는 `.ogg` 또는 `.wav`를 지원합니다.

#### 효과음 (`Assets/_Project/Audio/sfx/`)
| 파일명 (ID) | 용도 | 권장 길이 | 추천 추출 원본 |
| --- | --- | --- | --- |
| `sfx_shot_pistol` | 권총 사격 | 0.12s | *Free Firearm Library* → 9mm Pistol 단발 |
| `sfx_shot_silenced` | 소음권총 사격 | 0.08s | *25 CC0 Bang* (피치/볼륨 낮춤) 또는 *Impact Sounds* |
| `sfx_shot_rifle` | 소총 사격 | 0.10s | *Free Firearm Library* → Assault Rifle 단발 |
| `sfx_reload` | 재장전 | 0.40s | *Free Firearm Library* → Mag In / Cocking 소리 |
| `sfx_empty` | 빈 탄창 클릭 | 0.05s | *Free Firearm Library* → Dry Fire 클릭음 |
| `sfx_footstep` | 발소리 | 0.08s | *Impact Sounds* → `footstep_concrete_000.ogg` |
| `sfx_door` | 문 열기/닫기 | 0.20s | *RPG Audio* → `doorOpen_1.ogg` |
| `sfx_hit_player` | 플레이어 피격 | 0.15s | *Impact Sounds* → `impactPunch_heavy_000.ogg` |
| `sfx_hit_enemy` | 적 피격 | 0.10s | *Impact Sounds* → `impactPunch_medium_000.ogg` |
| `sfx_takedown` | 근접 제압 | 0.20s | *Impact Sounds* → `impactSoft_heavy_000.ogg` |
| `sfx_alert` | 발각 경고음 (`!`) | 1.00s | *Interface Sounds* → `confirmation_001.ogg` / `error_001.ogg` |
| `sfx_decoy` | 유인탄 작동음 | 0.30s | *Impact Sounds* → `impactMetal_light_000.ogg` |
| `sfx_pickup` | 목표(자료) 확보 | 0.30s | *RPG Audio* → `bookOpen.ogg` 또는 `cloth1.ogg` |
| `sfx_success` | 미션 성공 | 1.00s | *Interface Sounds* → `maximize_001.ogg` (상승 톤) |
| `sfx_fail` | 미션 실패 | 1.00s | *Interface Sounds* → `minimize_001.ogg` (하강 톤) |
| `sfx_ui_click` | UI 버튼 클릭 | 0.05s | *Interface Sounds* → `click_001.ogg` |

#### 배경음 (`Assets/_Project/Audio/bgm/`)
| 파일명 (ID) | 용도 | 권장 길이 | 추천 추출 원본 |
| --- | --- | --- | --- |
| `bgm_menu` | 메인 메뉴 배경음 | 루프 (20s~) | *Dark Cavern Ambient* (`cavern_loop.wav`) |
| `bgm_game` | 인게임 배경음 | 루프 (30s~) | *Dark Ambience Loop* (`dark-ambience.ogg`) |

### 3.3 폰트 파일 매핑 (`Assets/_Project/Fonts/`)
- `NotoSansKR-Regular.ttf` (또는 `.otf`)를 `Assets/_Project/Fonts/`에 배치하고 TextMeshPro Font Asset(`NotoSansKR SDF.asset`)으로 변환하여 사용합니다.