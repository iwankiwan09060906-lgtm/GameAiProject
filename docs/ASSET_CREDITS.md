# Asset Credits & Licenses (외부 에셋 출처 목록)

이 프로젝트의 외부 에셋은 **CC0 1.0**, **CC BY 3.0/4.0**(크레딧 표기 조건), **SIL Open Font License 1.1**만 쓴다. CC-BY-SA·GPL·비상업(NC)·재배포 금지·라이선스 미표기·유료 에셋은 쓰지 않는다(공개 GitHub 저장소에 원본 파일이 올라가기 때문). 외부 에셋이 없는 항목은 `Util/ProceduralSprites.cs`와 `Audio/AudioManager.cs`가 실행 중에 만든 임시 에셋을 쓴다.

- **상태**
  - `사용` = `Assets/_Project/`에 넣었고 라이선스를 직접 확인함
  - `사용(재확인 필요)` = 넣었지만 넣을 때의 재확인을 못 함. 아래 "확인" 참고
  - `후보` = 쓰기로 정했지만 아직 프로젝트에 넣지 않음
  - `제외` = 검토 후 쓰지 않음
- **확인**: 모든 후보의 라이선스는 2026-10-26에 각 페이지에서 확인했다. 2026-11-03에 파일을 넣으면서 Claude Code가 다시 확인했다.
  - `사용`: 팩 안의 라이선스 파일(`AssetSource/licenses/`, 폰트는 `Fonts/*-OFL.txt`) 또는 다운로드 페이지(dyru, niro58)에서 확인했다.
  - `사용(재확인 필요)`: 팩 안에 라이선스 파일이 없고, OpenGameArt가 자동 접속을 막아(HTTP 403) 페이지도 다시 보지 못했다. **사람이 브라우저로 페이지를 열어 확인한 뒤** 상태를 `사용`으로 바꾸고 날짜와 이름을 적는다.
- `docs/reference/`의 목표 화면 3장은 **스타일 참고용 스크린샷**이며 게임에 포함하지 않는다. 그 게임의 그림·글꼴·대사는 쓰지 않는다.
- 각 파일이 어느 원본에서 왔는지는 `ASSET_DIRECTION.md` 3장에 있다. 사용한 원본은 `AssetSource/`에 있다.
- 직접 그린 픽셀아트(캐릭터·초상화·UI 등)는 4장에 제작자를 적는다.
- CC-BY 에셋의 지정 크레딧 문구는 5장에 모으고, 게임의 크레딧 화면에 그대로 넣는다.

---

## 1. 그래픽·UI

| 에셋 | 제작자 | 라이선스 | 출처 | 용도 | 상태 |
| --- | --- | --- | --- | --- | --- |
| "Modern Houses" Tileset TopDown | Ritpop | CC0 1.0 | https://opengameart.org/node/138632 | 나무·타일·카펫 바닥, 테이블, 수납장, 냉장고 | 사용(재확인 필요) |
| Crimelike — Furniture | extradave | CC0 1.0 | https://opengameart.org/node/4726 | 소파·침대·욕조·책상·카운터 등 엄폐물 | 사용(재확인 필요) |
| Tiny Top Down Pack | Screaming Brain Studios | CC0 1.0 | https://opengameart.org/content/tiny-top-down-pack | 벽 타일 | 사용 (2026-11-03, Claude Code) |
| Grass Tiles [32x32] | CDmir | CC0 1.0 | https://opengameart.org/content/grass-tiles-32x32-0 | 야외 바닥, 풀숲 장식 | 사용(재확인 필요) |
| Trees & Bushes | ansimuz | CC0 1.0 | https://opengameart.org/content/trees-bushes | 관목·소나무 2종(잔디 배경을 지워서 사용) | 사용 (2026-11-03, Claude Code) |
| Pixel Topdown Shooter — Assets | dyru | **CC BY 4.0** | https://dyru.itch.io/top-down-shooter | 플레이어·적·손에 든 총·문·상자·서류·방탄 아이콘 | 사용 (2026-11-03, Claude Code) |
| Gun Muzzle Flash Effects (Fire and Ion and Melee) | Reactorcore | CC0 1.0 | https://opengameart.org/content/gun-muzzle-flash-effects-fire-and-ion-and-melee | 총구 화염 4장 | 사용(재확인 필요) |
| Gore Blood Gibs Meat Chunks | Reactorcore | CC0 1.0 | https://opengameart.org/content/gore-blood-gibs-meat-chunks | 피격 순간 피 애니메이션 9프레임 | 사용(재확인 필요) |
| Blood Splatters | AntumDeluge | CC0 1.0 | https://opengameart.org/node/147730 | 32×32 바닥 혈흔 4장 | 사용(재확인 필요) |
| CC0 Firearm Icons | AntumDeluge | CC0 1.0 | https://opengameart.org/node/120825 | HUD 권총 그림(turacept 원본 1장) | 사용(재확인 필요) |
| Weapon Pack — Firearms 02 | niro58 | CC0 1.0 | https://niro58.itch.io/weapon-pack-firearms-02 | HUD 소총 그림(Low_Res_Gun_05) | 사용 (2026-11-03, Claude Code) |
| Crosshair Pack | Kenney | CC0 1.0 | https://kenney.nl/assets/crosshair-pack | 꺾쇠 조준선 | 사용 (2026-11-03, Claude Code) |
| Emotes Pack | Kenney | CC0 1.0 | https://kenney.nl/assets/emotes-pack | `?` `!` 인지 아이콘 | 사용 (2026-11-03, Claude Code) |
| Bloodsplatter and Bloodsplash Animation | overcrafted | CC0 1.0 | https://opengameart.org/node/51056 | (640×800 고해상도 그림이라 픽셀 화면과 맞지 않음. Gore 팩으로 대체) | 제외 |
| Pixel Art Top Down Soldiers | tbbk | CC0 1.0 | https://opengameart.org/content/pixel-art-top-down-soldiers | (중세풍·애니메이션 없음, 분위기 불일치) | 제외 |
| Top Down Shooter Character Animations | JJ99 | 상업 사용 허용, **재배포 금지** | https://jayjay99.itch.io/top-down-shooter-male-character-animation | (공개 저장소에 올릴 수 없음) | 제외 |
| Top Down Shooter Character | ChrisJulch | 표기 없음 | https://chrisjulch.itch.io/top-down-shooter-pixel-art | (라이선스 불명확) | 제외 |
| Top-down Shooter | Kenney | CC0 1.0 | https://kenney.nl/assets/top-down-shooter | (벡터 스타일이라 목표 화면과 맞지 않음) | 제외 |
| UI Pack (Sci-Fi) / Game Icons | Kenney | CC0 1.0 | https://kenney.nl/assets/game-icons | (픽셀 UI를 직접 그리기로 함) | 제외 |

## 2. 사운드 (SFX·BGM·환경음)

| 에셋 | 제작자 | 라이선스 | 출처 | 용도 | 상태 |
| --- | --- | --- | --- | --- | --- |
| The Free Firearm Sound Library | Ben Jaszczak 외 (Free Firearm Sound Library Team) | CC0 1.0 | https://opengameart.org/content/the-free-firearm-sound-library | `sfx_shot_pistol`, `sfx_shot_rifle` (사격음만 있는 팩) | 사용(재확인 필요) |
| 25 CC0 Bang / Firework SFX | rubberduck | CC0 1.0 | https://opengameart.org/content/25-cc0-bang-firework-sfx | `sfx_shot_silenced` | 사용(재확인 필요) |
| Impact Sounds | Kenney | CC0 1.0 | https://kenney.nl/assets/impact-sounds | `sfx_footstep`, `sfx_hit_*`, `sfx_takedown`, `sfx_decoy`, `sfx_casing` | 사용 (2026-11-03, Claude Code) |
| RPG Audio | Kenney | CC0 1.0 | https://kenney.nl/assets/rpg-audio | `sfx_door`, `sfx_pickup`, `sfx_reload` | 사용 (2026-11-03, Claude Code) |
| Interface Sounds | Kenney | CC0 1.0 | https://kenney.nl/assets/interface-sounds | `sfx_alert`, `sfx_bark`, `sfx_nvg_*`, `sfx_dialogue_next`, `sfx_success`, `sfx_fail`, `sfx_ui_click`, `sfx_empty` | 사용 (2026-11-03, Claude Code) |
| Abandoned passages (horror ambience loop) | congusbongus | CC0 1.0 | https://opengameart.org/content/abandoned-passages-horror-ambience-loop | `bgm_game` | 사용(재확인 필요) |
| Dark Cavern Ambient | Paul Wortmann | CC0 1.0 | https://opengameart.org/content/dark-cavern-ambient | `bgm_menu` | 사용(재확인 필요) |
| Crickets ambient noise (loopable) | Ted Kerr | CC0 1.0 (표기 선택) | https://opengameart.org/content/crickets-ambient-noise-loopable | `amb_outdoor_night` | 사용(재확인 필요) |
| Dark Ambience Loop | Iwan Gabovitch (qubodup) | **CC BY 3.0** / GPL 2.0 / GPL 3.0 | https://opengameart.org/content/dark-ambience-loop | 추격·교전 배경음 예비. 게임에 넣지 않고 `AssetSource/reserve/`에 보관 | 후보 |

## 3. 폰트

| 폰트 | 제작자 | 라이선스 | 출처 | 용도 | 상태 |
| --- | --- | --- | --- | --- | --- |
| 갈무리 (Galmuri11) | Lee Minseo (quiple) | SIL OFL 1.1 | https://github.com/Quiple/Galmuri | HUD·대화창·메뉴·적 대사 | 사용 (2026-11-03, Claude Code) |
| Noto Sans KR | Google & Adobe | SIL OFL 1.1 | https://fonts.google.com/noto/specimen/Noto+Sans+KR | 디버그 오버레이 | 사용 (2026-11-03, Claude Code) |
| Neo둥근모 (NeoDunggeunmo) | Dalgona | SIL OFL 1.1 | https://github.com/neodgm | (받은 파일이 소스뿐이고 `.ttf`가 없음) | 제외 |

OFL은 폰트와 라이선스를 함께 배포하도록 정하고 있으므로 `Fonts/Galmuri-OFL.txt`, `Fonts/NotoSansKR-OFL.txt`를 폰트 옆에 둔다.

## 4. 직접 제작

| 에셋 | 제작자 | 도구 | 비고 |
| --- | --- | --- | --- |
| 플레이어·적 재채색본, 시체 임시본 | Claude Code | `AssetSource/build_assets.ps1` | `Art/chars/` — 원본이 CC BY 4.0이므로 5장 크레딧 유지 |
| HUD 무기 그림(빨강 윤곽 재채색, 소음기 추가) | Claude Code | `AssetSource/build_assets.ps1` | `Art/ui/weapon_*` — 원본은 CC0 |
| 초상화 2장, 폭탄·탈출·유인탄, 밝기·소음 아이콘, 자세 실루엣 4장, UI 패널 3장, 천장등, 탄피 (임시본) | Claude Code | 문자 격자·코드(`AssetSource/drawn/`, `build_assets.cs`) | 이미지 생성 AI가 아니라 픽셀을 직접 지정해 그림 |
| (예) 시체 스프라이트 다듬기 | 김성범 | Aseprite | `Art/chars/corpse_*.png` |
| (예) 초상화 교체 | 김성범 | Aseprite | `Art/portraits/*.png` |

## 5. 크레딧 화면에 넣을 문구

CC-BY 에셋은 아래 문구를 **반드시** 크레딧 화면에 넣는다(사용하지 않게 되면 삭제).

```
Characters and props based on "Pixel Topdown Shooter - Assets" by Dyru
(https://dyru.itch.io/top-down-shooter), licensed under CC BY 4.0, modified.
```

Dark Ambience Loop를 게임에 넣으면 아래 문구를 위에 더한다.

```
"Dark Ambience Loop" by Iwan Gabovitch qubodup.net, licensed under CC BY 3.0.
```

CC0 에셋은 감사 표시로 이어서 적는다.

```
Thanks to: Ritpop, extradave, Screaming Brain Studios, CDmir, ansimuz, Reactorcore,
AntumDeluge, niro58, Kenney, The Free Firearm Sound Library, rubberduck,
congusbongus, Paul Wortmann, Ted Kerr.
Fonts: Galmuri (Lee Minseo), Noto Sans KR (Google & Adobe) — SIL OFL 1.1.
```

---

## 라이선스 요약
- **CC0 1.0**: 저작권을 포기한 퍼블릭 도메인. 출처 표기 의무는 없지만 수업 검증을 위해 모두 기록한다.
- **CC BY 3.0 / 4.0**: 수정·상업 사용·재배포 가능. 단, 제작자·제목·라이선스·수정 여부를 표기해야 한다(5장).
- **SIL OFL 1.1**: 게임에 포함해 배포할 수 있는 오픈 폰트 라이선스. 폰트 파일만 따로 판매하는 것은 금지.
- **쓰지 않음**: CC-BY-SA, GPL 단독, NC, 재배포 금지, 라이선스 미표기, 유료.
