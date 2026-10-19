# 검은 먹 플랫폼 테스트

## 실행

Unity **6000.3.23f1**에서 `Scenes/BlackInkTestScene.unity`를 열고 Play를 누릅니다.
메뉴 `Tools > InkRunner > Open Black Ink Test Scene`으로도 열 수 있습니다.
시작 후 3초 동안 먼저 다리를 그릴 수 있고, 이후 Inkwarrior가 오른쪽으로 자동 전진합니다.
출발 플랫폼의 오른쪽 끝은 x=0, 도착 플랫폼의 왼쪽 끝은 x=3입니다.
붉은 점선 부근(y=0)을 따라 x=-0.3부터 x=3.3까지 연결하면 됩니다.

- **좌클릭 드래그:** 마우스 경로를 윗면으로 하는 두꺼운 검은 먹 플랫폼 생성.
- **Reset:** 캐릭터 위치·속도·애니메이션, 생성물, 잉크, 입력과 준비 시간을 초기화.
- **Refill:** 잉크만 최대치로 복원. 길과 캐릭터 위치 유지. 놓았다가 새로 클릭해 그립니다.
- 추락하면 `FALLEN`, 도착 플랫폼에 착지하면 `CROSSED`가 표시됩니다.

설정·일시정지·점프·흰 먹 제거·자동 회복은 이 테스트의 범위에 포함하지 않았습니다.
기본 TMP 폰트에 한글 글리프가 없어 테스트 HUD는 영문으로 표시합니다.

## 기존 리소스와 보호 범위

- `Assets/Prefabs/Inkwarrior.prefab`: PlayScene에서 사용하는 원본.
- `Assets/Sprites/TempKJS/run_01.png` ~ `run_16.png`: 기존 캐릭터 이미지.
- `Assets/Sprites/TempKJS/Inkwarrior.controller`: 기존 Animator.
- `Assets/Sprites/TempKJS/InkWarrior_Run.anim`: 기존 실제 Run 애니메이션.
- `Assets/Script/Chr/InkWarriorCollision.cs`: 원본의 장애물 태그 감지. 테스트 Variant에서만 제외.
- `Assets/Script/Core/GameManager.cs`, `PoolManager.cs`: 현재 빈 구현.

프로젝트에는 이동·맵 스크롤·접지·먹 선택·잉크 자원의 실제 구현이 없어 테스트 전용 코드를 만들었습니다.
테스트에서는 **고정 지형 + 캐릭터 전진** 방식 하나만 실행합니다. 따라서 스크롤에 따른 길이/비용 오차가 없습니다.
원본 캐릭터·Animator·공용 코드·TitleScene·PlayScene·ScoreScene·패키지·Build Settings는 수정하지 않았습니다.
테스트 캐릭터는 원본을 상속한 Prefab Variant입니다. 기존 그림·프레임·Animator 연결을 유지합니다.
Run만 존재하므로 준비 중에는 해당 첫 포즈를 정지하고 달릴 때 기존 Run을 재생합니다.

## 구성 파일

모든 구현은 `Assets/Features/BlackInkPrototype/` 아래에 있습니다.

| 경로 | 역할 |
| --- | --- |
| `Scenes/BlackInkTestScene.unity` | 전용 실행 씬 |
| `Prefabs/Inkwarrior_BlackInkTest.prefab` | 원본을 상속하는 물리 설정용 Variant |
| `Scripts/BlackInkTestController.cs` | 준비·자동 이동·추락/도착·입력·잉크·제한·Reset/Refill |
| `Scripts/InkPathMath.cs` | 길이 예산·기울기·두께 면적 충돌 검사 |
| `Scripts/InkStrokeGeometry.cs` | 연속 메시와 PolygonCollider2D |
| `Scripts/BlackInkHud.cs` | TMP/uGUI 게이지·수치·버튼·UI 입력 차단 |
| `Materials/InkBrush.shader`, `InkBrush.mat` | 불투명한 검은 몸체, 붓털 끝과 아래 번짐 |
| `Materials/InkGaugeUI.shader` | 원본 게이지 UV 분리와 붉은색/붓 표시 |
| `Materials/InkSurface.physicsMaterial2D` | 마찰·반발 0 물리 재질 |
| `Materials/TestGround.mat`, `DrawingGuide.mat`, `TestPlatformQuad.asset` | 테스트 지형과 비충돌 점선 |
| `Textures/InkGaugeSource.png` | 제공된 최신 붉은 테두리 PNG 원본을 그대로 복사 |
| `Editor/BlackInkSceneBuilder.cs` | 테스트 씬과 Variant 재생성 도구 |
| `Editor/BlackInkValidation.cs` | Play Mode/실제 물리/입력·비용 자동 검증 |

폴더와 파일의 **모든 `.meta`를 함께 커밋**해야 GUID 연결이 유지됩니다.
`Assets/Features.meta`와 `Assets/Features/BlackInkPrototype.meta`도 포함합니다.
`Temp/` 아래 검증용 복사본·로그·컴파일 출력은 커밋 대상이 아닙니다.
자동 Commit·Push·브랜치 변경·Pull·병합은 수행하지 않았습니다.

## Inspector

씬의 **Black Ink Test - select for tuning** 오브젝트를 선택합니다.

| 값 | 기본값 | 설명 |
| --- | --- | --- |
| Maximum Ink | 100 | 전체 예산 |
| Cost Per Unit | 10 | 실제 생성한 경로 1 월드 유닛당 비용. 최대 10유닛 |
| Character Body Height | 2.10 | `run_01`, 머리~발바닥 약 210px / 100PPU. 무기·망토·투명 여백 제외, 고정 기준 |
| Thickness Ratio | 0.75 | 주 몸체 두께 1.575. 작은 아래 번짐과 붓털은 비충돌 장식 |
| Minimum Point Spacing Ratio | 0.025 | 초기 간격 0.0525유닛. 릴리스에서 남은 유효 길이를 반영 |
| Platform Gap | 3 | Play 시작 시 도착 플랫폼 간격 반영 |
| Preparation Seconds | 3 | 준비 중 그리기 허용 |
| Run Speed | 1.8 | 캐릭터 수평 속도. 지형은 정지 |
| Maximum Slope | 35° | 수직벽·급경사 제한 |
| Clear Previous On New Stroke | OFF | ON이면 새 첫 유효 선분 성공 시 기존 길 교체. 환급 없음 |
| Texture Scale | 0.7 | 월드 기준 붓결 크기 |
| Edge Roughness | 0.5 | 가장자리/번짐 거칠기 |
| Offscreen Cleanup Distance | 8 | 카메라와 캐릭터 모두 뒤로 충분히 벗어난 길만 정리 |
| Maximum Points / Segments / Strokes | 2048 / 1984 / 64 | 상한에 닿으면 새 생성 중단, 기존 길 유지 |
| No Draw Zones | 빈 배열 | 연결한 Collider의 영역과 실제 두꺼운 몸체 전체가 겹치면 생성 차단 |

몸 높이는 매 애니메이션 프레임마다 재계산하지 않습니다. 고정 두께의 비용은 두께와 독립입니다.
기본 Layer를 사용하고 프로젝트의 기존 2D 충돌 설정을 따릅니다.
Variant에서 기존 Capsule을 비트리거로 바꾸고 중력·연속 충돌·회전 고정을 적용했습니다.

## 입력과 외형의 지원 범위

한 붓질은 x방향이 유지되는 경로이며, 좌→우와 우→좌 모두 가능합니다.
방향을 되돌리거나 급경사로 움직이면 선을 끊고 새 유효 구간에서 다시 시작합니다.
따라서 루프·복잡한 자기 교차는 생성하지 않습니다. 기존 다른 붓질과의 시각적 겹침은 허용합니다.
빠른 입력은 중간을 분할 검사하므로 UI/캐릭터/금지 영역을 가로질러 연결하지 않습니다.
UI에서 시작한 클릭, 고갈·상한·창 이탈·포커스 상실 후에는 새 클릭이 필요합니다.

먹 외형은 절차적 셰이더입니다. 중심은 불투명하고, 윗면은 매끄러운 실제 충돌 경로이며,
아래에 약한 번짐과 불규칙한 붓털 끝이 붙습니다. 길이 방향 UV는 월드 길이를 사용해 늘어짐을 막습니다.
별도의 먹 플랫폼 참고 이미지는 첨부되지 않아 문서의 외형 설명을 기준으로 제작했습니다.
붓털과 번짐에는 별도 Collider를 만들지 않습니다.

게이지는 원본 합성 이미지의 투명 여백을 제외한 UV 영역을 사용합니다.
붓은 비율을 유지한 약 50×50 영역, 전체 게이지는 500×50입니다.
프레임은 고정 크기의 끝/몸통 조각으로 표시하고 검은 채움은 별도 고정 폭 텍스처입니다.
검은 채움의 오른쪽 가시 영역만 RectMask2D로 줄여 100/50/0%를 표시합니다.
붓과 테두리는 움직이거나 압축되지 않습니다. 원본 PNG에 검은 채움을 합성하지 않았습니다.
몸통 테두리 조각의 미세한 반복은 남을 수 있습니다. 원본 붉은 게이지 그림은 보존했습니다.

## 검증

메뉴 `Tools > InkRunner > Validate Black Ink Prototype (Play Mode)`로 재검증할 수 있습니다.
이 도구는 전용 씬을 열어 테스트를 실행하고 `Logs/BlackInkValidationResults/`에 결과와 화면을 저장합니다.
실행 전 편집 중인 씬을 저장하고 Play Mode를 종료합니다. 검증 종료 후 테스트 씬이 열린 상태로 남습니다.
최종 실행 결과와 확인 범위는 동봉된 `ValidationResults.md`를 확인하세요.

공용 씬에서의 통합 동작, 실제 맵 스크롤 연동, 흰 먹 연동, 별도 Player 빌드는 이번 검증 범위에 포함하지 않습니다.
