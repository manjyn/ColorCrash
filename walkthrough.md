# ScriptableObject 기반 능력치 관리 구현 결과 보고서 (Walkthrough)

캡슐화된 `UnitData` ScriptableObject를 도입하여 유닛의 로직과 데이터를 성공적으로 분리하였습니다.

## 변경된 주요 내용

### 1. [NEW] [UnitData.cs](file:///c:/GameProject/ColorCrash/Assets/Scripts/Units/UnitData.cs)
- 유닛의 능력치 정보(이름, 체력, 방어력, 공격력, 이동 속도, 공격 사거리, 공격 쿨타임, 탐색 범위)를 저장하는 ScriptableObject 에셋 스크립트입니다.
- **캡슐화 패턴 적용**: 모든 멤버 변수를 `[SerializeField] private`로 선언하여 외부 런타임 수정을 완전 차단하였으며, 외부 노출은 읽기 전용 public 프로퍼티(`UnitName`, `MaxHp`, `Defense` 등)를 통해 캡슐화를 철저히 보장했습니다.

### 2. [MODIFY] [UnitBase.cs](file:///c:/GameProject/ColorCrash/Assets/Scripts/Units/UnitBase.cs)
- `UnitData` 기획 에셋을 참조할 수 있도록 `unitData` 필드를 추가했습니다.
- 유닛이 활성화될 때(`OnEnable`), 기획 에셋의 읽기 전용 값들을 실시간 연산에 사용될 런타임 변수(`maxHp`, `defense`, `attackDamage`, `moveSpeed` 등)에 자동으로 복사하여 초기화합니다.
- 기존 자식 클래스의 수정 없이도 변수들이 100% 호환되도록 설계되었습니다.

### 3. [MODIFY] [UnitManager.cs](file:///c:/GameProject/ColorCrash/Assets/Scripts/UnitManager.cs)
- 유닛 스폰 시 자연스러운 속도 편차를 적용하기 위해 참조하는 기본 속도(`moveSpeed`) 값을 프리팹 직속 값이 아닌, 캐싱된 `UnitData.MoveSpeed` 값을 우선 참조하도록 수정하였습니다.

### 4. [MODIFY] [Assembly-CSharp.csproj](file:///c:/GameProject/ColorCrash/Assembly-CSharp.csproj)
- 새로 컴파일해야 할 `UnitData.cs` 스크립트 파일을 프로젝트 항목에 추가했습니다.

---

## 빌드 검증 결과
- `dotnet build` 명령어를 통해 프로젝트를 빌드한 결과, **오류 0개, 경고 0개**로 문제없이 빌드가 정상 완료되었습니다.
