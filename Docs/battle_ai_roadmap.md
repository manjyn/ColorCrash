# 컬러 크러쉬: Red팀 전투 AI 5단계 개발 로드맵 (Battle AI Implementation Roadmap)

---

## 📌 개요 (Overview)

본 문서는 Red팀 인게임 실시간 전투 커맨더 인공지능([battle_ai_gdd.md](file:///C:/GameProject/ColorCrash/Docs/battle_ai_gdd.md))의 C# 스크립트 모듈 구현을 안정적이고 체계적으로 진행하기 위한 **5단계 연차별 개발 로드맵 및 검증 가이드**입니다.

1~4단계 코드 작업 결과물(`BattleAIDataSO.cs`, `BattlefieldEvaluator.cs`, `AITileTargeting.cs`, `AISpawnPlanner.cs`, `EnemyAIController.cs`)의 구현 결과를 종합 분석하여, 마지막 **5단계 작업에 유니티 에디터 에셋 4종 자동 생성기(`BattleAIDataAssetCreator.cs`) 및 AI 실시간 디버거(`BattleAIDebugger.cs`) 구축 계획을 구체화하여 업데이트**하였습니다.

---

## 🗺️ 5단계 개발 로드맵 한눈에 보기

```
[ 1단계: 데이터 & 전황 평가기 (완료) ] ➔ [ 2단계: 유닛 스폰 & 타깃팅 (완료) ] ➔ [ 3단계: 건물 & 스펠 타깃팅 (완료) ] ➔ [ 4단계: AI 메인 루프 & 4-Phase (완료) ] ➔ [ 5단계: 최적화 & 밸런싱 (차기 작업) ]
```

---

## 📑 단계별 세부 개발 및 검증 가이드

### 1단계: AI 데이터 자산 & 전황 평가기 (완료 ✅)

- **구현 완료 파일**:
  - `Assets/Scripts/AI/BattleAIDataSO.cs` (ScriptableObject 데이터 클래스)
  - `Assets/Scripts/AI/BattlefieldEvaluator.cs` (전황 평가기 컴포넌트)
- **달성된 성과**:
  - 난이도별/성향별 파라미터 데이터 자산 구조체 작성 (`EvaluationInterval`, `MinActionInterval`, `CounterChance` 등)
  - `(Red 타일 / 전체 타일) * 100` 점령 비율 O(1) 카운터 계산 및 `OnTilePainted()` 연동
  - Red 팀 기준 최전방 전선 Y 좌표(`FrontlineY`) `int[] _redTilesPerY` 배열 추적 연산
  - Max Pop Cap (`10 + N`) 한도 체크 및 Zero GC Alloc 재사용 버퍼 시스템 구축

---

### 2단계: 유닛 소환 & 스폰 존 충돌 분산 (완료 ✅)

- **구현 완료 파일**:
  - `Assets/Scripts/AI/AITileTargeting.cs` (타깃 좌표 연산기 - 스폰 모듈)
  - `Assets/Scripts/AI/AISpawnPlanner.cs` (소환 의사결정 결정기 - 유닛 모듈)
- **달성된 성과**:
  - 유저 진격 라인 X 좌표 분석 후 Red 팀 지정 스폰 존(`MapHeight - 1`, `MapHeight - 2`) 최적 X 좌표 선정
  - `XSpreadOffsets` 순회를 이용한 충돌 분산(Body Collision Spreading) 구현
  - 소유 잉크 검사 및 기초 유닛(`Footman`, `Archer`, `Elite`, `Warlord`) 소환 행동 구조체(`AISpawnAction`) 도출

---

### 3단계: 건물 배치 & 마법 스펠 타깃팅 및 Ink Bomb 시전 발동 (완료 ✅)

- **구현 완료 파일**:
  - `Assets/Scripts/AI/AITileTargeting.cs` (건물/스펠 타깃 연산 확장)
  - `Assets/Scripts/AI/AISpawnPlanner.cs` (건물/스펠 의사결정 및 Ink Bomb 발동 헬퍼 확장)
- **달성된 성과**:
  - **건물(4x1 Barricade, 2x2 Cannon/Mortar)**: 100% Red 팀 소유 타일 검사 및 전선 Y 기준 로컬 바운딩 탐색 구현
  - 건물 배치 시 해당 타일의 유닛 밀쳐내기 구조체 `BuildingDisplacementInfo` 제공
  - **Ink Bomb (3 Ink, 3x3 범위)**: 유저 유닛 뭉침(`O(U^2)`) 탐색, `Mathf.Clamp` 경계 보정 및 장애물 타일 1칸 자동 시프트(Shift) 보정 연산 완성
  - **Ink Bomb 3x3 도색 및 50 데미지 발동 연동**: `ExecuteInkBombSpell()` 메서드 및 `OnInkBombTriggered` 이벤트 연동 완성

---

### 4단계: AI 메인 루프 & 4-Phase 전술 (완료 ✅)

- **구현 완료 파일**:
  - `Assets/Scripts/AI/EnemyAIController.cs` (AI 메인 컨트롤러)
- **달성된 성과**:
  - **동적 잉크 템포 엔진**: N 인자 기본 충전, Phase 4 2배속(`0.5s` + 피버 가속) 및 15 Ink 한도 해제, 점령률 35% 미만 열세 시 [분노의 잉크] 가속(`0.77s`) 구현
  - **5단계 통합 의사결정 파이프라인**: `[1순위] Ink Bomb 스펠` ➔ `[2순위] 건물 요새` ➔ `[3순위] 유저 카운터 유닛` ➔ `[4순위] 1 Ink 손패 리롤` ➔ `[5순위] 저코스트 덱 순환` 순차 알고리즘 완성
  - **4-Phase FSM 전환**: 전투 제한시간 경과 비율에 따른 Phase 1(20%), Phase 2(30%), Phase 3(30%), Phase 4(20%) 동적 전환 구현
  - **인적 감성 쿨다운 및 카드 4슬롯 순환 드로우**: `MinActionInterval` 연사 방지 및 사용 카드 슬롯 드로우 구현

---

### 5단계: 실시간 최적화 & 밸런싱 (Optimization & Polishing) - 차기 작업 🎯

- **핵심 목표**: 유니티 에디터 에셋 4종 자동 생성 에디터 헬퍼 구현, 실시간 AI 디버거 시각화 및 Zero GC Alloc 최종 점검.
- **구현 대상 파일**:
  - `Assets/Editor/BattleAIDataAssetCreator.cs` (유니티 에디터 에셋 자동 생성 스크립트 - 신규 작성)
  - `Assets/Scripts/AI/BattleAIDebugger.cs` (실시간 AI 런타임 디버그 뷰어 - 신규 작성)
- **고도화된 세부 구현 명세**:
  1. **유니티 에디터 에셋 4종 자동 생성기 (`BattleAIDataAssetCreator.cs`)**:
     - Unity 에디터 상단 메뉴 (`Tools/ColorCrash/Create All Battle AI Data Profiles`) 클릭 시 4가지 대표 에셋 파일 자동 생성:
       - `Assets/Database/AIDatabase/BattleAIData_Easy_Agro.asset` (쉬움, 영토 선점형)
       - `Assets/Database/AIDatabase/BattleAIData_Normal_Bunker.asset` (보통, 요새 방어형)
       - `Assets/Database/AIDatabase/BattleAIData_Hard_Adaptive.asset` (어려움, 실시간 카운터형)
       - `Assets/Database/AIDatabase/BattleAIData_Boss_Stage1.asset` (보스전 특화)
  2. **AI 실시간 런타임 디버거 (`BattleAIDebugger.cs`)**:
     - `EnemyAIController`, `BattlefieldEvaluator`의 실시간 상태(점령률, 최전방 전선 Y, 위협도, 잉크 충전 템포, 4-Phase 상태, 손패 4장 및 최근 결정 행동)를 유니티 인스펙터 및 씬 Gizmos에 직관적으로 시각화.
  3. **Zero GC Alloc & 성능 튜닝 검증**:
     - `BattlefieldEvaluator`, `AITileTargeting`, `AISpawnPlanner`, `EnemyAIController` 전체의 재사용 버퍼 누수 점검 및 Zero GC Alloc 60fps 유지 확인.
- **단계별 검증 테스트 (Verification)**:
  - [ ] 유니티 에디터 메뉴를 클릭하면 `Assets/Database/AIDatabase/` 하위에 4종의 `.asset` 프로필 데이터 파일이 정상 생성되는가?
  - [ ] 씬 실행 시 `BattleAIDebugger`가 AI의 점령률, 4-Phase 상태, 잉크 충전 템포, 손패 카드 및 소환 타일을 인스펙터에 실시간으로 시각화 표시하는가?
  - [ ] C# 컴파일 빌드(`dotnet build Assembly-CSharp.csproj`)에서 오류 0개가 발생하는가?

---

### 💡 다음 진행 단계 안내

1~4단계 완성 결과 및 5단계 최적화/밸런싱 구현 명세를 반영하여 **[battle_ai_roadmap.md](file:///c:/GameProject/ColorCrash/Docs/battle_ai_roadmap.md) 문서가 정밀 업데이트 완료**되었습니다.

준비가 되시면 **"5단계 작업 시작해줘"**라고 지시해 주시면, 5단계(`BattleAIDataAssetCreator.cs` - 에셋 4종 자동 생성기 및 `BattleAIDebugger.cs` - AI 실시간 디버거) 구현 계획(Implementation Plan) 수립 후 C# 코드 작성을 진행하여 **Red팀 전투 AI 개발을 최종 완성**하겠습니다!
