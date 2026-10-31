# 검은 먹 — 무한 달리기 테스트

## 실행

Unity **6000.3.23f1**에서 아래 전용 씬을 열고 **Play**를 누릅니다.

`Assets/Features/BlackInkPrototype/Scenes/BlackInkTestScene.unity`

메뉴 `Tools > InkRunner > Open Black Ink Test Scene`으로도 열 수 있습니다.
TitleScene의 **Start**와 기존 코어 `SceneManager.LoadPlay()`도 이 저장된 씬을 엽니다.
Build Settings에는 기존 씬들의 순서를 유지한 채 `BlackInkTestScene`을 마지막에 등록했습니다.
시작 후 3초 준비 시간이 지나면 기존 Inkwarrior가 오른쪽으로 계속 달립니다.
캐릭터는 화면 왼쪽 약 8% 위치에서 출발하고, 카메라가 따라가며 앞쪽 맵을 생성합니다.
플랫폼 사이에는 **150~250px의 랜덤한 빈틈**이 있습니다.
캐릭터가 도착하기 전에 양쪽 플랫폼의 윗면을 먹으로 연결해야 하며, 그리지 않으면 추락합니다.

- **좌클릭 드래그:** 경로를 윗면으로 하는 검은 먹 플랫폼 생성. 생성한 길이에 따라 잉크 소모.
- **Reset:** 캐릭터·먹·잉크·준비 시간을 초기화하고 같은 랜덤 순서의 맵으로 다시 시작.
- **Refill:** 현재 위치와 길을 유지하고 잉크만 채움. 마우스를 놓고 새로 클릭해 그립니다.
- **HUD:** 실제 Game 뷰 기준 몸 높이·먹 두께, 이동 거리, 현재 플랫폼·장식 수 표시.

`obstacle_1`, `obstacle_2`는 장식입니다. Collider나 충돌·피격·게임 오버 판정이 없습니다.
기본 TMP 폰트의 한글 글리프 제한으로 테스트 HUD는 영문으로 표시합니다.

## 크기와 맵 생성 방식

기본값은 **캐릭터 몸 높이 40px, 먹의 충돌 몸체 두께 15px**입니다.
이는 텍스처 파일 크기가 아니라 실제 Game 뷰 렌더링 픽셀 기준입니다.
`Lock To Screen Pixels`가 켜져 있으면 창 크기에 맞춰 카메라 범위를 조절합니다.
Game 뷰 표시 배율이 1×가 아니면 에디터 화면에서 재는 크기는 달라질 수 있습니다.
몸 높이는 기존 `run_01`의 머리~발바닥 약 210px를 기준으로 하며 무기·망토·여백을 제외합니다.
애니메이션 포즈별 외곽선은 변하지만 기준 높이와 먹 두께는 고정합니다.

100px = 1 월드 유닛이므로 몸은 0.4u, 먹은 0.15u입니다. 두께 비율은 `15 / 40 = 0.375`입니다.
원본 몸 높이 2.1u에 대해 테스트 Prefab Variant만 `40 / 210` 배율로 축소합니다.
먹의 아래 번짐과 붓털 장식은 15px 충돌 몸체 밖으로 조금 보일 수 있습니다.

`platform_1`~`platform_6`를 한 묶음씩 섞어 생성하며 묶음 경계에서도 같은 그림의 연속을 피합니다.
일반 플랫폼의 보행 폭은 2.0~3.6u이고 출발 플랫폼은 안전한 출발 공간을 확보하도록 넓어질 수 있습니다.
각 플랫폼 사이의 빈틈은 1.5~2.5u, 즉 현재 Game 뷰에서 150~250px 범위로 무작위 선택합니다.
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
| Character Height Pixels / Ink Thickness Pixels | 40 / 15 | 몸 높이 / 먹 두께 |
| Lock To Screen Pixels / Pixels Per World Unit | ON / 100 | 실제 Game 뷰 픽셀 유지 / 월드 환산 |
| Reference Pixel Height | 1080 | 픽셀 고정을 끌 때 사용하는 기준 화면 높이 |
| Runner Viewport X / Ground Viewport Y | 0.08 / 0.27 | 왼쪽부터 캐릭터 위치 / 아래부터 지면 위치 |
| Preparation Seconds / Run Speed | 3 / 1.8 | 준비 시간 / 초당 이동 월드 거리 |
| Maximum Ink / Cost Per Unit | 100 / 10 | 전체 잉크 / 생성한 길이 1u당 비용 |
| Maximum Slope | 35° | 생성 선분의 상한. 안정적인 정수 픽셀 연속 입력은 30° 이하 검증 |
| Clear Previous On New Stroke | OFF | 켜면 새 유효 붓질 시작 시 기존 먹 제거, 잉크 환급 없음 |
| Maximum Points / Segments / Strokes | 2048 / 1984 / 64 | 먹 저장 상한 |

맵 설정은 **Endless World - random platforms + visual obstacles**의 `EndlessInkWorld`에서 조절합니다.

| 값 | 기본값 | 설명 |
| --- | --- | --- |
| Minimum / Maximum Platform Width | 2 / 3.6 | 플랫폼 보행 폭 범위(u) |
| Minimum Platform Gap / Maximum Platform Gap | 1.5 / 2.5 | 플랫폼 사이 랜덤 간격(u). 화면 기준 150~250px |
| Generate Ahead Distance / Cleanup Behind Distance | 6 / 4 | 카메라 앞 생성 거리 / 뒤 정리 여유(u) |
| Maximum Active Platforms / Random Seed | 32 / 27102026 | 활성 플랫폼 상한 / 재현용 시드 |
| Minimum / Maximum Platforms Between Obstacles | 1 / 3 | 장식 배치 간격 |
| Minimum / Maximum Obstacle Height | 0.55 / 0.95 | 보이는 장식 높이(u) |

무한 모드의 플랫폼 간격은 **World의 Minimum Platform Gap / Maximum Platform Gap**으로 바꿉니다.
Controller의 `Platform Gap` 필드는 예전 두 플랫폼 모드용입니다. 조정값을 남기려면 Play를 종료한 상태에서 값을 바꾸고 씬을 저장하세요.

## 파일 위치와 보존 범위

달리기·먹 구현은 `Assets/Features/BlackInkPrototype/`에 있습니다.

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
| `Editor/CurrentSceneBugValidation.cs` | 현재 저장된 씬의 지형 가시성·기존 준비 시간·먹 경사 회귀 검증 |

기존 진입 연결은 `Assets/Script/TitleUIManager.cs`, `Assets/Script/Core/SceneManager.cs`에서
`BlackInkTestScene`을 로드하도록 수정했고, `ProjectSettings/EditorBuildSettings.asset`에 같은 씬을 추가했습니다.

Downloads의 확장자 없는 제공 이미지 8개는 픽셀을 수정하지 않고 `.png` 이름으로 복사했습니다.
복사본과 제공 원본 파일의 바이트가 동일하며, 붉은 게이지 원본도 보존합니다.
원본 `Assets/Prefabs/Inkwarrior.prefab`, 캐릭터 이미지·Animator·Run 애니메이션, 팀의 저장된 씬과 배경을 유지합니다.
2026-10-31 수정은 기존 코드와 저장된 씬을 사용하며 씬 재생성 도구를 실행하지 않았습니다.
현재 작업 브랜치는 `InkTest`이며 이번 변경의 Git Commit·Push는 수행하지 않았습니다.
공유할 때는 기능 폴더의 파일과 모든 `.meta`, 상위 `Assets/Features.meta`도 함께 포함하세요.

## 입력과 검증

기존 먹 생성·길이별 잉크 소모·Reset·Refill·붉은 게이지를 유지합니다.
마우스를 가만히 누른 채 카메라만 움직이면 먹이 생성되거나 잉크가 소모되지 않습니다.
이때 붓질의 연결을 끝내고 실제 마우스 이동이 다시 생기면 새 위치에서 이어 그립니다.
UI 위 입력, 캐릭터·금지 영역과 겹치는 두꺼운 몸체, 급경사와 방향 반전은 기존 제한을 적용합니다.

2026-10-31에는 Reset으로 다시 생성한 플랫폼이 배경 뒤에 가리던 문제를 그리기 순서로 수정했습니다.
1px 단위 입력이 완만한 경사를 짧은 45° 조각으로 잘라 먹을 끊던 문제는 짧은 입력의 판정을 보류해 수정했습니다.
기존 35° 제한·먹 15px·충돌 형상·이동 코드·실제 생성 길이 과금은 유지합니다.
기존 준비 표시는 `READY 3.0s`처럼 소수점으로 줄어드는 한 개이며, 새 3/2/1 UI나 타이머를 추가하지 않았습니다.

현재 씬의 문제별 확인은 `Tools > InkRunner > Validate Saved Current Scene (A B C)`로 실행합니다.
저장된 씬을 열어 Play Mode에서 확인하며 씬을 재생성하지 않습니다.
결과는 `Logs/CurrentSceneBugValidationResults/`에 저장됩니다.
최신 결과는 [ValidationResults.md](ValidationResults.md), 원인과 재현은 [CurrentSceneBugfixReport.md](Documentation/CurrentSceneBugfixReport.md)를 확인하세요.
최신 현재 씬 회귀·경계 진단은 **134개 PASS / 실패 0개**입니다.
같은 최종 소스로 기존 규칙도 재검증해 [41개 모두 PASS](Documentation/CurrentSceneLegacyRegressionReport.md)했습니다.
10°·20°·30°의 1px·2px 양방향 입력은 모두 한 붓질·실제 횡단을 확인했습니다.
**34~35° 정수 입력은 여전히 분할될 수 있습니다.** 생성 선분 상한 35°와 입력의 연속 지원 범위를 구분합니다.
15px 수직 입구 벽은 자동으로 오르지 않으므로 현재 발판 윗면에서부터 완만하게 연결합니다.
입력은 OS 마우스 대신 `ProcessPointer` 경로를 호출했고 같은 캐릭터의 실제 Physics2D를 실행했습니다.

메뉴 `Tools > InkRunner > Validate Endless Ink Runner (Play Mode)`로 재검증합니다.
예전 `Validate Black Ink Prototype (Play Mode)` 메뉴도 같은 새 검증을 실행합니다.
결과와 화면은 `Logs/EndlessInkValidationResults/`에 저장되며 최신 결과는 `ValidationResults.md`를 확인하세요.
2026-10-27의 이전 검증도 **41개 PASS**로 역사 기록을 유지합니다.
랜덤 빈틈 150~250px의 범위·다양성·Reset 재현, 먹 없이 추락, 그린 15px 먹 다리의 실제 횡단을 확인했습니다.
최신 재검증의 장시간 120초·200u 이상 달리기도 **Editor 검증 코드의 입력 다리 46개·명시적 Refill 11회**로 보조했습니다.
일반 Play에는 자동 다리·자동 Refill이 없습니다. 원본 프로젝트가 열려 있어 `Logs/RandomGapValidationProject/` 복사본에서 검증했습니다.
상세 자동 기록: [RandomGapInkValidationReport.md](Documentation/RandomGapInkValidationReport.md)
검색 인덱서의 Editor 환경 예외는 원본 보고서에 별도 기록했으며 프로토타입 런타임 오류는 없었습니다.
기존 [32개 통과 기록](Documentation/EndlessInkValidationReport.md)은 간격 0u·먹 25px 버전의 결과로 유지합니다.
2026-10-20의 32개 통과 기록과 기존 스크린샷은 이전 두 플랫폼 버전의 기록이며 현재 씬의 검증 결과가 아닙니다.
Title 진입 연결과 별개로 장애물 충돌, 점프·흰 먹·잉크 자동 회복 및 별도 Player 빌드는 이번 변경 범위 밖입니다.
