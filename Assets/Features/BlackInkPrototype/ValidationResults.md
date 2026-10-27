# 랜덤 빈틈·15px 먹 구현 및 검증 결과

검증일: **2026-10-27 (한국 시간)**

Unity **6000.3.23f1**의 Play Mode와 실제 Physics2D로 검증했고, 최신 자동 검증은 **41개 모두 PASS**입니다.
원본 프로젝트가 Unity에서 열려 있어 동일 소스를 `Logs/RandomGapValidationProject/`에 격리 복사해 실행했습니다.

**플랫폼 빈틈 150~250px, 먹 두께 15px, 먹이 없을 때 실제 추락, 그린 먹 다리를 건너 다음 플랫폼에 도착하는 동작을 확인했습니다.**
장시간 검증은 시뮬레이션 120초·실제 이동 200u 이상입니다. 이때 **Editor 검증 코드가 입력 처리 함수를 통해 다리 46개를 그리고 Refill을 11회 호출**했습니다.
실제 게임에는 자동 다리나 자동 Refill 기능이 없으며, 사용자가 먹을 그리지 않으면 추락합니다.

상세 자동 기록: [RandomGapInkValidationReport.md](Documentation/RandomGapInkValidationReport.md)

## 실행 방법

1. `Assets/Features/BlackInkPrototype/Scenes/BlackInkTestScene.unity`를 엽니다.
2. **Play**를 누르면 3초 준비 후 캐릭터가 오른쪽으로 자동 전진합니다.
3. 캐릭터가 도착하기 전에 빈틈 양쪽 플랫폼의 윗면을 좌클릭 드래그로 연결합니다.
4. **Refill**은 잉크만 채웁니다. 추락하면 **Reset**으로 처음부터 다시 시작합니다.

메뉴 `Tools > InkRunner > Open Black Ink Test Scene`으로도 같은 씬을 열 수 있습니다.
기존 테스트 씬을 갱신했으며 공용 PlayScene과 분리되어 있습니다.

## 실제 확인한 내용

- **픽셀 크기:** 몸 기준 높이 0.40u와 먹 충돌 두께 0.15u가 현재 Game 뷰에서 실제 **40px / 15px**로 투영됩니다. 100px = 1u 기준입니다.
- **원본 캐릭터:** 테스트 Variant만 `40 / 210` 비율로 축소하고 기존 이미지·Animator를 연결합니다. 몸 기준은 머리~발바닥이며 무기·망토·투명 여백을 제외합니다.
- **출발과 카메라:** 화면 왼쪽 약 8% 위치에서 출발하고 달리는 동안 카메라가 그 위치를 유지하며 따라갑니다.
- **빈틈 범위와 다양성:** 첫 빈틈을 포함해 보행면 사이의 실제 간격이 **150~250px**에 들어갑니다. 각 빈틈은 고정값 대신 서로 다른 랜덤 폭을 사용합니다.
- **실제로 비어 있는 지형:** 빈틈 중심 아래에 숨겨진 지면 Collider2D가 없습니다. 먹 없이 첫 빈틈에 도착한 캐릭터는 실제 중력으로 추락합니다.
- **먹 다리 횡단:** 플레이어 입력 경로로 첫 랜덤 빈틈에 15px 먹을 그린 뒤 실제 Physics2D 캐릭터가 그 위를 밟고 다음 돌 플랫폼에 도착합니다.
- **플랫폼·장식 종류:** 긴 달리기 중 `platform_1`~`platform_6`와 `obstacle_1`~`obstacle_2`가 모두 나타납니다. 장애물에는 Collider2D·Rigidbody2D가 없어 충돌·피격 없이 통과합니다.
- **장시간 생성과 정리:** 검증용 다리·Refill 보조 아래 120초·200u 이상 전진하며 앞쪽 맵을 생성하고 뒤쪽을 정리합니다. 관측된 최대 활성 수는 **플랫폼 6개 / 장애물 4개**, 플랫폼 설정 상한은 32개입니다.
- **먹 생성과 비용:** 빠른 3.6u 붓질은 연속 메시·충돌 몸체를 만들고 정확히 잉크 36을 소모합니다. 빠르거나 느린 샘플링 모두 같은 완성 길이에 같은 비용을 청구합니다.
- **잔량 제한:** 잔량 7이면 마지막 선분을 0.7u로 잘라 생성합니다. 먹의 붓 재질과 번짐 메시를 유지합니다.
- **카메라와 입력:** 단순 클릭과 정지한 마우스는 먹이나 비용을 만들지 않습니다. 마우스를 누른 채 카메라만 움직여도 동일합니다.
- **먹의 월드 고정:** 스크롤 중 기존 먹의 위치·UV·충돌 형상이 유지됩니다. 지나간 먹은 정리하고 현재 다리는 저장 상한 안에서 유지합니다.
- **Refill·Reset:** Refill은 먹과 캐릭터 위치를 유지합니다. Reset은 먹을 지우고 시작 위치·카메라·준비 시간·잉크·초기 맵을 복원하며, 같은 시드의 플랫폼 위치와 빈틈 폭을 재현합니다.
- **추락과 복구:** 추락하면 실패 상태가 되고 Reset으로 실제 물리 몸체와 화면의 Collider 위치를 함께 복원합니다.

기본값은 준비 3초, 달리기 속도 1.8u/초, 잉크 100, 길이 1u당 비용 10입니다.
플랫폼 보행 폭은 2.0~3.6u, 랜덤 빈틈은 1.5~2.5u, 장식의 보이는 높이는 55~95px입니다.
맵 시드는 `27102026`이며 같은 화면 크기와 설정에서 Reset하면 같은 경로를 재생합니다.
세부 구성과 Inspector 위치는 [README_KO.md](README_KO.md)를 확인하세요.

## 실제 Unity 렌더

- [150~250px 랜덤 빈틈](Documentation/ValidationScreenshots/EndlessInk_Gaps150-250px.png)
- [먹 없이 빈틈에 추락](Documentation/ValidationScreenshots/EndlessInk_GapFallen.png)
- [15px 먹 다리 그리기](Documentation/ValidationScreenshots/EndlessInk_Drawing15px.png)
- [먹 다리를 건너 다음 플랫폼에 도착](Documentation/ValidationScreenshots/EndlessInk_GapCrossed.png)
- [검증용 다리·Refill 보조 달리기 10초](Documentation/ValidationScreenshots/EndlessInk_GapRun10seconds.png)
- [검증용 다리·Refill 보조 달리기 60초](Documentation/ValidationScreenshots/EndlessInk_GapRun60seconds.png)

실제 Unity URP 카메라와 HUD를 1920×1080 렌더 타깃에 출력했습니다.
10초·60초는 시뮬레이션 시간입니다. 배치 Editor에서 검증했으며 수작업 시안이나 합성 화면이 아닙니다.

## 재검증과 확인 범위

메뉴 `Tools > InkRunner > Validate Endless Ink Runner (Play Mode)`로 같은 검증을 실행합니다.
예전 `Validate Black Ink Prototype (Play Mode)` 메뉴도 새 검증을 실행합니다.
실행한 프로젝트의 `Logs/EndlessInkValidationResults/`에 결과 보고서와 렌더가 저장됩니다.
이번 원본 출력은 `Logs/RandomGapValidationProject/Logs/EndlessInkValidationResults/`입니다.

- 실제 물리 추락·횡단·장시간 달리기·카메라 이동을 실행했습니다. 입력은 컨트롤러의 입력 처리 함수를 호출했습니다.
- 검증용 자동 입력·Refill은 Editor 코드에만 있습니다. 일반 Play에서는 사용자가 직접 다리를 그리고 Refill을 눌러야 합니다.
- 프로토타입 실행 중 런타임 오류·참조 누락·셰이더 오류·예외는 없었습니다.
- 격리 복사본의 Unity Editor 검색 인덱서에서 `ArgumentOutOfRangeException`이 기록됐습니다. `UnityEditor.Search` 환경 관찰로 원본 보고서에 별도 기록했으며 위 프로토타입 런타임 검증과 구분합니다.
- 실제 OS 마우스·포커스 전환, 모든 Game 뷰 크기에서의 수동 조작, 별도 Player 빌드는 검증하지 않았습니다.
- 공용 PlayScene 통합, 장애물 충돌·피격, 점프·흰 먹·잉크 자동 회복은 이번 구현 범위에 포함하지 않습니다.
- 한 붓질은 x방향이 유지되고 기본 35도 이하인 경로를 지원합니다. 몸 두께 외 붓털·번짐은 비충돌 장식입니다.
- 몸의 기준 높이는 고정하며 애니메이션 포즈별 외곽선은 다를 수 있습니다. Game 뷰 표시 배율이 1×가 아니면 모니터에서 잰 픽셀 수는 달라집니다. HUD는 기존 TMP 폰트의 한글 제한으로 영문입니다.

## 이전 기록과 작업 보호

[EndlessInkValidationReport.md](Documentation/EndlessInkValidationReport.md)와 25px 먹·붙은 지형 스크린샷은 **간격 0u·먹 25px 버전의 32개 통과 기록**으로 유지합니다.
[UnityValidationReport.md](Documentation/UnityValidationReport.md)와 `BlackInk_*.png`는 **2026-10-20의 이전 두 플랫폼 버전** 기록입니다.
이전 기록은 이번 랜덤 빈틈·15px 먹의 최신 41개 결과와 구분해야 합니다.

제공한 플랫폼·장애물 이미지 8개는 `Textures/Environment/`에 `.png`로 복사했고 원본과 바이트가 동일합니다.
원본 Inkwarrior Prefab·스프라이트·Animator·Run 애니메이션, 공용 코드와 공용 씬을 유지합니다.
작업 브랜치는 **InkTest**이며 이번 변경의 **Git Commit·Push는 수행하지 않았습니다.**
