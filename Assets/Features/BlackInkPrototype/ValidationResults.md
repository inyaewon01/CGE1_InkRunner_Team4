# 무한 달리기 구현 및 검증 결과

검증일: **2026-10-27 (한국 시간)**

Unity **6000.3.23f1**에서 이 프로젝트의 실제 `BlackInkTestScene`을 열어 Play Mode와 Physics2D로 검증했습니다.
최신 자동 검증은 **32개 모두 PASS**입니다. **시뮬레이션 시간 120초** 동안 캐릭터가 **200 월드 유닛 이상** 달리며 추락하거나 완료 상태로 멈추지 않았습니다.
검증 중 런타임 오류·참조 누락·셰이더 오류·예외는 기록되지 않았습니다.

원본 자동 검증 기록: [EndlessInkValidationReport.md](Documentation/EndlessInkValidationReport.md)

## 실행 방법

1. `Assets/Features/BlackInkPrototype/Scenes/BlackInkTestScene.unity`를 엽니다.
2. **Play**를 누르고 3초 준비 시간이 지나면 자동 달리기를 확인합니다.
3. 좌클릭 드래그로 먹을 그립니다. **Refill**은 잉크만 채우고, **Reset**은 처음부터 다시 시작합니다.

메뉴 `Tools > InkRunner > Open Black Ink Test Scene`으로도 같은 씬을 열 수 있습니다.
기존 테스트 씬을 무한 달리기로 갱신했으며 공용 PlayScene과 분리되어 있습니다.

## 실제 확인한 내용

- **크기:** 몸 기준 높이 0.40u, 먹 충돌 두께 0.25u가 현재 Game 뷰에서 실제 **40px / 25px**로 투영됩니다. 100px = 1u 기준입니다.
- **원본 캐릭터:** 테스트 Variant만 `40 / 210` 비율로 축소하고 기존 이미지·Animator를 연결합니다. 몸 기준은 머리~발바닥이며 무기·망토·투명 여백을 제외합니다.
- **출발과 카메라:** 화면 왼쪽 약 8% 위치에서 출발하고 이동 중에도 카메라가 그 위치를 유지하며 따라갑니다.
- **랜덤 플랫폼:** 제공한 `platform_1`~`platform_6`가 모두 나타나고 실제 캐릭터가 경계를 넘어 달립니다. 기본 간격 0u로 지면이 이어집니다.
- **장식 장애물:** `obstacle_1`~`obstacle_2`가 중간중간 나타나며 Collider2D·Rigidbody2D가 없습니다. 충돌·피격 판정 없이 통과합니다.
- **장시간 생성과 정리:** 실제 이동 거리 200u 이후에도 앞쪽 맵을 생성하고 뒤쪽을 정리합니다. 관측된 최대 활성 수는 **플랫폼 11개 / 장애물 7개**, 플랫폼 설정 상한은 32개입니다.
- **먹 생성:** 빠른 3.6u 붓질이 연속 메시·충돌 몸체를 만들고 정확히 잉크 36을 소모합니다. 빠르거나 느린 샘플링 모두 같은 완성 길이에 같은 비용을 청구합니다.
- **잔량 제한:** 잔량 7이면 마지막 선분을 0.7u로 잘라 생성합니다. 먹의 붓 재질과 번짐 메시를 유지합니다.
- **카메라와 입력:** 단순 클릭과 정지한 마우스는 먹이나 비용을 만들지 않습니다. 마우스를 누른 채 카메라만 움직여도 동일합니다.
- **먹의 월드 고정:** 스크롤 중 기존 먹의 위치·UV·충돌 형상이 유지되고, 카메라 뒤로 완전히 벗어난 먹은 정리됩니다.
- **Refill:** 기존 먹과 캐릭터 위치를 유지하면서 잉크만 복원합니다.
- **Reset:** 긴 달리기 후 시작 위치·카메라·준비 시간·잉크·초기 맵을 복원하고 생성한 먹을 지웁니다.
- **추락과 복구:** 추락 시 실패 상태가 되고 Reset으로 실제 물리 몸체와 화면의 Collider 위치를 함께 복원합니다.

기본값은 준비 3초, 달리기 속도 1.8u/초, 잉크 100, 길이 1u당 비용 10입니다.
플랫폼은 2.0~3.6u 보행 폭으로 원본 비율을 유지하며, 장식의 보이는 높이는 55~95px입니다.
맵 시드 `27102026`을 사용하므로 같은 화면 크기와 설정에서 Reset하면 같은 랜덤 순서로 재시작합니다.
세부 구성과 Inspector 위치는 [README_KO.md](README_KO.md)를 확인하세요.

## 실제 Unity 렌더

- [25px 먹 그리기](Documentation/ValidationScreenshots/EndlessInk_Drawing25px.png)
- [달리기 10초](Documentation/ValidationScreenshots/EndlessInk_10seconds.png)
- [달리기 60초](Documentation/ValidationScreenshots/EndlessInk_60seconds.png)

실제 Unity URP 카메라와 HUD를 1920×1080 렌더 타깃에 출력했습니다.
10초·60초는 시뮬레이션 시간입니다. 배치 Editor에서 검증했으며 수작업 시안이나 합성 화면이 아닙니다.

## 재검증과 확인 범위

메뉴 `Tools > InkRunner > Validate Endless Ink Runner (Play Mode)`로 같은 검증을 실행합니다.
예전 `Validate Black Ink Prototype (Play Mode)` 메뉴도 새 검증을 실행합니다.
결과 보고서와 렌더는 프로젝트의 `Logs/EndlessInkValidationResults/`에 저장됩니다.

- 실제 물리 달리기와 카메라 이동을 실행했습니다. 입력 검증은 컨트롤러의 입력 처리 함수를 호출합니다.
- 실제 OS 마우스·포커스 전환, 모든 Game 뷰 크기에서의 수동 조작, 별도 Player 빌드는 검증하지 않았습니다.
- 공용 PlayScene 통합, 장애물 충돌·피격, 점프·흰 먹·잉크 자동 회복은 이번 구현 범위에 포함하지 않습니다.
- 한 붓질은 x방향이 유지되고 기본 35도 이하인 경로를 지원합니다. 몸 두께 외 붓털·번짐은 비충돌 장식입니다.
- 몸의 기준 높이는 고정하며 애니메이션 포즈별 외곽선은 다를 수 있습니다. 에디터의 Game 뷰 표시 배율이 1×가 아니면 모니터에서 잰 픽셀 수는 달라집니다.
- 기존 TMP 기본 폰트의 한글 지원 제한으로 HUD는 영문입니다.

## 이전 기록과 작업 보호

[UnityValidationReport.md](Documentation/UnityValidationReport.md)와 `BlackInk_*.png`는 **2026-10-20의 이전 두 플랫폼 버전** 기록으로 유지합니다.
해당 버전의 32개 통과 및 복사본 검증 설명은 위 최신 무한 달리기 결과와 구분해야 합니다.

제공한 플랫폼·장애물 이미지 8개는 `Textures/Environment/`에 `.png`로 복사했고 원본과 바이트가 동일합니다.
원본 Inkwarrior Prefab·스프라이트·Animator·Run 애니메이션, 공용 코드와 공용 씬을 유지합니다.
작업 브랜치는 **InkTest**이며 이번 변경의 **Git Commit·Push는 수행하지 않았습니다.**
