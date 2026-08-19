# ColorCrash

Unity 기반의 그리드 전투 및 잉크 영토 점령 실시간 전략(RTS) 게임 프로젝트입니다.

## 주요 특징 (Key Features)

- **잉크 영토 점령 시스템 (Ink Territory Control)**:
  - 전장 그리드 타일별 아군/적군 점령 상태 실시간 갱신 및 영토 확장
  - 영토 점령 비율(Territory Ratio) 기반 실시간 형세 계산 및 타임아웃 승리 판정
- **다채로운 유닛 클래스 및 병종 관리 (Unit System)**:
  - 보병(Footman), 궁수(Archer), 엘리트(Elite), 장수(Warlord), 석궁(Crossbow) 역할별 유닛 구성
  - `UnitData` ScriptableObject를 도입하여 능력치 데이터 캡슐화 및 밸런싱 분리
- **방어 타워 및 구조물 시스템 (Tower System)**:
  - 바리케이드(Barricade), 대포(Cannon), 몰타르(Mortar) 등 전술 타워 배치
  - 타워별 고유 공격 사거리, 쿨타임 및 장애물 방어 로직 적용
- **투사체 및 범위 스펠 연동 (Projectiles & Spells)**:
  - 직사 및 곡사 포탄, 잉크 탄환 궤적 계산 (`ProjectileManager`)
  - 잉크 폭탄 등 광역 공격 스펠 시스템 제공 (`SpellDatabase`)
- **지능형 적 AI 시스템 (Battlefield AI & Enemy AI)**:
  - 전장 현황 및 타일 점령 가치를 실시간 분석하는 `BattlefieldEvaluator`
  - AI 스폰 플래너(`AISpawnPlanner`) 및 타겟팅 AI를 통한 자동 적 스폰 판정
  - 최적 경로 검색 알고리즘(`Pathfinder`)을 활용한 장애물 회피 이동
- **동적 스테이지 및 전장 환경 (Stage & BattleContext)**:
  - 1단계(10x14)부터 5단계(24x36)까지 그리드 규모, 제한 시간, 장애물 밀도 동적 조절
  - 월드맵 타일 유형 및 도적단 레벨 연계 전장 구성

## 기술 스택 (Tech Stack)

- **Engine**: Unity 6
- **Language**: C#

## 프로젝트 구조 (Directory Structure)

```
ColorCrash/
├── Assets/
│   ├── Database/               # 유닛/타워/스펠 밸런스 및 SO(ScriptableObject) 에셋
│   ├── Material/ & Shaders/    # 잉크 영토 점령 및 그리드 렌더링용 머티리얼 및 셰이더
│   ├── Prefabs/                # 유닛, 타워, 투사체 및 UI 프리팹 에셋
│   ├── Resources/              # 런타임 동적 로드 데이터베이스 에셋
│   ├── Scenes/                 # 메인 메뉴, 월드맵 및 인게임 전장(Battle) 씬
│   └── Scripts/                # 인게임 핵심 C# 로직 스크립트
│       ├── AI/                 # 적 AI 전술 및 스폰 시스템
│       │   ├── AISpawnPlanner.cs        # 적 스폰 카드 및 타이밍 계획
│       │   ├── AITileTargeting.cs       # 타일 점령 가치 평가 및 타겟팅
│       │   ├── BattlefieldEvaluator.cs  # 전장 형세 및 아군/적군 위협도 산출
│       │   └── EnemyAIController.cs     # 적 AI 메인 컨트롤러
│       ├── BattleField/        # 전장 시스템 및 그리드/전투 관리
│       │   ├── Projectiles/    # 직사/곡사 투사체 로직 (Archer, Cannon, Mortar 등)
│       │   ├── Towers/         # 방어 타워 구조물 로직 (Barricade, Cannon, Mortar)
│       │   ├── UI/             # 전장 UI (체력바, 스폰 카드, 영토 점령 바 등)
│       │   ├── Units/          # 유닛 개별 클래스 및 `UnitData` ScriptableObject
│       │   ├── BattleInkManager.cs  # 잉크 영토 점령 및 비율 실시간 관리자
│       │   ├── GridManager.cs       # 전장 그리드 타일 생성, 좌표 및 상태 관리자
│       │   ├── Pathfinder.cs        # A* 알고리즘 기반 실시간 유닛 길찾기
│       │   └── BattleManager.cs     # 전투 세션, 승패 판정 및 인게임 흐름 제어
│       ├── WorldMap/           # 월드맵 타일 탐색, 정찰 및 세션 진입 시스템
│       ├── DevConsole/         # 개발 및 테스트용 디버그 콘솔 시스템
│       ├── Utils/              # 공용 유틸리티 및 데이터 처리 헬퍼
│       ├── BattleContext.cs    # 스테이지 그리드 규모, 난이도 및 섹션 데이터
│       └── CoreManager.cs      # 게임 싱글톤 관리자 및 최상위 인게임 제어
├── ProjectSettings/            # Unity 엔진 및 프로젝트 설정 파일
└── ColorCrash.sln              # Visual Studio C# 솔루션 파일
```

---
© ColorCrash Project.
