# ScriptableObject 기반 능력치 관리 구현 태스크 리스트

- [x] [UnitData.cs](file:///c:/GameProject/ColorCrash/Assets/Scripts/Units/UnitData.cs) 파일 신규 추가 (캡슐화 및 읽기 전용 프로퍼티 포함)
- [x] [UnitBase.cs](file:///c:/GameProject/ColorCrash/Assets/Scripts/Units/UnitBase.cs) 수정 (UnitData 필드 추가 및 OnEnable에서 능력치 초기화 연동)
- [x] [UnitManager.cs](file:///c:/GameProject/ColorCrash/Assets/Scripts/UnitManager.cs) 수정 (SpawnUnit의 속도 편차 로직을 UnitData.MoveSpeed 기준으로 연동)
- [x] [Assembly-CSharp.csproj](file:///c:/GameProject/ColorCrash/Assembly-CSharp.csproj) 수정 (UnitData.cs 파일 컴파일 항목 추가)
- [x] 코드 빌드 확인 및 검증
