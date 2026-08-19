# Implementation Plan - 독립 스펠 InkBomb & 계층적 2단계 트리 동적 비율 시스템

적 커맨더 AI가 잉크폭탄(`InkBomb`)을 손패 카드 슬롯에서 제하고 **언제든지 발동 가능한 독립 커맨드 마법(3 Ink 소모)**으로 관리하며, **계층적 2단계 트리 파이프라인(스펠 시전 ➔ 동적 목표 비율 & 손패 쏠림 폴백 ➔ 전략 소환)**을 구축하는 통합 구현계획서입니다.

---

## 🏛️ 계층적 2단계 트리 의사결정 파이프라인 명세

```
[제 1선택] 독립 스펠 Ink Bomb 발동 평가 (손패 미포함, 3 Ink 소모)
   ├── (유저 2기 이상 밀집 & 잉크>=3 & 주사위 성공 시) ➔ [Ink Bomb 즉시 발동!] (종료)
   └── (미시전 시) ➔ [제 2선택] 동적 목표 비율 (유닛 vs 건물) 평가
                         ├── (유닛 선택 또는 건물 0장 폴백 시) ➔ [지능형 유닛 소환] (종료)
                         └── (건물 선택 또는 유닛 0장 폴백 시) ➔ [전략적 건물 건설] (종료)
```

---

### 📌 [제 1선택]: 독립 마법 스펠 Ink Bomb 발동 평가

- **메커니즘**: `InkBomb`은 손패 4장에 포함되지 않으며 잉크 조건만 충족되면 언제든지 발동 가능합니다.
- **발동 조건**:
  1. 현재 소지 잉크가 3 이상인가? (`currentInk >= 3`)
  2. 전장에 유저(Blue) 유닛이 **2기 이상 뭉쳐있는 밀집 구역**이 존재하는가?
  3. AI의 스펠 시전 확률(`CounterChance`, 기본 60%) 판정을 통과했는가?
- **결과**:
  - **[YES]**: 3 Ink를 소비하여 `Ink Bomb`을 유저 밀집 타일에 즉시 투하하고 의사결정을 종료합니다.
  - **[NO]**: **[제 2선택] (유닛 vs 건물)**으로 이동합니다.

---

### 📌 [제 2선택]: 유닛(Unit) vs 건물(Building) 동적 비율 & 폴백 평가

- **목표 비율 롤링**: 전투 개시 시 성향 범위 내에서 $\text{TargetRatio} = \text{Random.Range}(2.5f, 4.5f)$ 롤링.
- **실시간 필드 비율 비교**: $\text{CurrentRatio} = \frac{\text{CurrentRedUnitCount}}{\text{Mathf.Max}(1, \text{RedStructureCount})}$
  - $\text{CurrentRatio} < \text{TargetRatio}$ ➔ 1차 **[유닛 소환 페이즈]**
  - $\text{CurrentRatio} \ge \text{TargetRatio}$ ➔ 1차 **[건물 배치 페이즈]**

#### 🛡️ 비상 오버라이드 및 손패 쏠림 극단 예외 폴백 (Fallback Engine)
1. **비상 오버라이드**: 필드 Red 유닛이 **0기(전멸)**이거나 위협 수치가 비상(`ThreatLevel >= 2.0f`)이면 **즉시 [유닛 페이즈]로 오버라이드**.
2. **손패 쏠림 극단 폴백 (Hand Bias Fallback)**:
   - **건물 페이즈 결정이나 손패 4장에 건물 카드가 0장일 시**: 잉크를 낭비하지 않고 **즉시 [유닛 페이즈]로 자동 전환**.
   - **유닛 페이즈 결정이나 손패 4장에 유닛 카드가 0장일 시**: **즉시 [건물 페이즈]로 자동 전환**.
   - **극단 쏠림 시 잉크 4 이상 소지할 시**: 1 Ink 리롤 시스템(`TryInkReroll`)을 작동시켜 손패 덱 교체.

---

### 📌 [3단계]: 전략적 최종 소환 실행

- **[최종 유닛 페이즈 확정 시]**: `PlanUnitSpawn()`을 통해 유저 위협 라인 및 카운터 유닛 지능형 소환.
- **[최종 건물 페이즈 확정 시]**: `SelectStrategicBuilding()`을 통해 4대 가상점 전략 건물 지능형 건설.

---

## 📂 변경 예정 파일 명세

### 1. ScriptableObject & Setup
- **`BattleAIDataSO.cs`**: `MinUnitBuildingRatio` (기본 2.5f) 및 `MaxUnitBuildingRatio` (기본 4.5f) 파라미터 추가.

### 2. Decision Tree Engine Refactoring
- **`EnemyAIController.cs`**: `ExecuteDecisionPipeline()`을 계층적 2단계 트리 구조로 전면 리팩토링 및 `>>>>>>>` `Debug.LogError` 추적 로그 탑재.
- **`AISpawnPlanner.cs`**: `PlanSpellSpawn()` 손패 미의존 독립 스펠 평가로 개편 및 손패 내 유닛/건물 소지 검사 헬퍼 (`HasUnitCard`, `HasBuildingCard`) 구현.

---

## ⚙️ 검증 계획
- `dotnet build Assembly-CSharp.csproj` 컴파일 성공 및 오류 0개 검증.
- 유니티 콘솔 창에서 `>>>>>>>` `Debug.LogError`를 모니터링하여 독립 스펠 시전, 비율 롤링, 손패 폴백 발동 여부 실시간 확인.
