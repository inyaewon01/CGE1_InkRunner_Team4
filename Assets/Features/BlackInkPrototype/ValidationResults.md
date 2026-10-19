# 구현 및 검증 완료

검증일: 2026-10-20 (한국 시간)

Unity **6000.3.23f1**, 기존 URP 2D **17.3.0**, 기존 Input System **1.20.0**을 유지했습니다.
최종 소스와 동일한 복사본에서 실제 Unity Play Mode 및 Physics2D를 실행했고, **32개 검증이 모두 통과**했습니다.
검증한 씬·Prefab Variant·재질·GUID 메타데이터를 본 프로젝트의 `Assets/Features/BlackInkPrototype/`로 옮겼습니다.

## 바로 실행

1. `Scenes/BlackInkTestScene.unity`를 엽니다.
2. Play를 누릅니다.
3. 3초 준비 시간에 붉은 점선을 따라 두 플랫폼 사이를 좌클릭 드래그합니다.
4. 캐릭터가 자동 전진해 다리를 건넙니다. Reset으로 재시작하고 Refill로 먹만 보충합니다.

Inspector 조절은 씬의 **Black Ink Test - select for tuning** 오브젝트에서 합니다.
기준 H=2.10, 두께=1.575, 잉크=100, 비용=10/월드 유닛, 간격=3, 준비=3초, 길 유지=ON입니다.
상세 구성과 조절값은 `README_KO.md`, 함께 커밋할 정확한 목록은 `FileManifest.txt`를 확인합니다.

## 실제 확인한 내용

- 원본 Inkwarrior 이미지·Animator를 연결한 Variant가 정상 시작합니다.
- 생성 중에도 메시와 실제 PolygonCollider2D가 존재하며, 주 몸체는 마우스 윗면에서 아래로 0.75H만큼 생성됩니다.
- 같은 3.6유닛 경로는 빠르게/느리게 입력해도 36의 잉크를 소비합니다.
- 정지한 마우스와 단순 클릭은 길과 비용을 생성하지 않습니다.
- 끝점 사이의 면적과 두께까지 검사해 금지 영역을 가로질러 연결하지 않습니다.
- UI에서 시작한 클릭은 버튼을 놓을 때까지 차단합니다.
- 잔량 7이면 0.7유닛만 생성하며, 잉크 0에서도 기존 길과 충돌은 남습니다.
- 새 붓질은 기존 길을 유지합니다. 교체 옵션은 첫 유효 선분에서만 이전 시각·충돌을 함께 지웁니다.
- 최대 점 상한에 도달하면 새 생성만 멈춥니다. 역방향 붓질도 정상 생성합니다.
- 포커스 상실 후 진행 중인 붓질을 재연결하지 않습니다. 이 항목은 입력 처리 함수를 직접 호출해 검증했습니다.
- Refill은 길과 캐릭터 위치를 유지합니다. Reset은 생성물을 없애고 준비 상태와 잉크를 복원합니다.
- **완성된 다리의 연결부와 완만한 오르막·내리막을 실제 캐릭터가 건넜습니다.**
- **같은 붓질을 누른 채 생성된 일부 구간을 실제로 밟고, 그 길을 더 늘려 도착까지 건넜습니다.**
- 다리가 없으면 실제 중력으로 떨어지고 실패 상태가 됩니다.
- 횡단 후 Reset도 물리 위치와 화면의 Collider 위치를 즉시 복원합니다.
- 게이지 100%/50%/0%를 실제 카메라와 uGUI를 URP 렌더 타깃에 출력해 확인했습니다.
- 프로토타입 실행 중 누락된 참조·셰이더 오류·런타임 예외가 없었습니다.

원본 자동 검증 기록: `Documentation/UnityValidationReport.md`

실제 Unity 렌더:

- `Documentation/ValidationScreenshots/BlackInk_100.png`
- `Documentation/ValidationScreenshots/BlackInk_50.png`
- `Documentation/ValidationScreenshots/BlackInk_0.png`
- `Documentation/ValidationScreenshots/BlackInk_Crossed.png`

일반 ScreenCapture는 배치 Editor의 게임 화면을 저장하지 못해, 카메라와 동일한 UI를 실제 URP 렌더 타깃으로 출력했습니다.
수작업으로 재현한 시안이나 합성 그림이 아닙니다.

## 범위와 한계

- 실제 기존 스크롤 구현이 없어 **고정 지형 + 캐릭터 자동 전진** 테스트입니다. 맵 스크롤 연동은 검증하지 않았습니다.
- 한 붓질은 x방향이 유지되고 기본 35도 이하인 경로를 지원합니다. 루프·자기 교차·수직벽은 선을 끊어 제한합니다.
- 먹 플랫폼 참고 이미지는 별도로 첨부되지 않아, 명세의 외형 설명에 맞춘 절차적 붓 표현을 제작했습니다. 내부는 불투명하고 아래 번짐·붓털 끝은 비충돌 장식입니다.
- 붉은 게이지는 제공된 원본을 보존하고 UV로 분리했습니다. 테두리 몸통의 미세한 반복은 남을 수 있습니다.
- 기존 TMP 기본 폰트가 한글을 지원하지 않아 테스트 HUD는 영문입니다.
- 공용 PlayScene 통합, 실제 OS 포커스/모든 창 크기에서의 수동 입력, 흰 먹, 별도 Player 빌드는 검증하지 않았습니다.
- 검증용 복사본의 Unity 자체 검색 인덱서에서 시작 시 `ArgumentOutOfRangeException`이 기록됐습니다. 스택은 `UnityEditor.Search`이며 프로토타입 코드와 분리해 원본 결과에 기록했습니다. 위 32개 기능 검증에는 영향이 없었습니다.

## 작업 보호

기존 `Assets/Prefabs/Inkwarrior.prefab`, 스프라이트, Animator/Run 애니메이션, 공용 씬과 공용 코드는 수정하지 않았습니다.
테스트 Variant에서만 기존 장애물 태그 감지 컴포넌트를 제외하고, 중력·비트리거 Capsule·물리 재질을 설정했습니다.
Unity 버전·패키지·Build Settings·Layer 설정을 바꾸지 않았습니다.
작업 브랜치는 기존 **PlaySceneBranch**를 유지했으며, Pull·브랜치 변경·Commit·Push·병합을 수행하지 않았습니다.
