# 검은 먹 — 무한 달리기 테스트

## 실행

Unity **6000.3.23f1**에서 아래 전용 씬을 열고 **Play**를 누릅니다.

`Assets/Features/BlackInkPrototype/Scenes/BlackInkTestScene.unity`

메뉴 `Tools > InkRunner > Open Black Ink Test Scene`으로도 열 수 있습니다.
시작 후 3초 준비 시간이 지나면 기존 Inkwarrior가 오른쪽으로 계속 달립니다.
캐릭터는 화면 왼쪽 약 8% 위치에서 출발하고, 카메라가 따라가며 앞쪽 맵을 생성합니다.
플랫폼은 기본적으로 빈틈없이 이어져 있어 그리지 않아도 달리기를 계속 볼 수 있습니다.

- **좌클릭 드래그:** 경로를 윗면으로 하는 검은 먹 플랫폼 생성. 생성한 길이에 따라 잉크 소모.
- **Reset:** 캐릭터·먹·잉크·준비 시간을 초기화하고 같은 랜덤 순서의 맵으로 다시 시작.
- **Refill:** 현재 위치와 길을 유지하고 잉크만 채움. 마우스를 놓고 새로 클릭해 그립니다.
- **HUD:** 실제 Game 뷰 기준 몸 높이·먹 두께, 이동 거리, 현재 플랫폼·장식 수 표시.

`obstacle_1`, `obstacle_2`는 장식입니다. Collider나 충돌·피격·게임 오버 판정이 없습니다.
기본 TMP 폰트의 한글 글리프 제한으로 테스트 HUD는 영문으로 표시합니다.

## 크기와 맵 생성 방식

기본값은 **캐릭터 몸 높이 40px, 먹의 충돌 몸체 두께 25px**입니다.
이는 텍스처 파일 크기가 아니라 실제 Game 뷰 렌더링 픽셀 기준입니다.
`Lock To Screen Pixels`가 켜져 있으면 창 크기에 맞춰 카메라 범위를 조절합니다.
Game 뷰 표시 배율이 1×가 아니면 에디터 화면에서 재는 크기는 달라질 수 있습니다.
몸 높이는 기존 `run_01`의 머리~발바닥 약 210px를 기준으로 하며 무기·망토·여백을 제외합니다.
애니메이션 포즈별 외곽선은 변하지만 기준 높이와 먹 두께는 고정합니다.

100px = 1 월드 유닛이므로 몸은 0.4u, 먹은 0.25u입니다.
원본 몸 높이 2.1u에 대해 테스트 Prefab Variant만 `40 / 210` 배율로 축소합니다.
먹의 아래 번짐과 붓털 장식은 25px 충돌 몸체 밖으로 조금 보일 수 있습니다.

`platform_1`~`platform_6`를 한 묶음씩 섞어 생성하며 묶음 경계에서도 같은 그림의 연속을 피합니다.
일반 플랫폼의 보행 폭은 2.0~3.6u이고 출발 플랫폼은 안전한 출발 공간을 확보하도록 넓어질 수 있습니다.
그림은 원래 가로세로 비율을 유지하고, 보행 윗면에 맞춰 배치합니다.
첫 플랫폼은 비우고 이후 1~3개 플랫폼 간격으로 `obstacle_1`~`obstacle_2`가 번갈아 나타납니다.
장애물의 보이는 높이는 기본 55~95px입니다.

지형과 그린 먹은 월드에 고정되고 캐릭터와 카메라가 전진해 옆으로 흐르는 느낌을 만듭니다.
카메라 오른쪽 6u 앞까지 생성하고 뒤로 4u 이상 벗어난 플랫폼과 장식은 정리합니다.
활성 플랫폼은 최대 32개로 제한해 오래 달려도 무한히 쌓이지 않게 했습니다.
기본 랜덤 시드는 `27102026`이며 같은 화면 크기와 설정에서 Reset하면 같은 경로를 재생합니다.

## Inspector에서 조절

캐릭터·먹 설정은 **Black Ink Endless Runner - select for tuning**의 `BlackInkTestController`에서 조절합니다.

| 값 | 기본값 | 설명 |
| --- | --- | --- |
| Character Height Pixels / Ink Thickness Pixels | 40 / 25 | 몸 높이 / 먹 두께 |
| Lock To Screen Pixels / Pixels Per World Unit | ON / 100 | 실제 Game 뷰 픽셀 유지 / 월드 환산 |
| Reference Pixel Height | 1080 | 픽셀 고정을 끌 때 사용하는 기준 화면 높이 |
| Runner Viewport X / Ground Viewport Y | 0.08 / 0.27 | 왼쪽부터 캐릭터 위치 / 아래부터 지면 위치 |
| Preparation Seconds / Run Speed | 3 / 1.8 | 준비 시간 / 초당 이동 월드 거리 |
| Maximum Ink / Cost Per Unit | 100 / 10 | 전체 잉크 / 생성한 길이 1u당 비용 |
| Maximum Slope | 35° | 먹 윗면의 허용 기울기 |
| Clear Previous On New Stroke | OFF | 켜면 새 유효 붓질 시작 시 기존 먹 제거, 잉크 환급 없음 |
| Maximum Points / Segments / Strokes | 2048 / 1984 / 64 | 먹 저장 상한 |

맵 설정은 **Endless World - random platforms + visual obstacles**의 `EndlessInkWorld`에서 조절합니다.

| 값 | 기본값 | 설명 |
| --- | --- | --- |
| Minimum / Maximum Platform Width | 2 / 3.6 | 플랫폼 보행 폭 범위(u) |
| Platform Gap | 0 | 플랫폼 사이 간격(u). 늘리면 먹 다리 테스트 가능 |
| Generate Ahead Distance / Cleanup Behind Distance | 6 / 4 | 카메라 앞 생성 거리 / 뒤 정리 여유(u) |
| Maximum Active Platforms / Random Seed | 32 / 27102026 | 활성 플랫폼 상한 / 재현용 시드 |
| Minimum / Maximum Platforms Between Obstacles | 1 / 3 | 장식 배치 간격 |
| Minimum / Maximum Obstacle Height | 0.55 / 0.95 | 보이는 장식 높이(u) |

무한 모드의 플랫폼 간격은 **World의 Platform Gap**으로 바꿉니다.
Controller의 동명 필드는 예전 두 플랫폼 모드용입니다. 조정값을 남기려면 Play를 종료한 상태에서 값을 바꾸고 씬을 저장하세요.

## 파일 위치와 보존 범위

모든 구현은 `Assets/Features/BlackInkPrototype/`에 있습니다.

| 경로 | 역할 |
| --- | --- |
| `Scenes/BlackInkTestScene.unity` | 기존 테스트 씬을 무한 달리기로 갱신 |
| `Prefabs/Inkwarrior_BlackInkTest.prefab` | 기존 캐릭터를 상속하는 크기·물리 설정 Variant |
| `Scripts/BlackInkTestController.cs`, `Scripts/EndlessInkWorld.cs` | 자동 달리기·카메라·입력·먹 / 맵 생성·정리 |
| `Scripts/InkPathMath.cs`, `Scripts/InkStrokeGeometry.cs`, `Scripts/BlackInkHud.cs` | 경로 검사·충돌 메시·게이지와 버튼 |
| `Textures/Environment/` | 제공한 `platform_1.png`~`platform_6.png`, `obstacle_1.png`~`obstacle_2.png` |
| `Textures/InkGaugeSource.png`, `Materials/` | 기존 붉은 게이지 원본과 먹·지형 재질 |
| `Editor/BlackInkSceneBuilder.cs`, `Editor/EnvironmentAssetImporter.cs` | 씬 재생성 / 제공 PNG의 Sprite 임포트 |
| `Editor/EndlessInkValidation.cs`, `Editor/BlackInkValidation.cs` | 새 검증 / 예전 메뉴와 실행 경로 호환 |

Downloads의 확장자 없는 제공 이미지 8개는 픽셀을 수정하지 않고 `.png` 이름으로 복사했습니다.
복사본과 제공 원본 파일의 바이트가 동일하며, 붉은 게이지 원본도 보존합니다.
원본 `Assets/Prefabs/Inkwarrior.prefab`, 캐릭터 이미지·Animator·Run 애니메이션, 공용 스크립트와 공용 씬은 유지합니다.
현재 작업 브랜치는 `InkTest`이며 이번 변경의 Git Commit·Push는 수행하지 않았습니다.
공유할 때는 기능 폴더의 파일과 모든 `.meta`, 상위 `Assets/Features.meta`도 함께 포함하세요.

## 입력과 검증

기존 먹 생성·길이별 잉크 소모·Reset·Refill·붉은 게이지를 유지합니다.
마우스를 가만히 누른 채 카메라만 움직이면 먹이 생성되거나 잉크가 소모되지 않습니다.
이때 붓질의 연결을 끝내고 실제 마우스 이동이 다시 생기면 새 위치에서 이어 그립니다.
UI 위 입력, 캐릭터·금지 영역과 겹치는 두꺼운 몸체, 급경사와 방향 반전은 기존 제한을 적용합니다.

메뉴 `Tools > InkRunner > Validate Endless Ink Runner (Play Mode)`로 재검증합니다.
예전 `Validate Black Ink Prototype (Play Mode)` 메뉴도 같은 새 검증을 실행합니다.
결과와 화면은 `Logs/EndlessInkValidationResults/`에 저장되며 최신 결과는 `ValidationResults.md`를 확인하세요.
2026-10-27 실제 Unity 검증은 **32개 모두 PASS**이며, 시뮬레이션 120초 동안 200u 이상 달리기를 확인했습니다.
상세 자동 기록: [EndlessInkValidationReport.md](Documentation/EndlessInkValidationReport.md)
2026-10-20의 32개 통과 기록과 기존 스크린샷은 이전 두 플랫폼 버전의 기록이며 현재 씬의 검증 결과가 아닙니다.
공용 씬 통합, 장애물 충돌, 점프·흰 먹·잉크 자동 회복 및 별도 Player 빌드는 이번 변경 범위 밖입니다.
