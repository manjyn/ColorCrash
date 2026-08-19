# 일체형 UnitDatabase 기반 능력치 관리 시스템 구현 계획

개별 유닛별로 존재하던 `UnitData` ScriptableObject 에셋들을 하나로 통합하여, 단 하나의 `UnitDatabase` 에셋 파일에서 모든 유닛의 능력치를 일괄적으로 편집하고 관리할 수 있도록 구조를 변경합니다.

## 제안된 변경 사항

### [DELETE] [UnitData.cs](file:///c:/GameProject/ColorCrash/Assets/Scripts/Units/UnitData.cs)
- 개별 유닛용 ScriptableObject 스크립트를 삭제합니다. (컴파일 빌드 시 충돌 방지를 위해 프로젝트 빌드 목록에서도 제외)

---

### [NEW] [UnitDatabase.cs](file:///c:/GameProject/ColorCrash/Assets/Scripts/Units/UnitDatabase.cs)
모든 유닛의 수치 데이터를 일체형 리스트로 보관하는 통합 데이터베이스 ScriptableObject입니다.
- **`UnitDataRecord` 구조체 정의 (캡슐화 적용)**:
  - 각 유닛의 데이터 단위입니다. `[System.Serializable]`이 적용됩니다.
  - 필드: `unitId`, `unitName`, `maxHp`, `defense`, `attackDamage`, `moveSpeed`, `attackRange`, `attackCooldown`, `searchRadius`
- **`UnitDatabase` 클래스 정의**:
  - `[SerializeField] private List<UnitDataRecord> records` 필드를 가집니다.
  - `Initialize()`: 런타임에 빠른 조회가 가능하도록 `records` 리스트를 기반으로 `Dictionary<string, UnitDataRecord>`를 구성합니다.
  - `TryGetRecord(string unitId, out UnitDataRecord record)`: 외부에서 유닛 ID로 레코드를 즉시 검색하는 기능을 제공합니다.

---

### [MODIFY] [UnitBase.cs](file:///c:/GameProject/ColorCrash/Assets/Scripts/Units/UnitBase.cs)
- **참조 변수 수정**:
  - 기존: `[SerializeField] protected UnitData unitData;` 에셋 참조 필드 제거.
  - 변경: `[SerializeField] protected string unitId;` 고유 키 문자열 추가 및 읽기 전용 프로퍼티 `public string UnitId => unitId;` 노출.
- **능력치 초기화 연동 (`OnEnable`)**:
  - `UnitManager.Instance.UnitDatabase`를 통해 자신의 `unitId`에 해당하는 `UnitDataRecord`를 찾습니다.
  - 찾은 레코드에서 모든 런타임 변수(`maxHp`, `defense`, `attackDamage`, `moveSpeed` 등)를 복사하여 초기화합니다.
  - `currentHp = maxHp` 설정으로 유닛 풀 복귀 시 초기화 처리합니다.

---

### [MODIFY] [UnitManager.cs](file:///c:/GameProject/ColorCrash/Assets/Scripts/UnitManager.cs)
- **통합 데이터베이스 참조 추가**:
  - `[SerializeField] private UnitDatabase unitDatabase;` 및 `public UnitDatabase UnitDatabase => unitDatabase;` 추가.
- **데이터베이스 초기화 (`Awake`)**:
  - `Awake()` 내부에서 `unitDatabase.Initialize()`를 호출하여 빠른 조회를 위한 딕셔너리를 생성합니다.
- **스폰 속도 편차 로직 수정 (`SpawnUnit`)**:
  - 스폰할 프리팹(`prefab`)의 `UnitId`를 사용하여 `unitDatabase`에서 속도 데이터를 검색한 뒤, 편차를 적용하여 인스턴스의 `moveSpeed`를 초기화합니다.

---

### [MODIFY] [Assembly-CSharp.csproj](file:///c:/GameProject/ColorCrash/Assembly-CSharp.csproj)
- 컴파일 대상 목록에서 `UnitData.cs`를 삭제하고, `UnitDatabase.cs`를 추가합니다.
  - `<Compile Include="Assets\Scripts\Units\UnitDatabase.cs" />` 추가.

---

## 검증 계획 (Verification Plan)

### 빌드 및 컴파일 테스트
- `dotnet build Assembly-CSharp.csproj` 명령어를 실행하여 컴파일 오류가 없는지 검증합니다.

### 기획 환경 검증
- 유니티 에디터에서 마우스 우클릭 메뉴(`ColorCrash/Unit Database`)를 통해 단 하나의 통합 데이터베이스 파일이 정상적으로 생성 및 수정되는지 확인합니다.
- 유닛 프리팹에 서로 다른 `unitId`를 지정하고 게임 실행 시 `UnitDatabase`로부터 각각 올바른 능력치 데이터가 주입되는지 확인합니다.
