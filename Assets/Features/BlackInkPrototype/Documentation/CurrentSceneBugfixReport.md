# 현재 저장된 씬 A / B / C 수정 기록

작업일: **2026-10-31 (한국 시간)** · 기존 `InkTest` 브랜치

## 1. 작업 범위와 실행 경로

기존 `Assets/Features/BlackInkPrototype/Scenes/BlackInkTestScene.unity`와 달리기·먹 시스템을 수정합니다.
새 게임이나 새 씬을 만들지 않고 팀이 추가한 배경·저장된 씬을 유지합니다. 씬 재생성 도구는 실행하지 않습니다.
TitleScene의 Start와 기존 코어 `SceneManager.LoadPlay()`가 같은 `BlackInkTestScene`을 열도록 연결합니다.
기존 `PlayScene`은 카메라·원본 캐릭터만 있어 현재 먹 시스템의 실행 경로로 적합하지 않았습니다.
Build Settings의 기존 씬 순서를 유지하고 사용 중인 `BlackInkTestScene`을 마지막에 추가합니다.

직접 확인하려면 TitleScene에서 Start를 누르거나 메뉴 `Tools > InkRunner > Open Black Ink Test Scene`으로 엽니다.
Play 후 기존 3초 준비 동안 빈틈을 먹으로 연결합니다. Reset 후에도 플랫폼이 배경 위에 보이는지 확인합니다.

## 2. A — 플랫폼이 안 보이는 원인과 수정

저장된 씬의 미리보기 플랫폼은 그리기 순서 1이지만, Play의 Reset에서 생성한 플랫폼은 0이었습니다.
배경 `background1_0`도 Default 레이어의 그리기 순서 0이므로 플랫폼이 배경에 가려졌습니다.
원본 검사에서 플랫폼 Sprite·알파·재질·카메라 레이어와 출발점 지지 Collider는 존재했습니다.
수정 전 실제 렌더: [플랫폼이 배경에 가린 화면](ValidationScreenshots/Current_Before_PlatformsHidden.png)

`EndlessInkWorld.CreatePlatform()`에서 생성하는 플랫폼 SpriteRenderer의 그리기 순서를 **0 → 1**로 맞췄습니다.
배경의 이미지·정렬·위치를 바꾸거나 강제로 숨기지 않습니다. 생성·Reset 경로에 같은 수정이 적용됩니다.
최종 실제 URP 렌더 차분의 플랫폼 가시 비율은 **0.9935**이며, 저장된 배경이 켜진 상태에서 확인했습니다.
확인 화면: [준비 중 지형](ValidationScreenshots/Current_After_Preparing.png), [Title Start로 진입](ValidationScreenshots/Current_After_TitleStart.png)

## 3. B — 기존 준비 시간 확인, 새 카운트다운 생략

기존 UI에는 `READY 3.0s`처럼 줄어드는 소수점 초 표시와 한 개의 준비 상태가 있습니다.
별도 3/2/1 숫자 UI나 여러 카운트다운 객체가 없으므로 새 UI·타이머·코루틴을 추가하지 않았습니다.
기존 `preparationSeconds`와 상태 전환도 변경하지 않았습니다.

수정 전 실제 Play Mode 측정에서 초기 출발은 3.0001초, 준비 중 Reset은 3.0013초,
달리는 중 Reset은 3.0020초, 빠른 Reset 10회 후 최종 출발은 3.0010초였습니다.
각 경우 마지막 Reset부터 한 번만 Running으로 전환했고 이전 타이머로 조기 출발하지 않았습니다.
준비 중 그리기는 가능하고 캐릭터·맵·이동 거리는 수평으로 진행하지 않았습니다.
최종 재검증에서도 초기 3.0006초, 준비·달리기 중 Reset 각각 3.0001초, 연속 Reset 후 3.0005초로 정상 전환했습니다.
기존 Start 버튼 UnityEvent를 두 번 연속 호출했을 때 현재 씬 로드는 1회였고 Controller·World·EventSystem이 중복되지 않았습니다.

## 4. C — 완만한 경사에서 먹이 잘리고 끼는 원인과 수정

몸 0.4u × 최소 간격 비율 0.025 = **0.01u, 화면 1px**이 기존 입력 간격입니다.
정수 픽셀로 들어오는 20° 선에도 `(1px, 1px)`처럼 국소적으로 45°인 표본이 섞입니다.
이 표본을 즉시 35° 초과로 거절하며 붓질을 끝내면 선이 조각나고 64개 붓질 상한을 일찍 소진합니다.

수정 전 1px 간격·왼쪽부터 그린 20° 입력은 64개 붓질로 나뉘어 약 2.1203u만 생성됐고 캐릭터가 도착하지 못했습니다.
10°도 40개, 30°도 64개로 나뉘었습니다. 이는 하나의 매끄러운 Collider만 놓는 검사로는 드러나지 않는 입력 경로 문제입니다.
기존 Capsule·중력·연속 충돌·마찰 0 설정과 단일 연속 경사 Collider의 통과 동작은 별도로 확인했습니다.

`BlackInkTestController.AppendTo()`에서 **최소 간격 6배 미만인 짧은 초과 경사 표본**은
방향 반전이나 금지 영역이 아닐 때 마지막 유효 기준점을 유지하고 다음 입력까지 판정을 보류합니다.
기본 40px 몸 높이에서는 6px 미만에 해당합니다.
보류된 표본은 먹이나 비용을 만들지 않습니다. 이후 유효 선분은 기존 35° 검사와 실제 생성 길이 과금을 따릅니다.
명확한 방향 반전·긴 급경사·캐릭터와 금지 영역의 두꺼운 몸체 검사는 유지합니다.

Collider 형상, 수평 이동 코드, 캐릭터 물리 설정과 먹 아래 방향 15px 두께를 바꾸지 않았습니다.
먹이 지면보다 15px 높게 시작해 생기는 수직 입구 벽은 경사 중간 걸림과 구분합니다.
직접 만든 15px 수직 단차를 자동으로 뛰거나 끌어올리는 기능은 추가하지 않습니다.
그릴 때 현재 발판의 윗면에서 시작해 다음 발판의 윗면까지 완만한 경사로 연결하세요.

최종 10°·20°·30° 정수 입력은 1px·2px 간격과 양방향 총 12개 조합 모두 **1개 붓질·실제 횡단**을 통과했습니다.
20°·1px·좌→우는 64개 조각·2.1203u에서 **1개 붓질·5.2007u**로 개선됐습니다.
예시 이미지는 [20° 입력으로 생성한 먹](ValidationScreenshots/Current_After_Uphill20.png)입니다. 횡단 순간 사진은 아니며 실제 횡단은 원본 보고서와 CSV로 확인합니다.

안정적인 연속 입력 검증 범위는 **30° 이하**입니다. 기존 생성 선분의 하드 상한은 **35°**입니다.
경계 진단에서 34°는 9개, 35°는 34~35개 붓질로 여전히 분할됐습니다. 해당 측정의 횡단은 성공했지만 한 붓질 보장 범위에 넣지 않습니다.
40°·45°는 완전한 다리와 횡단을 지원하지 않습니다. 실제 생성 선분 중 35° 초과는 0개였습니다.
15px 수직 입구 벽은 자동으로 오르지 않았고 별도 측정에서 5.34초 정지했습니다.

## 5. 검증 방법과 확인 범위

현재 씬 검증 메뉴: `Tools > InkRunner > Validate Saved Current Scene (A B C)`

기존 저장 씬을 열고 같은 캐릭터·`Rigidbody2D`·`CapsuleCollider2D`·먹 메시·재질·이동 코드를 실행합니다.
입력 검사는 실제 OS 마우스 대신 공개 `ProcessPointer` / `AppendTo` 경로에 정수 픽셀 표본을 전달합니다.
분리된 경사 물리 검사는 Editor 전용 임시 지형을 두고 0.02초마다 기존 `FixedUpdate`와 `Physics2D.Simulate`를 실행합니다.
임시 설정·배경 렌더 검사와 시간 배율은 종료 시 복원합니다. 저장된 팀 씬을 재작성하지 않습니다.

최종 현재 씬 회귀·경계 진단은 **134개 PASS / 실패 0개**입니다.
이는 경계 입력의 상한 준수 검사도 포함하며 모든 35° 입력의 연속성이나 40° 이상 횡단을 보장한다는 뜻은 아닙니다.
요약: [ValidationResults.md](../ValidationResults.md).
같은 최종 소스로 기존 규칙 회귀도 **41개 모두 PASS**했습니다: [기존 규칙 재검증 원본](CurrentSceneLegacyRegressionReport.md).
120초·200u 이상 달리기는 Editor 전용 입력 다리 **46개·명시 Refill 11회** 보조입니다. 일반 게임에는 자동 다리·자동 Refill이 없습니다.
상세 원본: [현재 씬 검증 보고서](CurrentSceneValidationReport.md), [물리 측정 CSV](CurrentScenePhysicsMetrics.csv), [경사 제한 진단](CurrentSceneSlopeLimitsReport.md).
수정 전 비교: [초기 A/B/C 검사](CurrentSceneBaselineReport.md), [정수 픽셀 입력 검사](CurrentScenePointerBaselineReport.md).
실제 OS 마우스·모든 창 크기에서의 수동 조작과 별도 Player 빌드는 이 자동 검증과 구분합니다.

## 6. 변경 파일과 보존 범위

| 경로 | 변경 이유 |
| --- | --- |
| `Scripts/EndlessInkWorld.cs` | 런타임 생성 플랫폼의 그리기 순서 1 |
| `Scripts/BlackInkTestController.cs` | 짧은 정수 픽셀 표본의 경사 판정 보류 |
| `Editor/CurrentSceneBugValidation.cs` | 저장된 현재 씬의 A/B/C 재현·회귀 검증 |
| `Editor/EndlessInkValidation.cs` | 기존 배치 진입점에서 씬 재생성 대신 저장 씬 검증 |
| `Assets/Script/TitleUIManager.cs` | 기존 Title Start에서 현재 씬 진입 |
| `Assets/Script/Core/SceneManager.cs` | 기존 LoadPlay에서 현재 씬 진입 |
| `ProjectSettings/EditorBuildSettings.asset` | 같은 기존 씬을 마지막에 등록 |

위 표의 `Scripts/`, `Editor/`는 `Assets/Features/BlackInkPrototype/` 기준입니다.
팀의 씬·배경, 원본 캐릭터 Prefab·이미지·Animator, Unity·URP 버전과 패키지를 보존합니다.
몸 40px·먹 15px·빈틈 150~250px 기본값을 유지합니다. Git Commit·Push는 수행하지 않았습니다.
이전 2026-10-27의 [41개 통과 기록](RandomGapInkValidationReport.md)은 그 시점의 기록으로 보존합니다.
