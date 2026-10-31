# 현재 씬 A / B / C 수정 및 검증 결과

검증일: **2026-10-31 (한국 시간)** · Unity **6000.3.23f1**

기존 저장된 `BlackInkTestScene`과 같은 소스로 실제 Unity Play Mode·Physics2D를 실행했습니다.
원본 Unity 프로젝트가 열려 있어 동일 소스를 격리 복사한 프로젝트에서 검증했습니다.
최종 현재 씬 회귀·경계 진단은 **134개 PASS / 실패 0개**입니다.
같은 최종 소스로 기존 랜덤 맵·먹·Reset·자원 규칙도 다시 실행해 **41개 모두 PASS**했습니다.
안정적인 연속 입력을 검증한 경사 범위는 **30° 이하**이며, 생성 선분의 기존 **35° 상한**과 구분합니다.

원본: [현재 씬 검증 보고서](Documentation/CurrentSceneValidationReport.md) · [물리 측정 CSV](Documentation/CurrentScenePhysicsMetrics.csv) · [경사 경계 진단](Documentation/CurrentSceneSlopeLimitsReport.md)

## 1. 실행 경로와 재현

TitleScene에서 **Start**를 누르거나 `Tools > InkRunner > Open Black Ink Test Scene`을 선택합니다.
실행 씬은 기존 `Assets/Features/BlackInkPrototype/Scenes/BlackInkTestScene.unity`입니다. 새 게임·새 씬을 만들지 않았습니다.

Play 후 기존 3초 준비 중 빈틈 양쪽 플랫폼의 **윗면부터 윗면까지** 좌클릭 드래그로 연결합니다.
캐릭터 40px, 먹 충돌 몸체 15px, 랜덤 빈틈 150~250px를 유지합니다.
Reset 후에도 배경 위에 플랫폼이 보이고, 완만한 경사 먹을 따라 달리는지 확인할 수 있습니다.

## 2. 문제별 원인과 처리

| 항목 | 확인한 원인 | 처리 |
| --- | --- | --- |
| A: 플랫폼이 안 보임 | 저장 미리보기의 순서는 1이지만 Reset이 만든 플랫폼은 배경과 같은 순서 0 | 런타임 생성 플랫폼의 SpriteRenderer 순서를 1로 맞춤. 배경은 유지 |
| B: 준비 시간 의심 | 기존 `READY 3.0s` 소수점 표시 한 개이며 초기·Reset 출발 시간이 정상 | 준비 코드 유지. 새 3/2/1 UI·타이머·코루틴 추가 생략 |
| C: 완만한 경사 먹이 끊김·끼임 | 1px 표본의 `(1,1)` 계단이 45°로 판정돼 붓질이 잘리고 64개 상한을 소진 | 6px 미만의 짧은 초과 경사 표본은 기준점을 유지해 판정 보류. 유효 생성 길이만 과금 |
| 기존 Start의 실행 대상 | 이전 `PlayScene`에는 현재 먹 시스템이 없음 | 기존 Start·LoadPlay를 저장된 `BlackInkTestScene`으로 연결하고 Build Settings 마지막에 등록 |

C 수정은 방향 반전·금지 영역·긴 급경사 제한을 유지합니다. Collider 형상·캐릭터 이동 코드·물리 설정을 바꾸지 않았습니다.

## 3. 실제 검증 결과

- **A 가시성:** 생성 플랫폼 순서 1 / 배경 순서 0을 확인했습니다. 실제 URP 렌더 차분의 가시 비율은 **0.9935**이며 출발 발판의 물리 지지도 확인했습니다.
- **B 준비:** 최초 Running 전환은 **3.0006초**였습니다. 준비 중 Reset과 달리는 중 Reset은 각각 **3.0001초**, 빠른 Reset 10회 후에는 **3.0005초**에 한 번만 출발했습니다. 준비 중 수평 이동·거리 진행은 0이었습니다.
- **실제 입력 경로 C:** 정수 픽셀 간격 1px·2px와 좌→우·우→좌 입력으로 **10°·20°·30° 총 12개 조합**을 검사했습니다. 모두 한 붓질로 이어졌고 실제 캐릭터가 횡단했으며 지속적인 경사 중간 정지가 없었습니다.
- **수정 전후:** 20°·1px·좌→우는 **64개 붓질 / 2.1203u 생성 / 도착 실패 → 1개 붓질 / 5.2007u 생성 / 도착 성공**으로 바뀌었습니다. 생성된 길이에 맞는 과금도 통과했습니다.
- **먹 물리:** 평지, ±10°·±20°·±35°의 연속 형상, 평지→경사→내리막 전환, 300·600점 밀집 경로, 작은 흔들림, 캐릭터가 밟은 상태의 먹 연장을 확인했습니다. 이 형상 검사는 아래 정수 픽셀 입력 한계와 별개입니다.
- **Title 진입:** 기존 배선된 Start 버튼의 UnityEvent를 두 번 연속 호출해 **씬 전환 1회**, 현재 Controller·World 초기화와 EventSystem 1개, 배경 보존을 확인했습니다. 진입 직후 기존 준비 시간은 2.9745초 남아 있었습니다.
- **기존 규칙 회귀:** 현재 소스로 41개를 재검증해 40px/15px, 빈틈 150~250px, 길이별 비용, Refill·Reset, 스크롤·정리·추락을 확인했습니다. 120초·200u 이상 달리기는 **Editor 입력 다리 46개·명시 Refill 11회** 보조 결과이며 관측 활성 수는 플랫폼 6개·장식 4개였습니다. 일반 Play에는 자동 다리·자동 Refill이 없습니다.

그리기 입력은 실제 OS 마우스가 아니라 **공개 `ProcessPointer` / `AppendTo` 경로**로 전달했습니다.
분리된 경사 측정은 같은 캐릭터·물리·먹 코드를 사용하고 Editor 전용 임시 지형과 시작 위치를 두어,
0.02초 간격으로 기존 `FixedUpdate` 및 `Physics2D.Simulate`를 실행했습니다. 배경·임시 설정·시간 배율은 종료 시 복원합니다.

## 4. 경사 지원 범위와 남은 한계

| 입력/형상 | 관측 결과 | 해석 |
| --- | --- | --- |
| 10°·20°·30°, 1px·2px, 양방향 입력 | 12개 조합 모두 한 붓질·실제 횡단 | 안정적인 연속 입력 검증 범위는 30° 이하 |
| 34° 정수 픽셀 입력 | 9개 붓질로 분할, 해당 측정에서는 횡단 | 한 번에 이어지는 입력 보장 범위에 포함하지 않음 |
| 35° 정수 픽셀 입력 | 34~35개 붓질로 분할, 해당 측정에서는 횡단 | 같은 제한. 모든 35° 입력을 연속 지원한다는 의미가 아님 |
| 40°·45° 입력 | 완전한 다리 횡단 실패 | 지원 범위 밖. 유효한 일부 조각만 생성하거나 생성하지 않음 |
| 실제 생성 선분 | 35° 상한을 넘는 선분 0개 | 기존 생성 제한 유지 |
| 15px 높이의 별도 수직 입구 벽 | 자동으로 올라가지 않음, 5.34초 정지 관측 | 경사 중간 걸림과 구분. 90° 턱 자동 오르기 기능 없음 |

134개 통과에는 경계 입력에서 **35° 제한을 지키는지 확인한 항목**도 포함됩니다.
경계 진단은 상한 준수를 검사하며, 34~35°의 연속성이나 40~45°의 횡단을 성공으로 판정하지 않습니다.
현재 발판보다 15px 위에서 갑자기 시작하는 먹은 수직 턱이 됩니다. 발판 윗면부터 완만하게 이어 그리세요.
실제 OS 마우스·포커스·모든 Game 뷰 크기에서의 수동 조작과 별도 Player 빌드는 검증하지 않았습니다.
Editor 검색 인덱서의 `ArgumentOutOfRangeException`은 원본 보고서에 환경 관찰로 별도 기록했습니다.

## 5. 파일과 검증 실행

상세 변경 파일·원인·수정 전 재현은 [CurrentSceneBugfixReport.md](Documentation/CurrentSceneBugfixReport.md)를 확인하세요.

- [수정 전 배경에 가려진 플랫폼](Documentation/ValidationScreenshots/Current_Before_PlatformsHidden.png)
- [수정 후 준비 화면](Documentation/ValidationScreenshots/Current_After_Preparing.png)
- [기존 Title Start로 진입](Documentation/ValidationScreenshots/Current_After_TitleStart.png)
- [실제 입력 경로로 생성한 20° 먹 — 횡단 순간 사진은 아님](Documentation/ValidationScreenshots/Current_After_Uphill20.png)

메뉴 `Tools > InkRunner > Validate Saved Current Scene (A B C)`로 재검증합니다.
저장된 현재 씬을 열며 씬 재생성 도구를 호출하지 않습니다. 결과는 실행 프로젝트의 `Logs/CurrentSceneBugValidationResults/`에 저장됩니다.
이번 최종 원본 출력은 `Logs/CurrentBugsFinalResults/`에 보존했습니다.
기존 규칙 재검증 원본: [CurrentSceneLegacyRegressionReport.md](Documentation/CurrentSceneLegacyRegressionReport.md).

## 6. 보존 범위와 이전 기록

팀의 저장된 씬·배경, 원본 Inkwarrior Prefab·이미지·Animator, Unity·URP·패키지는 유지합니다.
기존 Title·코어 진입 코드와 Build Settings만 현재 실행 대상에 맞춰 연결했고 새 준비 UI·게임 시스템은 추가하지 않았습니다.
기존 `InkTest` 브랜치에서 작업했으며 **Git Commit·Push는 수행하지 않았습니다.**

[2026-10-27 랜덤 빈틈 41개 기록](Documentation/RandomGapInkValidationReport.md), [이전 25px·붙은 지형 기록](Documentation/EndlessInkValidationReport.md), [2026-10-20 기록](Documentation/UnityValidationReport.md)과 해당 스크린샷은 역사 기록으로 유지합니다.
이전 장시간 달리기 기록도 Editor 입력 다리·명시 Refill 보조 결과로 현재 회귀와 같은 방식이며 일반 게임의 자동 기능이 아닙니다.
