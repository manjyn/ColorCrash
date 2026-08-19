# 컬러 크러쉬: 전투 적(Red 팀) AI 기획서

---

## 목차 (Table of Contents)
- [1. 개요 (Overview)](#1-개요-overview)
  - [1.1 적 커맨더 AI vs 유닛 자율 AI 역할 분담 (Two-Layer System)](#11-적-커맨더-ai-vs-유닛-자율-ai-역할-분담-two-layer-system)
- [2. AI 의사결정 시스템 아키텍처 (Decision-Making System)](#2-ai-의사결정-시스템-아키텍처-decision-making-system)
  - [2.1 전장 상태 평가 지표 (Battlefield State Evaluator)](#21-전장-상태-평가-지표-battlefield-state-evaluator)
  - [2.2 월드맵 전달 인자 연동 (Map Scale & Unit Scale)](#22-월드맵-전달-인자-연동-map-scale--unit-scale)
- [3. AI 전술 성향 (AI Archetypes)](#3-ai-전술-성향-ai-archetypes)
  - [3.1 유형 A: 영토 선점형 (Agro Painter - 소형 맵 전용)](#31-유형-a-영토-선점형-agro-painter---소형-맵-전용)
  - [3.2 유형 B: 요새 방어형 (Bunker Fortress - 중/대형 맵)](#32-유형-b-요새-방어형-bunker-fortress---중대형-맵)
  - [3.3 유형 C: 유저 카운터형 (Adaptive Counter - 정예/보스전)](#33-유형-c-유저-카운터형-adaptive-counter---정예보스전)
- [4. 카드 타입별 소환 및 배치 알고리즘 (Placement Rules)](#4-카드-타입별-소환-및-배치-알고리즘-placement-rules)
  - [4.1 유닛 소환 좌표 탐색 (지정 스폰 존 제한 및 충돌 분산)](#41-유닛-소환-좌표-탐색-지정-스폰-존-제한-및-충돌-분산)
  - [4.2 건물 배치 좌표 탐색 (Building Placement)](#42-건물-배치-좌표-탐색-building-placement)
  - [4.3 마법 스펠 타겟팅 (Ink Bomb Placement)](#43-마법-스펠-타겟팅-ink-bomb-placement)
- [5. 동적 4단계 Phase별 적 AI 제어 로직 (Dynamic 4-Phase AI Logic)](#5-동적-4단계-phase별-적-ai-제어-로직-dynamic-4-phase-ai-logic)
- [6. 손패 리롤 (Ink Reroll) 및 분노의 잉크 활용 로직](#6-손패-리롤-ink-reroll-및-분노의-잉크-활용-로직)
- [7. 난이도별 AI 파라미터 (Difficulty Configuration)](#7-난이도별-ai-파라미터-difficulty-configuration)
- [8. ScriptableObject 기반 데이터 아키텍처](#8-scriptableobject-기반-데이터-아키텍처)
  - [8.1 AI 데이터 자산 클래스 (AIDataSO.cs)](#81-ai-데이터-자산-클래스-aidatasocs)
  - [8.2 유니티 프로젝트 파일 구조 및 런타임 주입 Flow](#82-유니티-프로젝트-파일-구조-및-런타임-주입-flow)
- [9. 실시간 최적화 및 연산 가이드라인 (Optimization & Performance Guide)](#9-실시간-최적화-및-연산-가이드라인-optimization--performance-guide)
  - [9.1 이벤트 기반 타일/전선 상태 캐싱 (O(1) 연산)](#91-이벤트-기반-타일전선-상태-캐싱-o1-연산)
  - [9.2 유닛 중심 스펠 타겟팅 (Unit-centric Search)](#92-유닛-중심-스펠-타겟팅-unit-centric-search)
  - [9.3 건물 배치 좌표 로컬 바운딩 탐색 (Local Bounding Search)](#93-건물-배치-좌표-로컬-바운딩-탐색-local-bounding-search)
  - [9.4 Zero GC Alloc 재사용 버퍼 시스템](#94-zero-gc-alloc-재사용-버퍼-시스템)
  - [9.5 유닛 자율 AI 시야 감지 타임슬라이싱 & 맨해튼 거리 연산](#95-유닛-자율-ai-시야-감지-타임슬라이싱--맨해튼-거리-연산)

---

## 1. 개요 (Overview)

본 기획서는 Color Crash 전장에서 **적 (Red 팀)** 플레이어 역할을 수행하는 **적 커맨더 인공지능 (Enemy Commander AI)**의 의사결정 구조, 전황 판단 알고리즘, 유닛/건물/마법 스펠 소환 및 타일 배치 규칙을 정의합니다.

AI의 목적은 플레이어에게 단순한 정적 패턴이 아닌, 전황(타일 점령 비율, 전선 위치, 유닛 상성, 물리적 넉백 및 시야 반경)과 월드맵 전달 인자에 유연하게 대응하는 전략적 재미를 제공하는 것입니다.

### 1.1 적 커맨더 AI vs 유닛 자율 AI 역할 분담 (Two-Layer System)

Color Crash의 전투 시스템은 2개의 계층화된 AI로 명확히 역할이 분리됩니다:

```
[ 상위 계층: 적 커맨더 AI (Enemy Commander AI) ] ── (본 기획서의 핵심 범위)
  │  - 잉크 자원 관리, 손패 리롤, 카드 소환 시점 및 스폰/배치 위치(X,Y) 결정
  │  - 전황(점령률/위협도) 평가 및 벙커 방어선 구축 등 전략 수립
  ▼
[ 카드 소환 명령 ]
  │
  ▼
[ 하위 계층: 유닛 자율 AI (Unit Autonomous AI) ] ── (전투 기본 물리/행동 메카닉)
     - 아군(Blue) 및 적군(Red) 유닛 공통 적용
     - 지정 스폰 존 출격 후 전방 직진, 시야(Aggro Range 3칸) 감지, 타겟팅, 사거리 공격, 타일 칠하기, 넉백 수용
```

---

## 2. AI 의사결정 시스템 아키텍처 (Decision-Making System)

적 커맨더 AI는 설정된 **의사결정 평가 주기(Evaluation Interval: 난이도/프로필에 따라 0.3초~1.5초, 기본 0.8초)**마다 전장 상태를 평가(Evaluator)하고, 현재 상태(State), 소유 잉크(Ink), 인구수(Pop Cap)에 따라 최선의 소환 행동(Action)을 결정합니다.

```
[ 전장 상태 평가 (Evaluator) ] ── (의사결정 평가 주기: 0.3s ~ 1.5s)
       │
       ▼
[ AI 상태 전환 (FSM / Utility) ]
       │
       ▼
[ 소환 및 건물/스펠 선택 (Action Planner) ]
       │
       ▼
[ 타일 좌표 탐색 & 스폰 (Tile Targeting) ]
```

### 2.1 전장 상태 평가 지표 (Battlefield State Evaluator)
AI는 아래 5가지 지표를 실시간 측정합니다:
1. **점령 비율 (Territory Ratio)**: `(AI 타일 수 / 전체 타일 수) * 100` (70% 패배 위기 커트라인 실시간 감지)
2. **최전방 전선 (Frontline Y-Coord)**: AI(Red 팀) 진영 기준 가장 먼 곳에 위치한 AI 타일의 Y 좌표.
3. **위협도 (Threat Level)**: AI 전선 및 시야 반경(Aggro Range 3칸) 이내에 존재하는 유저 유닛의 수 및 화력 합산.
4. **건물 밀집도 (Structure Density)**: AI 영역 내 설치된 건물(Barricade, Cannon, Mortar) 수 및 배치 상태.
5. **현재 인구수 (Current Population)**: `현재 필드 내 AI 유닛 수 / 최대 인구수(Pop Cap)` 비율.

### 2.2 월드맵 전달 인자 연동 (Map Scale & Unit Scale)
AI는 전장 진입 시 월드맵에서 넘겨받은 **전장 크기(1~5단계 MapScale)**와 **유닛 규모 인자(UnitScaleParam, N = 1~30)**를 판정에 즉시 반영합니다.
- **최대 인구수 한도(Max Pop Cap = 10 + N) 체크**: 필드 유닛 수가 한도에 도달하면 유닛 소환을 멈추고 잉크를 모아 건물 또는 마법 스펠(Ink Bomb) 위주로 투입합니다.
  - *건물 제한 소형 맵(1~2단계) 예외 처리*: 건물 소환이 금지된 맵에서는 인구수 한도 도달 시 **'Ink Bomb 스펠 시전'**을 최우선 시도하며, 스펠 카드가 없거나 잉크가 부족한 경우 필드 유닛 사멸 전까지 리롤을 시도하거나 **소환 대기 모드(Idle Standby)**로 전환하여 잉크 누수를 방지합니다.
- **1~2단계 소형 맵 건물 카드 제한 인식**: 소형 맵 진입 시 건물 소환 로직을 비활성화하고 유닛 중심 카드 소환 및 잉크 사용에 100% 집중.
- **잉크 수급 템포(`1.2초 - N * 0.02초`) 적응**: N 값이 커질수록 잉크가 빨리 차므로 카드를 주저하지 않고 빠르게 연속 연타 소환.

---

## 3. AI 전술 성향 (AI Archetypes)

전장 크기 및 카드 해금 상태에 따라 3가지 AI 전술 유형이 동적 선택됩니다.

### 3.1 유형 A: 영토 선점형 (Agro Painter - 소형 맵 전용)
- **추천 전장**: 1~2단계 소형 전장 (건물 카드 제한전)
- **주요 목표**: 초기 중립 타일을 빠르게 칠하여 점령 비율 및 잉크 수급량 우위 점유.
- **주력 카드**: `Footman`, `Archer`
- **소환 전략**: 잉크가 찰 때마다 최전방 전선 타일에 Footman 유닛을 즉시 스폰하여 영토 확장.

### 3.2 유형 B: 요새 방어형 (Bunker Fortress - 중/대형 맵)
- **추천 전장**: 3~4단계 중/대형 전장
- **주요 목표**: 전장 중앙 길목에 Barricade + Cannon + Mortar 중심의 킬존(Kill Zone) 구축.
- **주력 카드**: `Barricade`, `Cannon`, `Mortar`, `Archer`
- **소환 전략**: 
  - 잉크 6 이상 모일 때까지 대기 후 [Barricade ➔ Cannon/Mortar] 연계 소환.
  - Barricade 뒤에 Archer를 배치하여 안정적인 거점 사수.

### 3.3 유형 C: 유저 카운터형 (Adaptive Counter - 정예/보스전)
- **추천 전장**: 4~5단계 대형/초대형 전장 및 정예/보스 스테이지
- **주요 목표**: 유저가 소환한 유닛/건물/마법 상성에 맞춰 실시간 대응.
- **상성 대응 표**:
  | 유저의 행동 | AI의 대응 행동 | 배치 목표 위치 |
  | :--- | :--- | :--- |
  | 유저 근접 유닛 (Footman/Elite) 다수 뭉침 | **Mortar** 또는 **Ink Bomb (스펠)** 시전 | 뭉쳐있는 유저 유닛 중앙 타겟 |
  | 유저 Warlord (공성 탱커) 소환 | **Cannon** 건설 + **Elite** 스폰 | Warlord 이동 경로 상 킬존 |
  | 유저 Barricade/Cannon 요새 구축 | **Mortar** (3~10칸 사거리) 또는 **Warlord** 소환 | Barricade 사거리 밖 후방 / 전방 |
  | 유저 Archer/Mortar 원거리 지원 | **Footman / Elite** 스폰 및 넉백 활용 | 유저 측면/전방 빠른 파고들기 |

- **카운터 카드 미보유 및 데드락 방지 대체 행동 순서 (Fallback Order)**:
  1. **[1순위] 카운터 카드 소환**: 위협 유저 유닛에 대응하는 적합한 카운터 카드가 손패에 있고 잉크가 충분하면 즉시 소환.
  2. **[2순위] 잉크 소비 리롤 시도**: 카운터 카드가 없고 잉크가 4 이상 남아있으면 1 Ink를 소비하여 손패 리롤(쿨타임 2초) 실행.
  3. **[3순위] 저코스트 덱 순환 소환**: 리롤 불가능 또는 리롤 후에도 카운터 카드가 없으면, **현재 손패 중 소모 잉크가 가장 낮은 카드(Footman/Archer)를 최전방 전선에 소환**하여 덱 순환 및 라인 압박 지속.

---

## 4. 카드 타입별 소환 및 배치 알고리즘 (Placement Rules)

### 4.1 유닛 소환 좌표 탐색 (지정 스폰 존 제한 및 충돌 분산)
- **엄격한 소환 위치 제한 (Base Spawn Zone)**:
  - 모든 유닛은 Red 팀 지정 스폰 존(최상단 스폰 구역) 내부 타일에서만 소환됩니다.
- **라인 카운터 대응 X 좌표 선택**:
  - 적 커맨더 AI는 진격해오는 유저 유닛의 라인(X 좌표)을 분석하고, 해당 라인과 맞물리는 **적 스폰 존 내부의 최적 X 타일 좌표**를 선택하여 소환합니다.
- **스폰 존 내 충돌 분산 (Body Collision Spreading)**:
  - 지정 스폰 존 타일(1x1)에 이미 유닛이 소환되어 존재할 경우, 동일 타일에 겹치지 않고 인접한 스폰 존 옆 타일로 소환 위치를 넓게 분산시켜 출격시킵니다.
- **출격 후 유닛 자율 AI 행동 및 경계 이탈 방지**:
  - 스폰 존에서 생성된 유닛은 전진하며 이동하다가 **시야 반경(Aggro Range 3칸)** 내에 적 유닛/건물이 포착되면 궤적을 꺾어 최단 거리로 접근해 교전을 수행합니다.
  - 피격 넉백이나 건물 밀쳐내기 발생 시 **전장 경계선 밖이나 장애물 내부로 밀려나지 않고 경계 타일에 정지(Boundary Clamping)** 처리됩니다.

### 4.2 건물 배치 좌표 탐색 (Building Placement)
- **건물 설치 타일 필수 조건**: 건물 설치 영역(Barricade 4x1, Cannon 2x2, Mortar 2x2) 전체 타일이 **반드시 아군(Red 팀) 소유 타일**이어야 합니다. 중립 타일이나 유저 타일이 단 1칸이라도 포함된 지점에는 건설할 수 없으며, 조건에 맞는 아군 타일 영역을 우선 탐색합니다.
- **Barricade (4x1)**: 전선 최전방 중앙 길목(Chokepoint) 아군 타일 탐색. 건물 영역 유닛 존재 시 유닛을 외곽 타일로 밀쳐내며(Push Out) 즉시 건설. (3~5단계 전장 전용)
- **Cannon (2x2)**: Barricade 바로 1~2칸 뒤쪽 아군 타일. 5칸 직사 사거리 내 유저 진격로 확보 타일.
- **Mortar (2x2)**: 최후방 안전 아군 타일 (유저 유닛이 즉시 접근할 수 없는 아군 영역 깊숙한 곳, 3~10칸 곡사 지원).

### 4.3 마법 스펠 타겟팅 (Ink Bomb Placement)
- **Ink Bomb (3 Ink)**: 유저 유닛이 3기 이상 뭉쳐있는 타일 지점 또는 유저의 핵심 건물(Cannon/Mortar) 위치에 3x3 범위 타겟팅 즉발 시전 (즉시 데미지 50 + 100% 아군 타일 도색).
- **타겟 좌표 오차 보정 및 Clamping 알고리즘**:
  - 난이도별 타겟팅 오차(`tileTargetOffset`: Easy ±2, Normal ±1) 계산 후 산출된 최종 X, Y 좌표는 전장 경계 밖으로 벗어나지 않도록 `Mathf.Clamp(TargetX, 0, MapWidth-1)`, `Mathf.Clamp(TargetY, 0, MapHeight-1)` 처리를 거칩니다.
  - 연산된 중심 타일이 이동/점령 불가 장애물(Obstacle) 타일일 경우, **3x3 범위 내 가장 많은 유저 유닛이 포함된 인접 이동 가능 타일로 좌표를 1칸 자동 시프트(Shift)**하여 시전 효과를 보장합니다.

---

## 5. 동적 4단계 Phase별 적 AI 제어 로직 (Dynamic 4-Phase AI Logic)

적 커맨더 AI는 4단계 Phase 전개에 발맞추어 자원 사용 및 소환 우선순위를 동적으로 전환합니다.

- **Phase 1: 영토 선점 단계 (Opening Rush - 20% 시간)**
  - 첫 손패 Footman 100% 우선 소환. 잉크가 찰 때마다 최전방 전선 타일에 Footman을 연속 스폰하여 중앙 무색 타일 점령 총력전.
- **Phase 2: 전선 구축 및 수비 단계 (Fortress Build / Line Holding - 30% 시간)**
  - **건물 허용 맵 (3~5단계)**: Barricade 1 Ink 할인 이점을 감지하여 전방 벙커 방어선 건설. 
  - **건물 제한 맵 (1~2단계)**: 아군 타일 위 유닛 방어력 +5 버프 및 잉크 -1 할인을 활용하여 Footman/Archer 물량으로 전선 고착 사수.
- **Phase 3: 요새 공성 및 정예 돌격 단계 (Siege & Counter / Elite Charge - 30% 시간)**
  - **건물 허용 맵 (3~5단계)**: Mortar/Warlord/InkBomb 우선 소환. 적 건물 파괴 시 주변 3x3 중립화 영역 즉시 재점령 유닛 투입.
  - **건물 제한 맵 (1~2단계)**: Elite/Warlord 잉크 -1 할인 및 공격력 +20% 가속 특성을 활용하여 유저 라인을 강하게 뚫어내는 정예 돌격전 수행.
- **Phase 4: 컬러 피버 (Color Fever Climax - 최후 20% 시간)**
  - 잉크 2배속(0.5초당 1 Ink) 및 0초 손패 리로드 환경에 발맞춰 **소환 딜레이 무제한 가속(Zerg Rush)**. 남아있는 모든 잉크를 유닛/마법 스펠 소환에 쏟아부어 막판 타일 점령률 역전 시도.
  - *인적 감성 쿨다운 제약 (Min Action Interval)*: 가속 상황에서도 프레임 단위 연속 스폰으로 인한 연산 과부하 및 부자연스러움을 방지하기 위해 **카드 간 최소 소환 간격(`minActionInterval`, 기본 0.2초)** 제약을 유지합니다.

---

## 6. 손패 리롤 (Ink Reroll) 및 분노의 잉크 활용 로직

1. **손패 리롤 (Ink Reroll) AI 사용 조건**:
   - 현재 잉크가 4 이상이나, 손패 카드 4장 중 위협도에 대응할 적절한 카운터 카드가 없을 때 1 Ink를 소비하여 리롤(쿨타임 2초) 실행.
2. **분노의 잉크 (Fury Ink) 대응 (AI 점령률 40% 미만 열세 시)**:
   - 잉크 충전이 30% 가속(0.77초당 1 Ink)되는 순간을 감지하여, 카드를 아끼지 않고 즉시 대량 소환하여 전선 복구 시도.

---

## 7. 난이도별 AI 파라미터 (Difficulty Configuration)

| 파라미터 | 쉬움 (Easy) | 보통 (Normal) | 어려움 (Hard) |
| :--- | :--- | :--- | :--- |
| 의사결정 평가 주기 (Evaluation Interval) | 1.5초 | 0.8초 | 0.3초 (실시간) |
| 카드 간 최소 소환 간격 (Min Action Interval) | 0.4초 | 0.2초 | 0.1초 |
| 잉크 수급 가중치 | 1.0x | 1.0x | 1.15x |
| 카운터 카드 및 스펠(Ink Bomb) 사용 확률 | 30% | 60% | 95% |
| 콤보 소환 (Barricade+Mortar) 활용 | 사용 안 함 | 간헐적 사용 | 적극 콤보 사용 |
| 손패 리롤(Ink Reroll) 및 Fallback 활용 | 사용 안 함 | 잉크 여유 시 사용 | 상황에 따라 적극 사용 |
| 최적 타일 탐색 오차 및 Clamping | ±2칸 (Clamping) | ±1칸 (Clamping) | 100% 최적 타일 (Clamping) |

---

## 8. ScriptableObject 기반 데이터 아키텍처

AI의 로직(C# 스크립트)과 AI 수치/성향(데이터 자산)을 완벽히 분리하여, 프로그래머의 코드 변경 없이 유니티 인스펙터 창에서 기획자가 AI 성향과 난이도를 1초 만에 수정 및 실시간 테스트할 수 있는 **ScriptableObject 기반 실무 표준 구조**입니다.

### 8.1 배틀 AI 데이터 자산 클래스 (BattleAIDataSO.cs)

```csharp
using System.Collections.Generic;
using UnityEngine;

public enum AIArchetype { AgroPainter, BunkerFortress, AdaptiveCounter }

[CreateAssetMenu(fileName = "BattleAIData_", menuName = "ColorCrash/Battle AI Data Profile")]
public class BattleAIDataSO : ScriptableObject
{
    [Header("기본 성향 설정")]
    public string profileName = "Normal_Bunker";
    public AIArchetype archetype = AIArchetype.BunkerFortress;

    [Header("의사결정 및 자원 파라미터")]
    [Tooltip("전황을 평가하고 다음 행동을 결정하는 의사결정 평가 주기 (초)")]
    public float evaluationInterval = 0.8f;

    [Tooltip("카드 간 연속 소환 최소 딜레이 간격 (초)")]
    public float minActionInterval = 0.2f;

    [Tooltip("기본 잉크 충전 가중치 배율")]
    public float inkMultiplier = 1.0f;

    [Tooltip("Phase 4 컬러 피버 시 잉크 충전 및 행동 가속 배율")]
    public float feverTempoMultiplier = 1.5f;

    [Tooltip("AI 점령률이 이 수치 미만으로 떨어지면 위기/분노 모드 발동 (0.0~1.0)")]
    [Range(0f, 1f)] public float crisisTerritoryThreshold = 0.35f;

    [Tooltip("유저 카운터 카드/스펠(Ink Bomb) 시전 성공 확률 (0.0~1.0)")]
    [Range(0f, 1f)] public float counterChance = 0.6f;

    [Tooltip("카운터 카드가 손패에 없을 때 1 Ink 리롤 활용 확률 (0.0~1.0)")]
    [Range(0f, 1f)] public float rerollChance = 0.3f;

    [Header("선호 카드 덱 설정")]
    public List<string> preferredUnitCards = new List<string> { "Footman", "Elite" };
    public List<string> preferredBuildingCards = new List<string> { "Barricade", "Mortar" };
    public List<string> preferredSpellCards = new List<string> { "InkBomb" };

    [Header("타겟팅 정밀도")]
    [Tooltip("타일 배치 목표 지점 오차 범위 (0: 100% 최적, 1~2: 오차 발생)")]
    public int tileTargetOffset = 1;
}
```

### 8.2 유니티 프로젝트 파일 구조 및 런타임 주입 Flow

```
Assets/
├── Database/
│   ├── UnitDatabase.asset
│   ├── TowerDatabase.asset
│   └── AIDatabase/                   ◄── AI 데이터 자산 폴더
│       ├── BattleAIData_Easy_Agro.asset
│       ├── BattleAIData_Normal_Bunker.asset
│       ├── BattleAIData_Hard_Adaptive.asset
│       └── BattleAIData_Boss_Stage1.asset
└── Scripts/AI/
    ├── BattleAIDataSO.cs              ◄── 배틀 씬 AI 데이터 구조체 ScriptableObject
    ├── WorldAIDataSO.cs               ◄── 월드 씬 AI 대전략 데이터 구조체 ScriptableObject
    ├── EnemyAIController.cs           ◄── AI 메인 루프 & ScriptableObject 수치 바인딩
    ├── BattlefieldEvaluator.cs        ◄── 전황/위협도/점령률 실시간 평가기
    ├── AISpawnPlanner.cs             ◄── 카운터 카드 & Ink Bomb 시전 결정기
    └── AITileTargeting.cs             ◄── 충돌 분산 소환 타일 좌표 연산기
```

#### 런타임 주입 Flow
1. **월드맵에서 전장 씬 진입**: 월드맵 인자로 스테이지 ID 및 난이도 지정.
2. **AI 데이터 로드**: `EnemyAIController`가 해당 스테이지에 매핑된 `BattleAIData_XXXX.asset`을 전달받음.
3. **실시간 평가 및 실행**: AI는 코드 변경 없이 에셋에 지정된 `evaluationInterval`, `counterChance`, `preferredCards` 수치에 맞춰 유연하게 의사결정 수행.

---

## 9. 실시간 최적화 및 연산 가이드라인 (Optimization & Performance Guide)

실시간 전장 환경(최대 5단계 초대형 맵 24x36 = 864 타일, 양팀 총 80기 유닛 출격, 0.3초 실시간 AI 주기)에서 프레임 드랍(GC Freeze) 및 CPU 병목을 방지하기 위한 기술적 구현 가이드라인입니다.

### 9.1 이벤트 기반 타일/전선 상태 캐싱 (O(1) 연산)
- **문제점**: evaluationInterval(0.3s)마다 864개 전체 타일을 전수 순회(`for` 루프)하여 점령 비율 및 최전방 전선 Y 좌표를 검색 시 CPU Spike 발생.
- **최적화 구현 룰**:
  1. **점령 타일 카운터 캐싱**: 타일 도색 시 발생하는 `TilePaintedEvent`를 수신하여 `RedTileCount`, `BlueTileCount` 카운터를 `+1`, `-1` 조절하여 점령 비율을 `O(1)`로 즉시 계산.
  2. **Y축 라인별 도색 타일 카운터 배열**: `int[] redTilesPerY = new int[MapHeight]` 배열을 관리하여, 도색 시 해당 Y값만 갱신. 최전방 전선 Y 좌표 탐색 시 배열을 상단부터 최하단으로 순회(최대 36회 연산)하여 탐색 연산을 극소화.

### 9.2 유닛 중심 스펠 타겟팅 (Unit-centric Search)
- **문제점**: 3x3 범위 내 유저 유닛 3기 이상 뭉친 타일을 찾기 위해 864개 타일을 전수 조사할 경우 `O(W * H * U)`의 막대한 부하 발생.
- **최적화 구현 룰**:
  - 타일 전수 조사를 금지하고, **현재 필드에 살아있는 유저 유닛 목록(`List<Unit> aliveUserUnits`)을 기준**으로 탐색.
  - 살아있는 유저 유닛의 위치 중심 주변 3x3 반경 내 다른 유저 유닛 개수를 검색하여 `O(U^2)` (최대 40기 기준 1,600회 연산 이하) 내에 최적 Ink Bomb 시전 지점을 도출.

### 9.3 건물 배치 좌표 로컬 바운딩 탐색 (Local Bounding Search)
- **문제점**: 건물(4x1, 2x2) 설치 타일의 100% 아군 소유 여부를 전체 맵 타일 2중 루프로 검사 시 연산 낭비.
- **최적화 구현 룰**:
  - **Barricade (4x1)**: 최전방 전선 Y 좌표 기준 `Y - 1 ~ Y + 1` 행(Row) 범위 내 아군 타일 영역만 검사.
  - **Cannon (2x2)**: Barricade 건설 지점 직후방 `Y + 1 ~ Y + 3` 행 범위만 검사.
  - **Mortar (2x2)**: Red 팀 스폰 존 직후방 최후방 3행 범위 내로 검사 영역을 제한.

### 9.4 Zero GC Alloc 재사용 버퍼 시스템
- **문제점**: 0.3초 주기마다 유닛/타일 쿼리용 `List<Unit>`, `List<Vector2Int>`를 동적 생성(`new`) 시 모바일 기기 메모리 파편화 및 1~2초 주기의 GC Freeze 발생.
- **최적화 구현 룰**:
  - `BattlefieldEvaluator` 및 `AITileTargeting` 스크립트 내부 필드에 재사용 쿼리 버퍼(`List<Unit> _searchUnitBuffer`, `List<Vector2Int> _validTileBuffer`)를 사전 할당.
  - 매 연산 시 `Clear()` 후 재사용하는 Zero GC Alloc 패턴을 엄격히 적용.

### 9.5 유닛 자율 AI 시야 감지 타임슬라이싱 & 맨해튼 거리 연산
- **문제점**: 필드 80기 유닛이 매 프레임 `Update()`에서 `Physics.OverlapSphere`를 실행 시 초당 4,800회 이상의 거리 연산 병목.
- **최적화 구현 룰**:
  1. **그리드 맨해튼 거리 연산**: 3D 물리 거리 대신 그리드 타일 좌표 기반 수식 `Mathf.Abs(dx) + Mathf.Abs(dy) <= 3`으로 가벼운 시야 판정 수행.
  2. **타임슬라이싱(Time-slicing)**: 시야 감지 주기를 매 프레임이 아닌 `0.1초~0.15초 쿨다운`으로 분산 처리.
